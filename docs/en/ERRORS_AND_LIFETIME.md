# Exceptions, logging failures, and lifetime

[Русский](../ERRORS_AND_LIFETIME.md) · [English](../en/ERRORS_AND_LIFETIME.md) · [Deutsch](../de/ERRORS_AND_LIFETIME.md)

[Index](README.md) · [Previous](REDACTION.md) · [Next](EXTENDING.md)

## Application exceptions

Pass an exception separately. `Error` and `Critical` have dedicated overloads;
`Write` can choose any standard level:

<!-- verify: en-exceptions -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Timestamp(null).ExceptionDetails()));
try
{
    throw new InvalidOperationException("Import failed",
        new ArgumentException("Invalid customer"));
}
catch (Exception exception)
{
    logger.Error(exception, "Ошибка импорта {BatchId}", 42);
    logger.Write(LogLevel.Warning, exception, "Попытка {Attempt} завершилась ошибкой", 1);
}
```

`Error("Failure {Exception}", exception)` treats it as an ordinary property. Use
the overload beginning with `Exception` for an exception snapshot. The logger does
not install global handlers or catch business-code exceptions. Applications decide
whether to catch or rethrow. Logging reads the existing stack trace; it does not
create a new throw stack.

## ExceptionInfo snapshot

The event holds a snapshot rather than the original Exception reference:

| Field | Contents |
| --- | --- |
| Type | Fully qualified exception type |
| Message | Exception message |
| StackTrace | Original stack or null |
| InnerExceptions | Child snapshots |

All immediate AggregateException children are captured; other exceptions capture
InnerException when present. Recursion stops after 16 transitions from the root.
Exception.Data and arbitrary extra exception properties are excluded.
`ExceptionDetails(false)` hides stack/children in that output, but an
AggregateException.Message can itself contain child messages. Other sinks retain
the full event snapshot.

## Internal logging failures

`OnError` reports technical failures:

<!-- verify: en-error-handler -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .OnError(failure => Console.Error.WriteLine(
        $"Logging failure: {failure.Stage}, {failure.Exception.GetType().Name}"))
    .WriteTo.Console(console => console.Timestamp(null)));

logger.Info("Invalid format {Price:Q}", 12.5m); // Pipeline failure; the event is dropped.
logger.Info("Следующая запись работает");
```

| Failure location | Behavior |
| --- | --- |
| Invalid API arguments or configuration | Throw to caller |
| File opening, logger creation, global-property capture | Throw from Create |
| Property capture in Bind / PushScope | Throw from that method |
| Event capture, regex, argument format, global filter | OnError; drop whole event |
| Sink filter or Emit | OnError; other sinks continue |
| Sink Flush / Dispose | OnError; other resources continue |
| OnError handler itself | Exception suppressed |
| Closed logger/view | ObjectDisposedException |

Creation errors are not ordinary event failures. If sink initialization fails,
already created owned sinks close and the original error propagates.
`LoggingFailure.Stage` is `Pipeline`, the sink type name, `Flush:<type>`, or
`Dispose:<type>`. Built-ins use `TextOutputSink`, so stage does not identify file
paths. Without a handler, Console.Error gets a short stage/type diagnostic, without
the technical exception message. `LoggingFailure.Exception` is not redacted.

## Resource ownership

| Resource | Who disposes it |
| --- | --- |
| Root from Create | Application using / Dispose |
| File sink and stream | Root logger |
| Console.Out / Console.Error | Never disposed by logger |
| Writer supplied to Console | Caller |
| Custom sink, default | Caller |
| Custom sink with disposeWithLogger: true | Root logger |
| Bound view | May close independently; root remains open |
| Scope | Reverse using order in the same async flow |

Caller-owned sinks are flushed, not disposed. Re-registering one sink gives it
multiple Emit calls, but each distinct instance is flushed/disposed once per operation.

## Flush and Dispose

A sink may buffer entries in memory before writing them together. `Flush()` asks
all sinks to pass their buffered data onward. It creates no event and leaves the
logger open, so subsequent writes are allowed.

The built-in file writer uses `StreamWriter.AutoFlush`: writes already leave its
writer buffer automatically. The sample's explicit Flush demonstrates the API;
normally `using` is enough because root Dispose flushes and closes owned resources.
Neither AutoFlush nor Flush separately guarantees physical persistence during power loss.

Root Dispose marks the core closed, flushes, and disposes owned sinks. Repeated
Dispose is safe. Writing, Flush, Bind, and PushScope after closure throw
ObjectDisposedException; IsEnabled returns false. Disposing a bound view affects
only that view; disposing the root invalidates all views. There is no reference
count extending root lifetime. The pipeline has no FlushAsync, DisposeAsync, or
background-queue guarantees.

## Concurrency and callbacks

One root lock serializes writes, flush, and closure. Sinks see the same event order,
but task scheduling order does not guarantee lock acquisition order. Slow sinks,
large snapshots, regex rules, and filters delay the caller. Keep callbacks short.
Nested writes to the same root during dispatch/flush are skipped; nested Flush is
a no-op. Root Dispose from its own callback throws InvalidOperationException and,
inside a sink, becomes a sink failure. Avoid cycles between callbacks of independent
roots, which have separate locks.

## Process-wide Log

Configure creates a replacement first, then under its lock disposes the previous
root and swaps it. A replacement creation failure leaves the prior logger working.
After successful replacement old views are invalid. Reopening the same file can
fail while the old writer is still open. Configure once at startup; if necessary,
Shutdown the old root before creating a new one after its users have finished.
Shutdown closes the current root; writes then throw. Configure can install a new
root after Shutdown, but existing views never switch to it automatically.
