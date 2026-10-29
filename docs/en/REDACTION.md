# Regex redaction

[Русский](../REDACTION.md) · [English](../en/REDACTION.md) · [Deutsch](../de/REDACTION.md)

[Index](README.md) · [Previous](OUTPUT.md) · [Next](ERRORS_AND_LIFETIME.md)

`Redact` processes events before global filters and dispatch. Every sink, including
custom `ILogSink`, sees the processed event. Applications define the rules;
the library does not automatically discover secrets.

## Example

<!-- verify: en-redaction -->
```csharp
using System.Text.RegularExpressions;
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .Redact(@"Bearer\s+[^\s""}]+", "Bearer ***", RegexOptions.IgnoreCase)
    .Redact(@"token=[A-Za-z0-9_-]+", "token=***")
    .WriteTo.Console(console => console.Timestamp(null))
    .WriteTo.File("logs/redacted.jsonl", file => file.Json()));

logger.Info("Authorization: {Authorization}", "Bearer demo-secret");
logger.Info("Payload {Payload}", new { Token = "token=abc123", Tags = new[] { "safe" } });
logger.Error(new InvalidOperationException("token=error123"), "Import failed");
```

The Bearer example recognizes only its specified delimiters; other token formats
need their own patterns.

## Contract

```csharp
LoggerConfiguration Redact(
    string pattern,
    string replacement = "***",
    RegexOptions options = RegexOptions.None,
    TimeSpan? timeout = null);
```

A null timeout selects 100 ms. An explicit timeout must be finite and positive;
`Regex` validates further restrictions. Invalid patterns/options/timeouts fail
during configuration. Replacements use normal Regex.Replace substitutions, such
as `$1`. Rules execute in registration order, each seeing the previous result.
Use `RegexOptions.IgnoreCase` for case-insensitive matches.

## Affected data

| Data | Treatment |
| --- | --- |
| MessageTemplate | All rules |
| Message | Render from processed properties, then redact the complete text |
| String property values | All rules |
| Strings inside JSON arrays/objects | Recursive redaction |
| Exception Type/Message/StackTrace, including children | Redacted strings |
| Caller MemberName/FilePath | Redacted strings |
| Numeric and other retained simple typed values | Remain typed and unchanged |
| Property and nested JSON keys | Unchanged |
| Timestamp, level, caller line number | Unchanged |

Guid/DateTime/TimeSpan values are not converted to strings just to redact properties.
Their rendered message text may change while Properties retain the typed value.
Pass a string if the property itself needs textual redaction.

Nested strings are processed before JSON escaping, so actual newlines can match
rules. Rules then run on the whole message, covering matches spanning template text
and an argument. Use replacements stable under repeated application, such as `***`;
a rule that matches its own replacement can change the final message again.

## Timeouts and failures

Timeout limits each regex operation, not the total time across all properties and
rules. Large objects require many operations on the calling thread. A timeout or
other preparation failure drops the entire event, reports `Pipeline` to `OnError`,
and permits subsequent writes. There is no fallback that emits unredacted data.

## Diagnostics and limits

`LoggingFailure.Exception` is the original technical pipeline/sink failure and is
not redacted. Select diagnostic details separately; stage and exception type are
available without printing its message. Filters see redacted values, so comparing
against an original token may no longer match. Use stable property names, pass
values separately, test realistic input forms, and enable `OnError` to see dropped events.
