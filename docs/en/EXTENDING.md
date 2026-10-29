# Custom ILogSink implementations

[Русский](../EXTENDING.md) · [English](../en/EXTENDING.md) · [Deutsch](../de/EXTENDING.md)

[Index](README.md) · [Previous](ERRORS_AND_LIFETIME.md) · [API](API.md)

`ILogSink` is the public extension point for custom destinations and formats. It
receives a prepared, redacted `LogEvent`. Built-in formatters are internal; the MVP
has no public `ILogFormatter` or arbitrary enricher callback.

## Minimal sink

A complete program that retains events in memory:

<!-- verify: en-custom-sink -->
```csharp
using Trexon.Logging;

using var sink = new CollectingSink();
using (var logger = Logger.Create(config => config
    .Redact("demo-secret")
    .WriteTo.Sink(sink, minimumLevel: LogLevel.Warning,
        filter: e => !e.Properties.ContainsKey("Skip"))))
{
    logger.Info("Below sink threshold");
    logger.Warning("Order {OrderId}, token {Token}", 42, "demo-secret");
}

foreach (var entry in sink.Snapshot())
{
    Console.WriteLine($"{entry.Level}: {entry.Message}");
    Console.WriteLine($"OrderId = {entry.Properties["OrderId"]}");
}

sealed class CollectingSink : ILogSink
{
    private readonly object _gate = new();
    private readonly List<LogEvent> _events = [];
    private bool _disposed;

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _events.Add(logEvent);
        }
    }

    public LogEvent[] Snapshot()
    {
        lock (_gate) return _events.ToArray();
    }

    public void Flush() { }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
    }
}
```

The sink receives one Warning event with Token `***`. The logger does not own the
sink by default; the outer `using var sink` closes it. Only this example stores
history: the library does not retain a list of events itself.

## Ownership

Inside a configuration callback:

```csharp
config.WriteTo.Sink(
    sink,
    minimumLevel: LogLevel.Trace,
    filter: null,
    disposeWithLogger: true);
```

With `true`, the root flushes then disposes the sink. With `false`, it still flushes
but the caller disposes it. Choose one owner and a shutdown order. A sink shared
between roots can receive concurrent Emit calls, so the example uses its own lock.
Resource ownership across roots is not coordinated: do not dispose a shared sink
while another root still uses it.

## Event data

LogEvent exposes Timestamp, Level, MessageTemplate, Message, Properties, Exception,
and Caller. Properties are read-only, complex values are independent JsonElement
snapshots, and later input mutations do not affect retained events. Exception is
an ExceptionInfo snapshot, not the original Exception reference. Caller may be
absent; FilePath can be null even with capture enabled.

Built-in `ExceptionDetails(false)` and `Caller(false)` are formatter settings.
A custom sink decides what to write from the captured, redacted event itself.

## Failures and cost

Emit runs synchronously under the root lock. Its errors go to OnError while other
sinks continue. There is no built-in retry, redelivery, or rollback of partially
written external data. Flush should drain this sink's buffers; Dispose releases
its resources. Both run under the root lock with isolated failures.

Do not call the same logger or its lifecycle methods from a sink: recursive writes
are skipped and root Dispose during dispatch is forbidden. A network sink must
define its own timeouts, queue, memory limits, and outage behavior; implementing
ILogSink alone does not supply those mechanisms.
