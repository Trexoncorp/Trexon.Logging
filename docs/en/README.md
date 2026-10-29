# Trexon.Logging documentation

[Русский](../README.md) · [English](../en/README.md) · [Deutsch](../de/README.md)

[Project home](../../README.md)

These guides cover the `0.1.0-alpha.1` MVP. Start with the runnable sample,
then choose a feature. Complete C# programs marked `verify` are compiled and run by
`python3 scripts/check-docs.py`. API signature blocks are declarations, not programs.
Sample data is shared between translations, including Russian Unicode strings.

## Learning path

1. [Getting started](GETTING_STARTED.md): requirements, installation, and first app.
2. [Configuration](CONFIGURATION.md): levels, filters, snapshots, and clocks.
3. [Messages](MESSAGES.md): placeholders, formats, and complex values.
4. [Context](CONTEXT.md): global properties, `Bind`, and async `PushScope`.
5. [Output](OUTPUT.md): console, colors, files, and JSON schema.
6. [Redaction](REDACTION.md): regex rules, order, timeouts, and limits.
7. [Errors and lifetime](ERRORS_AND_LIFETIME.md): exceptions, failures, `Flush`, and disposal.
8. [Extending](EXTENDING.md): custom `ILogSink` implementations and ownership.

## Reference and maintenance

| Document | Contents |
| --- | --- |
| [API](API.md) | All public types, methods, parameters, and defaults |
| [Architecture](ARCHITECTURE.md) | Pipeline, snapshots, locks, ownership, source map |
| [Development](DEVELOPMENT.md) | Build, tests, documentation languages, local packages |
| [Publishing](PUBLISHING.md) | CI, Trusted Publishing, versions, tags, and reruns |
| [Troubleshooting](TROUBLESHOOTING.md) | Symptoms, causes, and checks |
| [Roadmap](ROADMAP.md) | Implemented features and planned work |

## Available features

`Logger` and static `Log`; six levels plus `None`; templates and typed properties;
exception snapshots and caller capture; fluent configuration; independent levels
and filters; `TimeProvider`; `WithProperty`, `Bind`, and nested `AsyncLocal` scopes;
console/custom `TextWriter`/file output; text and JSON Lines; timestamps, time zones,
ANSI colors; regex redaction and timeouts; custom sinks and `OnError`; synchronous
flush/disposal; NuGet and symbol packages, XML docs, CI, and tag publication workflows.

Multiple-process file coordination, queues, rotation, and a ready-made DI/Host
integration package are [planned](ROADMAP.md).
