using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Trexon.Logging.Internal;

internal sealed class LoggerCore : IDisposable
{
    private readonly object _gate = new();
    private readonly LogLevel _level;
    private readonly bool _captureCaller;
    private readonly Dictionary<string, object?> _properties;
    private readonly RedactionRule[] _redactions;
    private readonly Func<LogEvent, bool>[] _filters;
    private readonly SinkRegistration[] _sinks;
    private readonly TimeProvider _clock;
    private readonly Action<LoggingFailure>? _errorHandler;
    private readonly AsyncLocal<ScopeNode?> _scope = new();
    private volatile bool _disposed;
    private bool _writing;

    internal LoggerCore(LoggerConfiguration configuration)
    {
        _level = configuration.Level;
        _captureCaller = configuration.CaptureCaller;
        _properties = configuration.Properties.ToDictionary(p => p.Key, p => PropertyValues.Capture(p.Value), StringComparer.Ordinal);
        _redactions = configuration.Redactions.ToArray();
        _filters = configuration.Filters.ToArray();
        _clock = configuration.Clock;
        _errorHandler = configuration.ErrorHandler;
        var registrations = new List<SinkRegistration>();
        try
        {
            foreach (var factory in configuration.Sinks) registrations.Add(factory());
            _sinks = registrations.ToArray();
        }
        catch
        {
            foreach (var sink in registrations.Where(s => s.Owned).Select(s => s.Sink).Distinct(ReferenceEqualityComparer.Instance))
            {
                try { ((ILogSink)sink!).Dispose(); } catch { /* Preserve the construction failure. */ }
            }
            throw;
        }
    }

    internal bool IsEnabled(LogLevel level) => !_disposed && level != LogLevel.None && level >= _level
        && _sinks.Any(s => level >= s.Level && s.Level != LogLevel.None);

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, typeof(Logger));

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Write(LogLevel level, string template, object?[] values, Exception? exception,
        IReadOnlyDictionary<string, object?> bound)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Logger));
            if (!IsEnabled(level) || _writing) return;
            _writing = true;
            try
            {
                LogEvent logEvent;
                try
                {
                    var properties = new Dictionary<string, object?>(_properties, StringComparer.Ordinal);
                    var scopes = new Stack<ScopeNode>();
                    for (var scope = _scope.Value; scope is not null; scope = scope.Parent) scopes.Push(scope);
                    foreach (var scope in scopes)
                        foreach (var property in scope.Properties) properties[property.Key] = property.Value;
                    foreach (var property in bound) properties[property.Key] = property.Value;
                    foreach (var property in MessageTemplate.Bind(template, values)) properties[property.Key] = property.Value;
                    foreach (var key in properties.Keys.ToArray()) properties[key] = PropertyValues.Redact(properties[key], _redactions);
                    var message = PropertyValues.Apply(MessageTemplate.Render(template, properties), _redactions);
                    logEvent = new LogEvent(_clock.GetUtcNow(), level, PropertyValues.Apply(template, _redactions), message,
                        new ReadOnlyDictionary<string, object?>(properties),
                        exception is null ? null : CaptureException(exception, 0), _captureCaller ? CaptureCaller() : null);
                    if (_filters.Any(filter => !filter(logEvent))) return;
                }
                catch (Exception error) { Report("Pipeline", error); return; }

                foreach (var registration in _sinks)
                {
                    if (level < registration.Level || registration.Level == LogLevel.None) continue;
                    try
                    {
                        if (registration.Filter?.Invoke(logEvent) != false) registration.Sink.Emit(logEvent);
                    }
                    catch (Exception error) { Report(registration.Sink.GetType().Name, error); }
                }
            }
            finally { _writing = false; }
        }
    }

    internal IDisposable PushScope(IReadOnlyDictionary<string, object?> properties)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Logger));
            var node = new ScopeNode(properties.ToDictionary(p => p.Key, p => PropertyValues.Capture(p.Value), StringComparer.Ordinal), _scope.Value);
            _scope.Value = node;
            return new ScopeLease(_scope, node);
        }
    }

    internal void Flush()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Logger));
            if (_writing) return;
            _writing = true;
            try { FlushSinks(); }
            finally { _writing = false; }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_writing) throw new InvalidOperationException("A logger cannot be disposed from its own sink or error callback.");
            _disposed = true;
            _writing = true;
            try
            {
                FlushSinks();
                var disposed = new HashSet<ILogSink>(ReferenceEqualityComparer.Instance);
                foreach (var registration in _sinks)
                {
                    if (!registration.Owned || !disposed.Add(registration.Sink)) continue;
                    try { registration.Sink.Dispose(); }
                    catch (Exception error) { Report("Dispose:" + registration.Sink.GetType().Name, error); }
                }
            }
            finally { _writing = false; }
        }
    }

    private void FlushSinks()
    {
        var flushed = new HashSet<ILogSink>(ReferenceEqualityComparer.Instance);
        foreach (var registration in _sinks)
        {
            if (!flushed.Add(registration.Sink)) continue;
            try { registration.Sink.Flush(); }
            catch (Exception error) { Report("Flush:" + registration.Sink.GetType().Name, error); }
        }
    }

    private void Report(string stage, Exception exception)
    {
        try
        {
            if (_errorHandler is not null) _errorHandler(new LoggingFailure(stage, exception));
            else Console.Error.WriteLine($"Trexon.Logging: {stage} failed ({exception.GetType().Name}).");
        }
        catch { /* Logging failures must not crash application operations. */ }
    }

    private ExceptionInfo CaptureException(Exception exception, int depth)
    {
        var children = depth >= 16 ? [] : exception is AggregateException aggregate
            ? aggregate.InnerExceptions.ToArray() : exception.InnerException is { } inner ? new[] { inner } : [];
        return new ExceptionInfo(PropertyValues.Apply(exception.GetType().FullName ?? exception.GetType().Name, _redactions),
            PropertyValues.Apply(exception.Message, _redactions),
            exception.StackTrace is null ? null : PropertyValues.Apply(exception.StackTrace, _redactions),
            Array.AsReadOnly(children.Select(child => CaptureException(child, depth + 1)).ToArray()));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private CallerInfo? CaptureCaller()
    {
        var frames = new StackTrace(true).GetFrames();
        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            if (method is null || method.DeclaringType?.Assembly == typeof(Logger).Assembly) continue;
            return new CallerInfo(PropertyValues.Apply(method.Name, _redactions),
                frame.GetFileName() is { } file ? PropertyValues.Apply(file, _redactions) : null, frame.GetFileLineNumber());
        }
        return null;
    }

    private sealed record ScopeNode(IReadOnlyDictionary<string, object?> Properties, ScopeNode? Parent);

    private sealed class ScopeLease(AsyncLocal<ScopeNode?> current, ScopeNode node) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(current.Value, node)) { current.Value = node.Parent; return; }
            for (var ancestor = current.Value; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, node))
                    throw new InvalidOperationException("Logging scopes must be disposed in reverse creation order.");
        }
    }
}
