namespace Trexon.Logging;

/// <summary>A captured, immutable event delivered to sinks after redaction.</summary>
/// <remarks>Complex property values are captured as immutable JSON elements.</remarks>
public sealed class LogEvent
{
    internal LogEvent(DateTimeOffset timestamp, LogLevel level, string template, string message,
        IReadOnlyDictionary<string, object?> properties, ExceptionInfo? exception, CallerInfo? caller)
    {
        Timestamp = timestamp;
        Level = level;
        MessageTemplate = template;
        Message = message;
        Properties = properties;
        Exception = exception;
        Caller = caller;
    }

    /// <summary>The UTC timestamp captured when the event was written.</summary>
    public DateTimeOffset Timestamp { get; }
    /// <summary>The event severity.</summary>
    public LogLevel Level { get; }
    /// <summary>The message template, with redaction applied.</summary>
    public string MessageTemplate { get; }
    /// <summary>The rendered message, with redaction applied.</summary>
    public string Message { get; }
    /// <summary>Captured structured properties, with redaction applied.</summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }
    /// <summary>Captured exception details, with redaction applied.</summary>
    public ExceptionInfo? Exception { get; }
    /// <summary>The calling method and source location, when enabled and available.</summary>
    public CallerInfo? Caller { get; }
}

/// <summary>A source location captured for a log event.</summary>
/// <param name="MemberName">The calling method.</param>
/// <param name="FilePath">The source file path, when debug symbols are available.</param>
/// <param name="LineNumber">The source line, or zero when unavailable.</param>
public sealed record CallerInfo(string MemberName, string? FilePath, int LineNumber);

/// <summary>An immutable snapshot of an exception and its inner exceptions.</summary>
public sealed class ExceptionInfo
{
    internal ExceptionInfo(string type, string message, string? stackTrace, IReadOnlyList<ExceptionInfo> innerExceptions)
    {
        Type = type;
        Message = message;
        StackTrace = stackTrace;
        InnerExceptions = innerExceptions;
    }

    /// <summary>The exception's fully qualified type name.</summary>
    public string Type { get; }
    /// <summary>The exception message.</summary>
    public string Message { get; }
    /// <summary>The original stack trace, when the exception has been thrown.</summary>
    public string? StackTrace { get; }
    /// <summary>Inner exceptions, including all children of an AggregateException.</summary>
    public IReadOnlyList<ExceptionInfo> InnerExceptions { get; }
}
