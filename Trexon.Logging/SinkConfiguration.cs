using System.Globalization;
using Trexon.Logging.Internal;

namespace Trexon.Logging;

/// <summary>Common formatting and filtering options for built-in output sinks.</summary>
/// <typeparam name="TSelf">The concrete configuration type, preserving fluent chaining.</typeparam>
public abstract class SinkConfiguration<TSelf> where TSelf : SinkConfiguration<TSelf>
{
    private LogLevel _level = LogLevel.Trace;
    private string? _timestamp = "yyyy-MM-dd HH:mm:ss.fff zzz";
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;
    private bool _json;
    private bool _exceptionDetails = true;
    private bool _caller = true;
    private Func<LogEvent, bool>? _filter;
    private TSelf Self => (TSelf)this;

    /// <summary>Sets this sink's minimum severity in addition to the logger threshold.</summary>
    public TSelf MinimumLevel(LogLevel level)
    {
        LoggerConfiguration.ValidateLevel(level);
        _level = level;
        return Self;
    }

    /// <summary>Sets the text timestamp format; null omits it. JSON always uses an ISO timestamp.</summary>
    public TSelf Timestamp(string? format)
    {
        if (format is not null)
            _ = DateTimeOffset.UnixEpoch.ToString(format, CultureInfo.InvariantCulture);
        _timestamp = format;
        return Self;
    }

    /// <summary>Sets the time zone for text output. The event itself keeps its UTC timestamp.</summary>
    public TSelf TimeZone(TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        _timeZone = timeZone;
        return Self;
    }

    /// <summary>Writes one JSON object per line instead of text.</summary>
    public TSelf Json(bool enabled = true) { _json = enabled; return Self; }

    /// <summary>Includes stack traces and inner exceptions. When false, writes only type and message.</summary>
    public TSelf ExceptionDetails(bool enabled = true) { _exceptionDetails = enabled; return Self; }

    /// <summary>Includes captured caller information. Enable capture with Enrich.WithCaller().</summary>
    public TSelf Caller(bool enabled = true) { _caller = enabled; return Self; }

    /// <summary>Adds a sink-specific predicate evaluated against the redacted event.</summary>
    public TSelf Filter(Func<LogEvent, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _filter = _filter is null ? predicate : Combine(_filter, predicate);
        return Self;
    }

    private static Func<LogEvent, bool> Combine(Func<LogEvent, bool> first, Func<LogEvent, bool> second)
        => e => first(e) && second(e);

    internal OutputOptions Snapshot() => new(_level, _timestamp, _timeZone, _json, _exceptionDetails, _caller, _filter);
}

/// <summary>Console output options.</summary>
public sealed class ConsoleSinkConfiguration : SinkConfiguration<ConsoleSinkConfiguration>
{
    internal TextWriter Output = System.Console.Error;
    internal ConsoleColorMode ColorMode = ConsoleColorMode.Auto;

    /// <summary>Sets ANSI color behavior. JSON output always remains plain JSON.</summary>
    public ConsoleSinkConfiguration Colors(ConsoleColorMode mode = ConsoleColorMode.Auto)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        ColorMode = mode;
        return this;
    }

    /// <summary>Redirects console output to a caller-owned writer, useful for hosting and testing.</summary>
    public ConsoleSinkConfiguration Writer(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        Output = writer;
        return this;
    }
}

/// <summary>Formatting and filtering options for append-only file output.</summary>
public sealed class FileSinkConfiguration : SinkConfiguration<FileSinkConfiguration>;
