# Roadmap

[Русский](../ROADMAP.md) · [English](../en/ROADMAP.md) · [Deutsch](../de/ROADMAP.md)

[Index](README.md) · [Architecture](ARCHITECTURE.md)

## Stable release 1.0.0

Implemented: fluent MinimumLevel/Enrich/Redact/Filter/OnError/WriteTo; Logger and Log;
structured events, six levels, templates, typed properties, exception snapshots;
console timestamps/colors/caller; UTF-8 append files and JSON Lines; Bind and nested
async-flow-local scopes; regex redaction with timeouts; isolated sink failures;
synchronous writes, Flush, ownership and disposal; net8/net10 behavioral tests;
a runnable sample; NuGet packaging and release workflows with tests before publishing;
and Russian, English, and German documentation.

The first stable release is `1.0.0`. Public API compatibility is maintained
within 1.x; breaking changes require a new major version.

## Next stages

| Stage | Scope | Completion criteria |
| --- | --- | --- |
| Files | Size/time rotation, retention, then compression | Rotation boundaries, concurrent writes, disk failures, storage policy tested |
| Queue | Bounded Channel, overflow policy, FlushAsync/DisposeAsync | Ordering, expected loss/blocking, and nonempty-queue shutdown tested |
| Integration | Trexon.Logging.Extensions.Logging, ILoggerProvider, scopes, DI/Host | Application and ASP.NET Core logs share one pipeline without cycles/duplicates |
| Hardening | Benchmarks, templates, JSON options, caller strategy | Real consumer scenarios and enabled-feature costs verified |
| API evolution | Compatible additions and contract checks | Preserve 1.x compatibility on Linux, Windows, macOS; ship breaking changes in 2.0 |

C# attributes could later offer another way to construct the same configuration.
The primary API remains fluent.
