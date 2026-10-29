namespace Trexon.Logging;

/// <summary>Defines the severity of a log event. None disables logging.</summary>
public enum LogLevel
{
    /// <summary>Detailed diagnostic information.</summary>
    Trace,
    /// <summary>Development diagnostics.</summary>
    Debug,
    /// <summary>Normal application activity.</summary>
    Information,
    /// <summary>An unexpected but recoverable condition.</summary>
    Warning,
    /// <summary>An operation failed.</summary>
    Error,
    /// <summary>A critical application failure.</summary>
    Critical,
    /// <summary>Disables all events.</summary>
    None
}
