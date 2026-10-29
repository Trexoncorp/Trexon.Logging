# Trexon.Logging

[English](https://github.com/Trexoncorp/Trexon.Logging/blob/main/README.en.md) · [Русский](https://github.com/Trexoncorp/Trexon.Logging/blob/main/README.md) · [Deutsch](https://github.com/Trexoncorp/Trexon.Logging/blob/main/README.de.md)

Structured logging for C# / .NET 8+: fluent configuration, colored console output,
text and JSON files, contextual properties, and data redaction. The core library
has no external NuGet dependencies. Licensed under [MIT](https://github.com/Trexoncorp/Trexon.Logging/blob/main/LICENSE).

**Status:** early MVP prerelease. The API may change before 1.0.
This README is the default package documentation on NuGet.

## Start with one line

<!-- verify: en-readme-basic -->
```csharp
using Trexon.Logging;

Log.Info("Application started");
Log.Info("Order {OrderId} created", 42);
Log.Shutdown();
```

Defaults: `Console.Error`, `Information`, UTC timestamps, text output, and automatic
colors in an interactive console. `Shutdown` closes the process-wide logger.

## Configure an instance

<!-- verify: en-readme-configured -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .MinimumLevel(LogLevel.Debug)
    .Enrich.WithCaller()
    .Enrich.WithProperty("Application", "Orders")
    .Redact(@"Bearer\s+\S+", "Bearer ***")
    .OnError(failure => Console.Error.WriteLine($"Logging failure: {failure.Stage}"))
    .WriteTo.Console(console => console.Timestamp("HH:mm:ss.fff").Colors())
    .WriteTo.File("logs/app.jsonl", file => file.Json()));

using (logger.PushScope("RequestId", "request-42"))
using (var orders = logger.Bind("Component", "Orders"))
{
    orders.Info("Order {OrderId} costs {Price:0.00}", 42, 199.9m);
    orders.Debug("Authorization: {Authorization}", "Bearer demo-token");
}
```

`Logger` owns its file resources; `using` disposes them. Instances provide explicit
lifetime management and test isolation. The static `Log` facade suits small apps.

## Install and verify

Install the latest prerelease from NuGet. For project references and local packages,
see [getting started](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/GETTING_STARTED.md).

```sh
dotnet add package Trexon.Logging --prerelease
```

Without `--prerelease`, the command selects stable versions only. Until a stable
version is published, include this option or pin an explicit prerelease with
`--version 0.1.0-alpha.2`.

Development requires SDK 10 and the .NET 8 runtime. From the repository root:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build
python3 scripts/check-docs.py
```

The documentation checker compiles and runs marked C# programs in temporary folders.
It does not execute release tags or package publication commands.

## Documentation

Start at the [documentation index](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/README.md).

| Task | Guide |
| --- | --- |
| Install and run | [Getting started](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/GETTING_STARTED.md) |
| Set levels, filters, and clocks | [Configuration](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/CONFIGURATION.md) |
| Pass templates and structured data | [Messages](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/MESSAGES.md) |
| Add application and request context | [Bind and PushScope](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/CONTEXT.md) |
| Configure console, files, colors, and JSON | [Output](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/OUTPUT.md) |
| Mask sensitive strings | [Redaction](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/REDACTION.md) |
| Handle exceptions and close resources | [Errors and lifetime](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/ERRORS_AND_LIFETIME.md) |
| Implement a sink | [Extending](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/EXTENDING.md) |
| Look up signatures and defaults | [API reference](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/API.md) |
| Understand the implementation | [Architecture](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/ARCHITECTURE.md) |
| Build, test, and maintain translations | [Development](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/DEVELOPMENT.md) |
| Publish a tagged NuGet release | [Publishing](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/PUBLISHING.md) |
| Investigate unexpected behavior | [Troubleshooting](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/TROUBLESHOOTING.md) |
| See planned work | [Roadmap](https://github.com/Trexoncorp/Trexon.Logging/blob/main/docs/en/ROADMAP.md) |

## MVP limits

Writes are synchronous: a slow sink delays the calling thread. Rotation, retention,
a background queue, and a `Microsoft.Extensions.Logging` adapter are planned.
`WithCaller()` inspects the stack; source locations depend on PDBs and optimizations.
Trexon.Logging is an independent project inspired by the convenience of Python Loguru.
