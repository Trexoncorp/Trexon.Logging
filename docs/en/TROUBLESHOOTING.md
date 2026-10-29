# Troubleshooting

[Русский](../TROUBLESHOOTING.md) · [English](../en/TROUBLESHOOTING.md) · [Deutsch](../de/TROUBLESHOOTING.md)

[Index](README.md) · [Configuration](CONFIGURATION.md) · [Errors](ERRORS_AND_LIFETIME.md)

## Sample exits immediately or prints an exception

The console sample writes several events and exits. It is not a persistent service
or UI. Its completion message and JSON path show it reached the end. With
`--demo-error`, an exception is deliberately thrown, caught, and passed to Error;
the printed stack trace is log content. Normal mode omits this demonstration.
See [sample commands](GETTING_STARTED.md#run-the-sample).

## An event is missing

| Check | Action |
| --- | --- |
| Logger threshold | Debug/Trace are off by default; set MinimumLevel |
| Sink threshold | Event must pass both thresholds |
| None | Logger or sink may be disabled |
| Output stream | Default is Console.Error, not Console.Out |
| Filters | Global/local predicate may return false |
| Pipeline failure | Enable OnError; check snapshots, formats, regex timeout |
| Sink failure | OnError provides sink type and original exception |
| Reentrant call | Logging from the same root's callback is skipped |
| File-only configuration | File does not implicitly add Console |

IsEnabled true checks thresholds/state, not filters or delivery guarantees.

## Disposed logger or stale view

ObjectDisposedException after using/Shutdown is expected. Keep the root alive until
all users finish. Log.Configure disposes the root of old bound views. Disposing a
view does not close the root; disposing the root invalidates every view.

## RequestId missing from text

Context is in Properties. Text does not append the dictionary automatically. Use
`{RequestId}`, Json(), or a custom sink. Check name precedence and the active async flow.

## Scope does not close or closes too soon

Use reverse-order using blocks. Out-of-order disposal throws InvalidOperationException
without removing the chain. Child ExecutionContext inherits scopes; child disposal
does not alter the parent's context. See [context](CONTEXT.md).

## No colors or visible ANSI sequences

Auto requires a current standard writer without redirection. For custom writers
choose Always only when the reader supports ANSI, or Never for plain text. JSON is
never colored. Redirect Console.Error before Logger.Create or pass Writer explicitly;
the selected writer is saved during registration.

## Time differs from local time

Text defaults to UTC. Set TimeZone(TimeZoneInfo.Local) or an explicit zone. JSON
retains the event timestamp. HH:mm:ss hides the offset; include zzz when needed.

## Caller points to an outer frame or lacks a source location

Enable WithCaller globally and do not hide it with Caller(false). StackTrace-based
capture depends on inlining, optimizations, async state machines, and PDB availability.
Missing paths are omitted in JSON; lineNumber may be zero. Build paths may be
normalized, for example to /_/.

## Exception has no stack trace

Use the dedicated Exception overload and check ExceptionDetails. An exception
created but never thrown normally has no stack. Logging does not generate one.
AggregateException.Message may include child messages even with details disabled.

## Placeholder remains literal or an array becomes several arguments

Only named `{Name}` and `{Name:format}` are supported. Numeric names, @, $, alignment,
and nested braces are unsupported. Missing values stay literal if not in context.
Cast `(object)array` to pass one array value. See [messages](MESSAGES.md).

## JSON contains backslashes or Unicode escapes

New events keep Cyrillic and other alphabets readable. Old lines are not rewritten;
the file is append-only. Quotes, backslashes, controls, and some characters such as
emoji still need escaping. JSON parsers restore their original values. Complex
message values may themselves contain JSON text; process the structure in
properties rather than parsing the rendered message again.

## A complex event disappears with Pipeline diagnostics

Check cycles, unsupported types, throwing getters, invalid formats, filter exceptions,
and regex timeouts. Argument capture precedes dispatch, so preparation failure drops
the entire event. Pass a small DTO and inspect OnError. NaN/Infinity can independently
fail a JSON sink while text output still succeeds.

## File is elsewhere or cannot open

Relative paths use the process working directory at registration. Use
AppContext.BaseDirectory for an application-relative path. Files open at Create,
so permission/path failures occur immediately. Plan one active writer per file.
Repeated Log.Configure with the same path may open a new writer before the old one
closes. Configure once at startup or explicitly finish the old root at a safe time.

## Regex misses values

Rules affect strings; typed numbers, Guid, dates, and keys remain unchanged. Check
input forms, RegexOptions, and replacement. Nested strings are redacted before JSON
escaping, then the rendered message is processed again; choose stable replacements
such as ***. LoggingFailure.Exception is not a redacted ExceptionInfo.
See [redaction limits](REDACTION.md).

## Flush appears to do nothing

The built-in file writer uses AutoFlush and already empties its writer buffer.
Flush matters for buffered destinations and leaves the logger open; Dispose also
closes owned resources. Neither promises physical disk persistence during power loss.
See [Flush and Dispose](ERRORS_AND_LIFETIME.md#flush-and-dispose).

## Build or test failures

| Symptom | Action |
| --- | --- |
| Unsupported target/slnx | Use SDK 10 from global.json |
| Missing net8.0 framework | Install the .NET 8 runtime |
| Old runner command rejected | Use dotnet test --solution Trexon.Logging.slnx |
| Zero tests after runner changes | Restore and rebuild, then test --no-build |
| Old local package | Use a unique prerelease version or isolated package cache |
| GitHub publication failure | Check NUGET_USER, policy, environment, and tag format |

See [development](DEVELOPMENT.md) and [release diagnostics](PUBLISHING.md).
