# Fluent configuration

[Русский](../CONFIGURATION.md) · [English](../en/CONFIGURATION.md) · [Deutsch](../de/CONFIGURATION.md)

[Index](README.md) · [Previous](GETTING_STARTED.md) · [Next](MESSAGES.md)

## Building the chain

`Logger.Create` passes a `LoggerConfiguration` to its callback. Its methods return
the same builder. `Enrich.WithProperty` / `WithCaller` and `WriteTo.Console` / `File`
/ `Sink` also return that builder. Inside a sink callback, methods return the
specific sink configuration.

<!-- verify: en-configuration -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .MinimumLevel(LogLevel.Debug)
    .Enrich.WithProperty("Application", "Orders")
    .Enrich.WithProperty("Environment", "Development")
    .WriteTo.Console(console => console
        .MinimumLevel(LogLevel.Warning)
        .Timestamp("HH:mm:ss")
        .Colors(ConsoleColorMode.Never))
    .WriteTo.File("logs/all.jsonl", file => file.Json()));

logger.Debug("Диагностика {OrderId}", 42); // File only.
logger.Warning("Повтор обработки {OrderId}", 42); // Console and file.
```

If no sink is registered, `Create` adds a console sink. Adding only a file or
custom sink does not implicitly add console output.

## Defaults

| Setting | Default |
| --- | --- |
| Logger threshold | `Information` |
| Sink threshold | `Trace` |
| Clock | `TimeProvider.System.GetUtcNow()` |
| Global properties, filters, regex rules | Empty |
| Caller capture | Disabled |
| Failure diagnostics | Short message to `Console.Error` |
| Console | `Console.Error`, text, `ConsoleColorMode.Auto` |
| Text timestamp | `yyyy-MM-dd HH:mm:ss.fff zzz`, UTC |
| Exception details | Enabled |
| Caller display | Enabled if captured |
| Custom sink ownership | `disposeWithLogger: false` |

## Levels

| Level | Value | Method | Text label |
| --- | --- | --- | --- |
| Trace | 0 | Trace | TRC |
| Debug | 1 | Debug | DBG |
| Information | 2 | Info | INF |
| Warning | 3 | Warning | WRN |
| Error | 4 | Error | ERR |
| Critical | 5 | Critical | CRT |
| None | 6 | — | No events |

An event must pass both logger and sink thresholds. A sink's `Trace` cannot recover
an event rejected by the logger's `Information`. `None` disables a logger or sink.
Undefined enum values cause `ArgumentOutOfRangeException`. Registered file sinks
still open during `Create`, even if the threshold is `None`.

`IsEnabled(level)` checks thresholds, sink availability, and disposal state. It does
not evaluate filters, so `true` does not guarantee output. For a valid level it
returns `false` after disposal. Use it before computing expensive arguments:

```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.Debug("Snapshot {Snapshot}", BuildExpensiveSnapshot());
}
```

`BuildExpensiveSnapshot` is application code. C# normally evaluates arguments before
calling `Debug`, even when the event will be skipped. There is no lazy API yet.

## Filters

Global filters see the prepared, redacted event; `false` stops all dispatch.
Multiple filters use short-circuit AND. Sink filters affect only their sink.

<!-- verify: en-filters -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .Filter(e => !e.Properties.ContainsKey("SkipLogging"))
    .WriteTo.Console(console => console
        .Timestamp(null)
        .Filter(e => e.Level >= LogLevel.Warning)
        .Filter(e => e.Message != "ConsoleExcluded"))
    .WriteTo.File("logs/events.jsonl", file => file.Json()));

logger.Info("OnlyFile");
logger.Warning("ConsoleAndFile");
logger.Warning("ConsoleExcluded");
using var skipped = logger.Bind("SkipLogging", true);
skipped.Error("NoOutput");
```

Filters run after capture, redaction, message rendering, and caller inspection.
Use thresholds for inexpensive level exclusion. A global filter exception reports
`Pipeline` and drops the event. A sink filter exception affects that sink only.
Callbacks execute synchronously on the logging thread.

## Configuration snapshots

A created logger uses saved settings. Mutating the builder does not reconfigure it.
Built-in sink settings are captured during registration; main builder settings
are captured when the core is created. Repeated `WithProperty` names replace values.
Delegates and custom sinks are external objects: captured mutable closure state is
not cloned. Configure on one thread before writing. There is no automatic
`appsettings.json` loading or hot reload; applications can read settings themselves.

## Custom clocks

`WithTimeProvider` supports deterministic timestamps. Return UTC from `GetUtcNow()`:

<!-- verify: en-fixed-clock -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WithTimeProvider(new FixedClock())
    .WriteTo.Console(console => console.Timestamp("yyyy-MM-dd HH:mm:ss zzz")));
logger.Info("Fixed time");

sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
}
```

Output: `2026-10-29 12:00:00 +00:00 [INF] Fixed time`. The timestamp is captured
after property processing. Configure the text time zone in [output](OUTPUT.md).
