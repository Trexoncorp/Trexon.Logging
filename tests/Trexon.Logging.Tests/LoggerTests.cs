using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Trexon.Logging.Tests;

public sealed class LoggerTests
{
    [Fact]
    public void Structured_message_keeps_template_properties_and_fixed_timestamp()
    {
        var sink = new MemorySink();
        var now = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        using var logger = Logger.Create(c => c.WithTimeProvider(new FixedClock(now)).WriteTo.Sink(sink));
        logger.Info("Order {OrderId} costs {Price:0.00}; repeat {OrderId}", 42, 12.5m);
        var e = Assert.Single(sink.Events);
        Assert.Equal("Order 42 costs 12.50; repeat 42", e.Message);
        Assert.Equal("Order {OrderId} costs {Price:0.00}; repeat {OrderId}", e.MessageTemplate);
        Assert.Equal(42, e.Properties["OrderId"]);
        Assert.Equal(12.5m, e.Properties["Price"]);
        Assert.Equal(now, e.Timestamp);
        Assert.Equal(LogLevel.Information, e.Level);
        Assert.Null(e.Caller);
        Assert.Null(e.Exception);
    }

    [Theory]
    [InlineData("{{Name}} {Name}", "{Name} Alice")]
    [InlineData("missing {Absent}", "missing {Absent}")]
    [InlineData("literal {bad-name} {Name}", "literal {bad-name} Alice")]
    [InlineData("unclosed {Name", "unclosed {Name")]
    public void Escapes_and_malformed_templates_are_predictable(string template, string expected)
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        if (template.Contains("{Name}", StringComparison.Ordinal)) logger.Info(template, "Alice");
        else logger.Info(template);
        Assert.Equal(expected, Assert.Single(sink.Events).Message);
    }

    [Fact]
    public void Null_and_unused_arguments_remain_structured()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        logger.Info("Value {Value}", (object?)null, 19);
        var e = Assert.Single(sink.Events);
        Assert.Equal("Value null", e.Message);
        Assert.Null(e.Properties["Value"]);
        Assert.Equal(19, e.Properties["_arg1"]);
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Minimum_level_is_inclusive(LogLevel threshold)
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.MinimumLevel(threshold).WriteTo.Sink(sink));
        foreach (var level in Enum.GetValues<LogLevel>()) logger.Write(level, "event");
        Assert.Equal(Enum.GetValues<LogLevel>().Count(l => l >= threshold && l != LogLevel.None), sink.Events.Count);
        Assert.All(sink.Events, e => Assert.True(e.Level >= threshold));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void Disabled_levels_do_not_inspect_argument_values()
    {
        var sink = new MemorySink();
        var failures = new List<LoggingFailure>();
        using var logger = Logger.Create(c => c.MinimumLevel(LogLevel.None).OnError(failures.Add).WriteTo.Sink(sink));
        logger.Critical("{Value}", new Unserializable());
        Assert.Empty(failures);
        Assert.Empty(sink.Events);
        Assert.False(logger.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void Disabled_sink_thresholds_also_skip_argument_capture()
    {
        var failures = new List<LoggingFailure>();
        using var logger = Logger.Create(c => c.OnError(failures.Add).WriteTo.Sink(new MemorySink(), LogLevel.Error));
        logger.Info("{Value}", new Unserializable());
        Assert.Empty(failures);
        Assert.False(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void Formatting_is_invariant_to_the_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            var sink = new MemorySink();
            using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
            logger.Info("{Price:0.00}", 12.5m);
            Assert.Equal("12.50", Assert.Single(sink.Events).Message);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Bound_context_is_immutable_and_message_values_take_precedence()
    {
        var sink = new MemorySink();
        using var root = Logger.Create(c => c.Enrich.WithProperty("Name", "global").WriteTo.Sink(sink));
        using var scope = root.PushScope("Name", "scope");
        using var bound = root.Bind("Name", "bound");
        root.Info("{Name}");
        bound.Info("{Name}");
        bound.Info("{Name}", "event");
        root.Info("{Name}");
        Assert.Equal(new[] { "scope", "bound", "event", "scope" }, sink.Events.Select(e => e.Message));
        Assert.Throws<NotSupportedException>(() => ((IDictionary)sink.Events[0].Properties).Add("New", 1));
    }

    [Fact]
    public void Complex_values_are_snapshots_rather_than_mutable_references()
    {
        var sink = new MemorySink();
        var values = new List<string> { "original" };
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        using var bound = logger.Bind("Context", values);
        logger.Info("{Values}", values);
        values[0] = "changed";
        bound.Info("bound");
        Assert.Equal("original", Assert.IsType<JsonElement>(sink.Events[0].Properties["Values"])[0].GetString());
        Assert.Equal("original", Assert.IsType<JsonElement>(sink.Events[1].Properties["Context"])[0].GetString());
    }

    [Fact]
    public void Exception_snapshot_preserves_stack_and_aggregate_children()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        var original = ThrownException();
        logger.Error(new AggregateException("outer", original, new ArgumentException("second")), "failed {Id}", 7);
        var error = Assert.Single(sink.Events).Exception!;
        Assert.Equal(typeof(AggregateException).FullName, error.Type);
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Contains(nameof(ThrownException), error.InnerExceptions[0].StackTrace);
        Assert.Equal("second", error.InnerExceptions[1].Message);
    }

    [Fact]
    public void Caller_capture_identifies_the_application_frame()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.Enrich.WithCaller().WriteTo.Sink(sink));
        WriteFromApplication(logger);
        var caller = Assert.Single(sink.Events).Caller!;
        Assert.Equal(nameof(WriteFromApplication), caller.MemberName);
        Assert.EndsWith("LoggerTests.cs", caller.FilePath);
        Assert.True(caller.LineNumber > 0);
    }

    [Fact]
    public void Configuration_is_snapshotted_when_the_logger_is_created()
    {
        LoggerConfiguration captured = null!;
        ConsoleSinkConfiguration capturedOutput = null!;
        var first = new StringWriter();
        var second = new StringWriter();
        using var logger = Logger.Create(c =>
        {
            captured = c;
            c.WriteTo.Console(output => { capturedOutput = output; output.Writer(first).Timestamp(null); });
        });
        captured.MinimumLevel(LogLevel.None).Enrich.WithProperty("Later", true);
        capturedOutput.Writer(second).Json();
        logger.Info("event");
        Assert.Contains("[INF] event", first.ToString());
        Assert.Empty(second.ToString());
    }

    [Fact]
    public void Invalid_configuration_fails_at_creation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Logger.Create(c => c.MinimumLevel((LogLevel)99)));
        Assert.ThrowsAny<ArgumentException>(() => Logger.Create(c => c.Redact("[")));
        Assert.Throws<ArgumentOutOfRangeException>(() => Logger.Create(c => c.Redact("x", timeout: TimeSpan.FromMilliseconds(-1))));
        Assert.Throws<ArgumentException>(() => Logger.Create(c => c.WriteTo.File(" ")));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteFromApplication(Logger logger) => logger.Info("caller");

    private static Exception ThrownException()
    {
        try { throw new InvalidOperationException("first"); }
        catch (Exception exception) { return exception; }
    }

    private sealed class Unserializable
    {
        public string Value => throw new InvalidOperationException("Should not be evaluated.");
    }
}
