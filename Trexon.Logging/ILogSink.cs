namespace Trexon.Logging;

/// <summary>Receives structured events. Calls from a single logger are serialized.</summary>
public interface ILogSink : IDisposable
{
    /// <summary>Writes a captured and redacted event.</summary>
    void Emit(LogEvent logEvent);
    /// <summary>Flushes buffered output.</summary>
    void Flush();
}

/// <summary>A failure reported by the logging pipeline.</summary>
/// <param name="Stage">The failed operation or sink type.</param>
/// <param name="Exception">The underlying failure.</param>
public sealed record LoggingFailure(string Stage, Exception Exception);

/// <summary>Controls ANSI colors in console output.</summary>
public enum ConsoleColorMode
{
    /// <summary>Use colors only for an interactive standard output/error stream.</summary>
    Auto,
    /// <summary>Always write ANSI color sequences.</summary>
    Always,
    /// <summary>Write plain text.</summary>
    Never
}
