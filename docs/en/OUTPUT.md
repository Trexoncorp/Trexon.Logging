# Sinks and output formats

[Русский](../OUTPUT.md) · [English](../en/OUTPUT.md) · [Deutsch](../de/OUTPUT.md)

[Index](README.md) · [Previous](CONTEXT.md) · [Next](REDACTION.md)

A sink receives events. Built-in console and file sinks share a text/JSON formatter;
a custom `ILogSink` receives the structured `LogEvent`. Each has independent levels
and filters.

## Console and TextWriter

`WriteTo.Console()` defaults to `Console.Error`, keeping stdout available for tool
results. Select `Writer(Console.Out)` or any caller-owned `TextWriter` instead:

<!-- verify: en-captured-output -->
```csharp
using Trexon.Logging;

using var writer = new StringWriter();
using (var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Writer(writer).Timestamp(null))))
{
    logger.Info("Captured {Id}", 42);
}
Console.Write(writer.ToString());
writer.WriteLine("Writer is still open");
```

The logger flushes but never disposes the supplied writer. The writer is captured
at registration; later `Console.SetError` calls do not replace the saved writer.

## Text layout

The current layout has a fixed field order:

```text
<timestamp> [<level>] <message> (<member> at <file>:<line>)
<exception type>: <exception message>
<stack trace and inner exceptions>
```

Optional fields depend on settings and captured data. Without a source file the
caller is `(MemberName)`. Text uses the file name; JSON retains the available full
path. Text messages and exceptions may contain newlines. Use JSON Lines for strict
line-based processing. A custom layout or field order is not yet supported.

## Time and time zones

Events use their `TimeProvider.GetUtcNow()`. Sink options affect text presentation:

| Option | Effect |
| --- | --- |
| `Timestamp("HH:mm:ss.fff")` | Custom timestamp format |
| `Timestamp(null)` | Hide text timestamp |
| `TimeZone(TimeZoneInfo.Local)` | Process-local time zone |
| `TimeZone(TimeZoneInfo.Utc)` | UTC, the default |
| `Json()` | ISO event timestamp; text format/time zone do not apply |

Formats are validated during configuration using invariant culture. Custom
`TimeZoneInfo` IDs must be available on the target operating system.

## Colors

`Colors()` defaults to `ConsoleColorMode.Auto`.

| Mode | Behavior |
| --- | --- |
| Auto | ANSI for the current Console.Out/Error when not redirected |
| Always | ANSI even in redirected output or a custom writer |
| Never | Plain text |

| Level | Color | ANSI SGR |
| --- | --- | --- |
| Trace | Gray | 90 |
| Debug | Cyan | 36 |
| Information | Green | 32 |
| Warning | Yellow | 33 |
| Error | Red | 31 |
| Critical | Bold red | 1;31 |

The entire text entry is colored and ends with reset `0`. There is no custom
palette API. JSON and built-in file sinks do not emit ANSI, even with
`Colors(Always)` combined with `Json()`.

## Files

<!-- verify: en-file-output -->
```csharp
using Trexon.Logging;

using (var logger = Logger.Create(config => config
    .WriteTo.File("logs/app.log", file => file.Timestamp("HH:mm:ss"))
    .WriteTo.File("logs/errors.jsonl", file => file
        .MinimumLevel(LogLevel.Error)
        .Json())))
{
    logger.Info("Started");
    logger.Error("Failed {Operation}", "Import");
    logger.Flush();
}
Console.WriteLine(File.ReadAllText("logs/errors.jsonl"));
```

`WriteTo.File` resolves a full path relative to the process working directory at
registration, which need not be the application directory. `Logger.Create` creates
the directory and opens the stream before the first event; opening errors propagate.
The sink appends UTF-8 without BOM, uses `FileShare.Read` and `StreamWriter.AutoFlush`,
and ends each entry with the platform newline. Existing content is retained.

The logger owns the file writer. Dispose releases it even after a flush failure;
closing errors go to `OnError`. AutoFlush and Flush empty writer buffers; they do
not separately guarantee physical persistence during power loss. Use one active
writer per file. Separate roots or processes do not coordinate and a second writer
may fail with a sharing error. Rotation, retention, and compression are not implemented.

## JSON Lines

`Json()` writes one compact object per event. Expanded for readability:

```json
{
  "timestamp": "2026-10-29T12:00:00+00:00",
  "level": "Information",
  "messageTemplate": "Order {Id}",
  "message": "Order 42",
  "properties": { "Id": 42 }
}
```

| Field | Meaning |
| --- | --- |
| timestamp | ISO DateTimeOffset string, normally UTC +00:00 |
| level | Full enum name, such as Information |
| messageTemplate | Redacted template |
| message | Redacted rendered message |
| properties | Typed property object |
| caller | Optional memberName, filePath, lineNumber |
| exception | Optional type, message, stackTrace, innerExceptions |

System fields use camelCase; user dictionary keys and nested JSON member names
retain their case. Null system fields are omitted; dictionary nulls remain JSON
null. Detailed exception output retains an empty `innerExceptions` array.
Numbers stay numeric, complex values stay structured, enums become strings, and
Guid/date/TimeSpan use System.Text.Json rules.

Cyrillic and other alphabets remain readable in JSON and in objects embedded in text.
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` still escapes quotes, backslashes, and
control characters. Some characters, including emoji, may use Unicode escapes or
surrogate pairs; parsers restore them without loss. Output is intended as UTF-8 JSON;
HTML embedding requires HTML escaping. Public JsonSerializerOptions are not exposed.

`ExceptionDetails(false)` keeps type/message and hides stack/children;
`Caller(false)` hides already captured caller info in that sink. Neither changes
the structured event or disables global caller capture.

## Parse an event

<!-- verify: en-json-schema -->
```csharp
using System.Text.Json;
using Trexon.Logging;

using var writer = new StringWriter();
using (var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Writer(writer).Json())))
{
    logger.Info("Order {Id}", 42);
}
using var json = JsonDocument.Parse(writer.ToString());
int id = json.RootElement.GetProperty("properties").GetProperty("Id").GetInt32();
Console.WriteLine($"Parsed order: {id}");
```
