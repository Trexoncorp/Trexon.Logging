using System.Collections.ObjectModel;
using Trexon.Logging.Internal;

namespace Trexon.Logging;

/// <summary>A synchronous structured logger with fluent configuration and explicit resource ownership.</summary>
/// <remarks>Writes, flushes and shutdown are serialized. Dispose the root logger after application work completes.</remarks>
public sealed class Logger : IDisposable
{
    private readonly LoggerCore _core;
    private readonly IReadOnlyDictionary<string, object?> _bound;
    private readonly bool _ownsCore;
    private int _disposed;

    private Logger(LoggerCore core, IReadOnlyDictionary<string, object?> bound, bool ownsCore)
        => (_core, _bound, _ownsCore) = (core, bound, ownsCore);

    /// <summary>Creates a logger. Console.Error is used when no sink is configured.</summary>
    public static Logger Create(Action<LoggerConfiguration>? configure = null)
    {
        var configuration = new LoggerConfiguration();
        configure?.Invoke(configuration);
        if (configuration.Sinks.Count == 0) configuration.WriteTo.Console();
        return new Logger(new LoggerCore(configuration), new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>()), true);
    }

    /// <summary>Checks severity thresholds without creating or formatting an event.</summary>
    public bool IsEnabled(LogLevel level)
    {
        LoggerConfiguration.ValidateLevel(level);
        return Volatile.Read(ref _disposed) == 0 && _core.IsEnabled(level);
    }

    /// <summary>Creates a contextual view sharing the root's sinks. Disposing the view never closes the root.</summary>
    public Logger Bind(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Bind(new Dictionary<string, object?> { [name] = value });
    }

    /// <summary>Creates a contextual view with captured properties. Message properties take precedence.</summary>
    public Logger Bind(IReadOnlyDictionary<string, object?> properties)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(properties);
        var bound = new Dictionary<string, object?>(_bound, StringComparer.Ordinal);
        foreach (var property in properties)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(property.Key);
            bound[property.Key] = PropertyValues.Capture(property.Value);
        }
        return new Logger(_core, new ReadOnlyDictionary<string, object?>(bound), false);
    }

    /// <summary>Adds async-flow-local context until the returned scope is disposed.</summary>
    /// <remarks>Scopes are shared by bound views and must be disposed in reverse order within each async flow.</remarks>
    public IDisposable PushScope(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return PushScope(new Dictionary<string, object?> { [name] = value });
    }

    /// <summary>Adds captured properties to the current async flow.</summary>
    public IDisposable PushScope(IReadOnlyDictionary<string, object?> properties)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(properties);
        foreach (var name in properties.Keys) ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _core.PushScope(properties);
    }

    /// <summary>Writes an event with named message properties.</summary>
    public void Write(LogLevel level, string messageTemplate, params object?[] values)
        => WriteCore(level, null, messageTemplate, values);

    /// <summary>Writes an event with captured exception details and named properties.</summary>
    public void Write(LogLevel level, Exception exception, string messageTemplate, params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(exception);
        WriteCore(level, exception, messageTemplate, values);
    }

    /// <summary>Writes a Trace event.</summary>
    public void Trace(string messageTemplate, params object?[] values) => WriteCore(LogLevel.Trace, null, messageTemplate, values);
    /// <summary>Writes a Debug event.</summary>
    public void Debug(string messageTemplate, params object?[] values) => WriteCore(LogLevel.Debug, null, messageTemplate, values);
    /// <summary>Writes an Information event.</summary>
    public void Info(string messageTemplate, params object?[] values) => WriteCore(LogLevel.Information, null, messageTemplate, values);
    /// <summary>Writes a Warning event.</summary>
    public void Warning(string messageTemplate, params object?[] values) => WriteCore(LogLevel.Warning, null, messageTemplate, values);
    /// <summary>Writes an Error event.</summary>
    public void Error(string messageTemplate, params object?[] values) => WriteCore(LogLevel.Error, null, messageTemplate, values);
    /// <summary>Writes an Error event with exception details.</summary>
    public void Error(Exception exception, string messageTemplate, params object?[] values) => Write(LogLevel.Error, exception, messageTemplate, values);
    /// <summary>Writes a Critical event.</summary>
    public void Critical(string messageTemplate, params object?[] values) => WriteCore(LogLevel.Critical, null, messageTemplate, values);
    /// <summary>Writes a Critical event with exception details.</summary>
    public void Critical(Exception exception, string messageTemplate, params object?[] values) => Write(LogLevel.Critical, exception, messageTemplate, values);

    /// <summary>Flushes every sink without closing it.</summary>
    public void Flush() { EnsureAvailable(); _core.Flush(); }

    /// <summary>Flushes and closes owned sinks, or releases this bound view. Safe to call repeatedly.</summary>
    public void Dispose()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        if (_ownsCore) _core.Dispose();
        Interlocked.Exchange(ref _disposed, 1);
    }

    private void WriteCore(LogLevel level, Exception? exception, string template, object?[] values)
    {
        EnsureAvailable();
        LoggerConfiguration.ValidateLevel(level);
        ArgumentNullException.ThrowIfNull(template);
        // A null params array is interpreted as a single null property value.
        _core.Write(level, template, values ?? [null], exception, _bound);
    }

    private void EnsureAvailable()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _core.ThrowIfDisposed();
    }
}
