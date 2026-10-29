using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trexon.Logging.Tests;

public sealed class RedactionTests
{
    [Fact]
    public void Nested_values_are_redacted_before_they_are_escaped_into_message_text()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.Redact(@"secret\s+line").WriteTo.Sink(sink));
        logger.Info("{Value}", new { Token = "secret\nline" });
        var e = Assert.Single(sink.Events);
        Assert.DoesNotContain("secret", e.Message);
        Assert.Equal("***", Assert.IsType<JsonElement>(e.Properties["Value"]).GetProperty("Token").GetString());
    }

    [Fact]
    public void All_sinks_and_filters_receive_only_redacted_events()
    {
        var first = new MemorySink();
        var second = new MemorySink();
        var writer = new StringWriter();
        var seen = new List<LogEvent>();
        using var logger = Logger.Create(c => c
            .Enrich.WithProperty("Token", "secret-global")
            .Redact("secret-[a-z]+", "[hidden]")
            .Filter(e => { seen.Add(e); return true; })
            .WriteTo.Sink(first)
            .WriteTo.Sink(second)
            .WriteTo.Console(o => o.Writer(writer).Json()));
        using var scope = logger.PushScope("Scope", "secret-scope");
        using var bound = logger.Bind("Bound", "secret-bound");
        bound.Error(new InvalidOperationException("secret-exception"), "secret-literal {Payload}",
            new { Password = "secret-nested", Values = new[] { "secret-array", "safe" } });
        var e = Assert.Single(first.Events);
        Assert.Same(e, Assert.Single(second.Events));
        Assert.Same(e, Assert.Single(seen));
        Assert.Equal("[hidden]", e.Properties["Token"]);
        Assert.Equal("[hidden]", e.Properties["Bound"]);
        Assert.Equal("[hidden]", e.Properties["Scope"]);
        Assert.Equal("[hidden]", e.Exception!.Message);
        Assert.DoesNotContain("secret-", e.MessageTemplate);
        Assert.DoesNotContain("secret-", e.Message);
        using var json = JsonDocument.Parse(writer.ToString());
        Assert.Equal("[hidden]", json.RootElement.GetProperty("properties").GetProperty("Payload").GetProperty("Password").GetString());
        Assert.Equal("[hidden]", json.RootElement.GetProperty("properties").GetProperty("Payload").GetProperty("Values")[0].GetString());
        Assert.DoesNotContain("secret-", writer.ToString());
    }

    [Fact]
    public void Replacements_with_quotes_and_newlines_preserve_valid_json()
    {
        var writer = new StringWriter();
        using var logger = Logger.Create(c => c.Redact("token", "\"hidden\"\nnext").WriteTo.Console(o => o.Writer(writer).Json()));
        logger.Info("{Value}", new { Value = "token" });
        using var json = JsonDocument.Parse(writer.ToString());
        Assert.Equal("\"hidden\"\nnext", json.RootElement.GetProperty("properties").GetProperty("Value").GetProperty("Value").GetString());
        Assert.Single(writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Regex_timeouts_drop_the_whole_event_and_report_a_failure()
    {
        var sink = new MemorySink();
        var failures = new List<LoggingFailure>();
        using var logger = Logger.Create(c => c.OnError(failures.Add)
            .Redact("(a+)+$", timeout: TimeSpan.FromMilliseconds(1)).WriteTo.Sink(sink));
        logger.Info("{Secret}", new string('a', 50_000) + "!");
        Assert.Empty(sink.Events);
        Assert.IsType<RegexMatchTimeoutException>(Assert.Single(failures).Exception);
    }

    [Fact]
    public void Redaction_rules_run_in_registration_order_and_support_groups()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.Redact("Bearer (.+)", "token=$1").Redact("secret", "***").WriteTo.Sink(sink));
        logger.Info("{Authorization}", "Bearer secret");
        Assert.Equal("token=***", Assert.Single(sink.Events).Properties["Authorization"]);
    }
}
