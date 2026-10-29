# Public API reference

[Русский](../API.md) · [English](../en/API.md) · [Deutsch](../de/API.md)

[Index](README.md) · [Usage](GETTING_STARTED.md) · [Extending](EXTENDING.md)

All types are in `Trexon.Logging`. This lists the authored public MVP contract.
Code blocks are signature declarations, not complete programs. `?` marks nullable
values. Generated record equality methods and inherited .NET members are omitted.

## Logger

`public sealed class Logger : IDisposable`. No public constructor; Create returns
a root, Bind a contextual view.

```csharp
public static Logger Create(Action<LoggerConfiguration>? configure = null);
public bool IsEnabled(LogLevel level);

public Logger Bind(string name, object? value);
public Logger Bind(IReadOnlyDictionary<string, object?> properties);
public IDisposable PushScope(string name, object? value);
public IDisposable PushScope(IReadOnlyDictionary<string, object?> properties);

public void Write(LogLevel level, string messageTemplate, params object?[] values);
public void Write(LogLevel level, Exception exception, string messageTemplate,
    params object?[] values);

public void Trace(string messageTemplate, params object?[] values);
public void Debug(string messageTemplate, params object?[] values);
public void Info(string messageTemplate, params object?[] values);
public void Warning(string messageTemplate, params object?[] values);
public void Error(string messageTemplate, params object?[] values);
public void Error(Exception exception, string messageTemplate, params object?[] values);
public void Critical(string messageTemplate, params object?[] values);
public void Critical(Exception exception, string messageTemplate, params object?[] values);

public void Flush();
public void Dispose();
```

| Operation | Contract |
| --- | --- |
| Create | Capture settings, open sinks; add Console if none registered |
| IsEnabled | Threshold/state check without user filters |
| Bind | Capture properties in a view sharing the root |
| PushScope | Capture properties in the current async flow; return a scope lease |
| Write / level methods | Synchronous event preparation and dispatch |
| Flush | Flush each distinct sink without closure |
| Root Dispose | Flush and close owned sinks; invalidate all views |
| View Dispose | Close only that view |

Validation and disposed-state errors can throw. Event pipeline/sink failures are
handled through OnError. See the [failure table](ERRORS_AND_LIFETIME.md).

## Log

`public static class Log`, the process-wide facade.

```csharp
public static void Configure(Action<LoggerConfiguration> configure);
public static bool IsEnabled(LogLevel level);
public static Logger Bind(string name, object? value);
public static IDisposable PushScope(string name, object? value);

public static void Write(LogLevel level, string template, params object?[] values);
public static void Write(LogLevel level, Exception exception, string template,
    params object?[] values);
public static void Trace(string template, params object?[] values);
public static void Debug(string template, params object?[] values);
public static void Info(string template, params object?[] values);
public static void Warning(string template, params object?[] values);
public static void Error(string template, params object?[] values);
public static void Error(Exception exception, string template, params object?[] values);
public static void Critical(string template, params object?[] values);
public static void Critical(Exception exception, string template, params object?[] values);

public static void Flush();
public static void Shutdown();
```

The initial root uses Logger.Create defaults. Configure replaces and disposes it.
Shutdown closes the current root; Configure may reopen the facade afterward.
Dictionary Bind/PushScope overloads are not declared on Log.

## LoggerConfiguration

`public sealed class LoggerConfiguration` has a public parameterless constructor.
Logger.Create supplies the builder to its callback; there is no separate Build method.

```csharp
public LoggerConfiguration();
public LoggerEnrichmentConfiguration Enrich { get; }
public LoggerSinkConfiguration WriteTo { get; }

public LoggerConfiguration MinimumLevel(LogLevel level);
public LoggerConfiguration Filter(Func<LogEvent, bool> predicate);
public LoggerConfiguration Redact(string pattern, string replacement = "***",
    RegexOptions options = RegexOptions.None, TimeSpan? timeout = null);
public LoggerConfiguration OnError(Action<LoggingFailure> handler);
public LoggerConfiguration WithTimeProvider(TimeProvider timeProvider);
```

| Method | Default / effect |
| --- | --- |
| MinimumLevel | Information; None disables all events |
| Filter | Append a global AND predicate after redaction |
| Redact | Append Regex.Replace rule; null timeout means 100 ms |
| OnError | Replace failure handler; suppress its exceptions |
| WithTimeProvider | Replace TimeProvider.System |

RegexOptions belongs to System.Text.RegularExpressions. Main settings are captured
at core creation; the builder does not control an already running logger.

## LoggerEnrichmentConfiguration

`public sealed class LoggerEnrichmentConfiguration`. Library-created, no public
constructor. Methods return the main LoggerConfiguration.

```csharp
public LoggerConfiguration WithCaller(bool enabled = true);
public LoggerConfiguration WithProperty(string name, object? value);
```

WithCaller inspects the stack for enabled events. WithProperty adds/replaces a
global property; names cannot be empty or whitespace. See [context](CONTEXT.md).

