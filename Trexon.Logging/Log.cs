namespace Trexon.Logging;

/// <summary>A process-wide convenience facade, configured with Console.Error by default.</summary>
/// <remarks>Configure at application startup. Reconfiguration disposes the previous root and invalidates its bound views.</remarks>
public static class Log
{
    private static readonly object Gate = new();
    private static Logger _logger = Logger.Create();

    /// <summary>Atomically replaces the default logger and disposes its previous sinks.</summary>
    public static void Configure(Action<LoggerConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var replacement = Logger.Create(configure);
        lock (Gate)
        {
            var previous = _logger;
            try { previous.Dispose(); }
            catch { replacement.Dispose(); throw; }
            _logger = replacement;
        }
    }

    /// <summary>Checks severity thresholds on the current default logger.</summary>
    public static bool IsEnabled(LogLevel level) { lock (Gate) return _logger.IsEnabled(level); }
    /// <summary>Creates a bound view of the current default logger.</summary>
    public static Logger Bind(string name, object? value) { lock (Gate) return _logger.Bind(name, value); }
    /// <summary>Adds context to the current default logger for this async flow.</summary>
    public static IDisposable PushScope(string name, object? value) { lock (Gate) return _logger.PushScope(name, value); }
    /// <summary>Writes an event to the default logger.</summary>
    public static void Write(LogLevel level, string template, params object?[] values) { lock (Gate) _logger.Write(level, template, values); }
    /// <summary>Writes an event and exception to the default logger.</summary>
    public static void Write(LogLevel level, Exception exception, string template, params object?[] values) { lock (Gate) _logger.Write(level, exception, template, values); }
    /// <summary>Writes a Trace event.</summary>
    public static void Trace(string template, params object?[] values) => Write(LogLevel.Trace, template, values);
    /// <summary>Writes a Debug event.</summary>
    public static void Debug(string template, params object?[] values) => Write(LogLevel.Debug, template, values);
    /// <summary>Writes an Information event.</summary>
    public static void Info(string template, params object?[] values) => Write(LogLevel.Information, template, values);
    /// <summary>Writes a Warning event.</summary>
    public static void Warning(string template, params object?[] values) => Write(LogLevel.Warning, template, values);
    /// <summary>Writes an Error event.</summary>
    public static void Error(string template, params object?[] values) => Write(LogLevel.Error, template, values);
    /// <summary>Writes an Error event and exception.</summary>
    public static void Error(Exception exception, string template, params object?[] values) => Write(LogLevel.Error, exception, template, values);
    /// <summary>Writes a Critical event.</summary>
    public static void Critical(string template, params object?[] values) => Write(LogLevel.Critical, template, values);
    /// <summary>Writes a Critical event and exception.</summary>
    public static void Critical(Exception exception, string template, params object?[] values) => Write(LogLevel.Critical, exception, template, values);
    /// <summary>Flushes all default sinks.</summary>
    public static void Flush() { lock (Gate) _logger.Flush(); }
    /// <summary>Flushes and disposes the default logger. Configure can install a new logger afterwards.</summary>
    public static void Shutdown() { lock (Gate) _logger.Dispose(); }
}
