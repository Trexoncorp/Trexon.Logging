using System.Text.RegularExpressions;
using Trexon.Logging.Internal;

namespace Trexon.Logging;

/// <summary>Builds a logger configuration. Created loggers use independent snapshots.</summary>
public sealed class LoggerConfiguration
{
    internal LogLevel Level = LogLevel.Information;
    internal bool CaptureCaller;
    internal readonly Dictionary<string, object?> Properties = new(StringComparer.Ordinal);
    internal readonly List<RedactionRule> Redactions = [];
    internal readonly List<Func<LogEvent, bool>> Filters = [];
    internal readonly List<Func<SinkRegistration>> Sinks = [];
    internal Action<LoggingFailure>? ErrorHandler;
    internal TimeProvider Clock = TimeProvider.System;

    /// <summary>Creates an empty configuration; add output with WriteTo.</summary>
    public LoggerConfiguration()
    {
        Enrich = new LoggerEnrichmentConfiguration(this);
        WriteTo = new LoggerSinkConfiguration(this);
    }

    /// <summary>Configures event context and caller capture.</summary>
    public LoggerEnrichmentConfiguration Enrich { get; }
    /// <summary>Configures console, file and custom sinks.</summary>
    public LoggerSinkConfiguration WriteTo { get; }

    /// <summary>Sets the minimum severity. Defaults to Information.</summary>
    public LoggerConfiguration MinimumLevel(LogLevel level)
    {
        ValidateLevel(level);
        Level = level;
        return this;
    }

    /// <summary>Adds a predicate evaluated after redaction and before sink dispatch.</summary>
    public LoggerConfiguration Filter(Func<LogEvent, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        Filters.Add(predicate);
        return this;
    }

    /// <summary>Redacts matching text in messages, property values, caller and exception details.</summary>
    /// <remarks>Rules run in registration order. A timeout drops the event and reports a pipeline failure.</remarks>
    public LoggerConfiguration Redact(string pattern, string replacement = "***",
        RegexOptions options = RegexOptions.None, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(replacement);
        if (timeout is { } duration && duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "A finite positive regex timeout is required.");
        Redactions.Add(new RedactionRule(new Regex(pattern, options, timeout ?? TimeSpan.FromMilliseconds(100)), replacement));
        return this;
    }

    /// <summary>Handles pipeline, sink, flush and disposal failures. Handler exceptions are suppressed.</summary>
    public LoggerConfiguration OnError(Action<LoggingFailure> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ErrorHandler = handler;
        return this;
    }

    /// <summary>Sets the timestamp source, for example for deterministic testing.</summary>
    public LoggerConfiguration WithTimeProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        Clock = timeProvider;
        return this;
    }

    internal static void ValidateLevel(LogLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level));
    }
}

/// <summary>Fluent context configuration.</summary>
public sealed class LoggerEnrichmentConfiguration
{
    private readonly LoggerConfiguration _owner;
    internal LoggerEnrichmentConfiguration(LoggerConfiguration owner) => _owner = owner;

    /// <summary>Captures the caller via stack inspection only for enabled events.</summary>
    /// <remarks>Source locations require PDBs; optimized/inlined callers may be represented by an outer frame.</remarks>
    public LoggerConfiguration WithCaller(bool enabled = true)
    {
        _owner.CaptureCaller = enabled;
        return _owner;
    }

    /// <summary>Adds a property to every event. Event properties override context properties.</summary>
    public LoggerConfiguration WithProperty(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _owner.Properties[name] = value;
        return _owner;
    }
}

/// <summary>Fluent sink configuration.</summary>
public sealed class LoggerSinkConfiguration
{
    private readonly LoggerConfiguration _owner;
    internal LoggerSinkConfiguration(LoggerConfiguration owner) => _owner = owner;

    /// <summary>Adds a console sink. Defaults to Console.Error, which is never disposed.</summary>
    public LoggerConfiguration Console(Action<ConsoleSinkConfiguration>? configure = null)
    {
        var options = new ConsoleSinkConfiguration();
        configure?.Invoke(options);
        var snapshot = options.Snapshot();
        var writer = options.Output;
        var colors = options.ColorMode;
        _owner.Sinks.Add(() => new SinkRegistration(new TextOutputSink(writer, snapshot, colors, false),
            snapshot.Level, snapshot.Predicate, true));
        return _owner;
    }

    /// <summary>Adds a UTF-8 append-only file sink. The logger owns its file stream.</summary>
    public LoggerConfiguration File(string path, Action<FileSinkConfiguration>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var options = new FileSinkConfiguration();
        configure?.Invoke(options);
        var snapshot = options.Snapshot();
        _owner.Sinks.Add(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            return new SinkRegistration(new TextOutputSink(writer, snapshot, ConsoleColorMode.Never, true),
                snapshot.Level, snapshot.Predicate, true);
        });
        return _owner;
    }

    /// <summary>Adds a structured sink. Caller-owned sinks are flushed but not disposed by default.</summary>
    public LoggerConfiguration Sink(ILogSink sink, LogLevel minimumLevel = LogLevel.Trace,
        Func<LogEvent, bool>? filter = null, bool disposeWithLogger = false)
    {
        ArgumentNullException.ThrowIfNull(sink);
        LoggerConfiguration.ValidateLevel(minimumLevel);
        _owner.Sinks.Add(() => new SinkRegistration(sink, minimumLevel, filter, disposeWithLogger));
        return _owner;
    }
}