## LoggerSinkConfiguration

`public sealed class LoggerSinkConfiguration`, accessed through WriteTo, with no
public constructor.

```csharp
public LoggerConfiguration Console(Action<ConsoleSinkConfiguration>? configure = null);
public LoggerConfiguration File(string path,
    Action<FileSinkConfiguration>? configure = null);
public LoggerConfiguration Sink(ILogSink sink,
    LogLevel minimumLevel = LogLevel.Trace,
    Func<LogEvent, bool>? filter = null,
    bool disposeWithLogger = false);
```

Console captures a caller-owned writer. File opens at core creation and belongs
to the logger. Sink registers an external instance, caller-owned by default.
Every call adds a registration.

## SinkConfiguration<TSelf>

Shared base configuration:

```csharp
public abstract class SinkConfiguration<TSelf>
    where TSelf : SinkConfiguration<TSelf>
{
    public TSelf MinimumLevel(LogLevel level);
    public TSelf Timestamp(string? format);
    public TSelf TimeZone(TimeZoneInfo timeZone);
    public TSelf Json(bool enabled = true);
    public TSelf ExceptionDetails(bool enabled = true);
    public TSelf Caller(bool enabled = true);
    public TSelf Filter(Func<LogEvent, bool> predicate);
}
```

| Method | Initial value | Meaning |
| --- | --- | --- |
| MinimumLevel | Trace | Additional sink threshold |
| Timestamp | yyyy-MM-dd HH:mm:ss.fff zzz | Text date format; null hides it |
| TimeZone | TimeZoneInfo.Utc | Text timestamp zone |
| Json | false | One JSON object per line |
| ExceptionDetails | true | Show stack and children |
| Caller | true | Show captured caller |
| Filter | None | Add local AND predicate |

Timestamp/TimeZone do not affect JSON timestamps. Caller(true) does not enable
capture; use Enrich.WithCaller().

## ConsoleSinkConfiguration

`public sealed class ConsoleSinkConfiguration : SinkConfiguration<ConsoleSinkConfiguration>`.
Public parameterless constructor, normally provided by the Console callback.

```csharp
public ConsoleSinkConfiguration Colors(ConsoleColorMode mode = ConsoleColorMode.Auto);
public ConsoleSinkConfiguration Writer(TextWriter writer);
```

Inherited methods return ConsoleSinkConfiguration. Writer defaults to Console.Error
and rejects null. Colors affect text only. The caller retains writer ownership.

## FileSinkConfiguration

`public sealed class FileSinkConfiguration : SinkConfiguration<FileSinkConfiguration>`.
Public parameterless constructor; only inherited options. No rotation, retention,
compression, or Colors methods. Pass the path to WriteTo.File; there is no path setter.

## LogLevel

```csharp
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}
```

None is a disabling threshold, not an emitted level. Info corresponds to Information.

## ConsoleColorMode

```csharp
public enum ConsoleColorMode
{
    Auto = 0,
    Always = 1,
    Never = 2
}
```

Auto checks the current standard writer and redirection. Arbitrary TextWriters
receive no automatic colors. See the [palette](OUTPUT.md).

## LogEvent

`public sealed class LogEvent`, created internally with no public constructor.

```csharp
public DateTimeOffset Timestamp { get; }
public LogLevel Level { get; }
public string MessageTemplate { get; }
public string Message { get; }
public IReadOnlyDictionary<string, object?> Properties { get; }
public ExceptionInfo? Exception { get; }
public CallerInfo? Caller { get; }
```

Captured before dispatch, with a read-only dictionary and independent JsonElement
values. Redaction is already applied where supported. Exception contains a snapshot,
not the original Exception.

## CallerInfo

Public positional record with a public constructor and generated init properties:

```csharp
public sealed record CallerInfo(string MemberName, string? FilePath, int LineNumber);
```

FilePath may be null and LineNumber zero. Capture depends on stack, PDBs, and
optimizations. Captured strings pass through Redact rules.

## ExceptionInfo

`public sealed class ExceptionInfo`, no public constructor.

```csharp
public string Type { get; }
public string Message { get; }
public string? StackTrace { get; }
public IReadOnlyList<ExceptionInfo> InnerExceptions { get; }
```

Type/message/stack are redacted snapshots. InnerExceptions includes aggregate
children within a depth limit. See [exceptions](ERRORS_AND_LIFETIME.md).

## ILogSink

```csharp
public interface ILogSink : IDisposable
{
    void Emit(LogEvent logEvent);
    void Flush();
}
```

Also implement IDisposable.Dispose. Calls from one root are serialized; sharing
between roots requires your own synchronization. [Complete example](EXTENDING.md).

## LoggingFailure

Public positional record with generated init properties:

```csharp
public sealed record LoggingFailure(string Stage, Exception Exception);
```

The callback receives the original technical exception without automatic redaction.
Stage formats and OnError behavior are documented under [errors](ERRORS_AND_LIFETIME.md).
