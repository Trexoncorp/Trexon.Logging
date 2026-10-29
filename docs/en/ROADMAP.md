# Roadmap

[Русский](../ROADMAP.md) · [English](../en/ROADMAP.md) · [Deutsch](../de/ROADMAP.md)

[Index](README.md) · [Architecture](ARCHITECTURE.md)

## MVP 0.1.0-alpha.1

Implemented: fluent MinimumLevel/Enrich/Redact/Filter/OnError/WriteTo; Logger and Log;
structured events, six levels, templates, typed properties, exception snapshots;
console timestamps/colors/caller; UTF-8 append files and JSON Lines; Bind and nested
async-flow-local scopes; regex redaction with timeouts; isolated sink failures;
synchronous writes, Flush, ownership and disposal; net8/net10 behavioral tests;
a runnable sample; NuGet packaging and release workflows with tests before publishing;
and Russian, English, and German documentation.

The first prerelease is `0.1.0-alpha.1`. The API may change before 1.0;
check compatibility when upgrading.

## Next stages

| Stage | Scope | Completion criteria |
| --- | --- | --- |
| Files | Size/time rotation, retention, then compression | Rotation boundaries, concurrent writes, disk failures, storage policy tested |
| Queue | Bounded Channel, overflow policy, FlushAsync/DisposeAsync | Ordering, expected loss/blocking, and nonempty-queue shutdown tested |
| Integration | Trexon.Logging.Extensions.Logging, ILoggerProvider, scopes, DI/Host | Application and ASP.NET Core logs share one pipeline without cycles/duplicates |
| Hardening | Benchmarks, templates, JSON options, caller strategy | Real consumer scenarios and enabled-feature costs verified |
| API 1.0 | Compatibility, docs, stable contracts | Behavior verified on Linux, Windows, macOS |

C# attributes could later offer another way to construct the same configuration.
The primary API remains fluent.
