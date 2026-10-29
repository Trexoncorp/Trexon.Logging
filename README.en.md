# Trexon.Logging

[Русский](README.md) · [English](README.en.md) · [Deutsch](README.de.md)

Structured logging for C# / .NET 8+: fluent configuration, colored console output,
text and JSON files, contextual properties, and data redaction. The core library
has no external NuGet dependencies. Licensed under [MIT](LICENSE).

**Version:** `0.1.0-alpha.1`, the first MVP prerelease. The API may change before
1.0. These guides describe this version.

## Start with one line

<!-- verify: en-readme-basic -->
```csharp
using Trexon.Logging;

Log.Info("Приложение запущено");
Log.Info("Заказ {OrderId} создан", 42);
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
    .OnError(failure => Console.Error.WriteLine($"Сбой логирования: {failure.Stage}"))
    .WriteTo.Console(console => console.Timestamp("HH:mm:ss.fff").Colors())
    .WriteTo.File("logs/app.jsonl", file => file.Json()));

using (logger.PushScope("RequestId", "request-42"))
using (var orders = logger.Bind("Component", "Orders"))
{
    orders.Info("Заказ {OrderId} на сумму {Price:0.00}", 42, 199.9m);
    orders.Debug("Authorization: {Authorization}", "Bearer demo-token");
}
```

`Logger` owns its file resources; `using` disposes them. Instances provide explicit
lifetime management and test isolation. The static `Log` facade suits small apps.
Code samples retain shared example data, including Russian text to demonstrate Unicode.

## Install and verify

Install the first prerelease from NuGet. For project references and local packages,
see [getting started](docs/en/GETTING_STARTED.md).

```sh
dotnet add package Trexon.Logging --version 0.1.0-alpha.1
```

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

Start at the [documentation index](docs/en/README.md).

| Task | Guide |
| --- | --- |
| Install and run | [Getting started](docs/en/GETTING_STARTED.md) |
| Set levels, filters, and clocks | [Configuration](docs/en/CONFIGURATION.md) |
| Pass templates and structured data | [Messages](docs/en/MESSAGES.md) |
| Add application and request context | [Bind and PushScope](docs/en/CONTEXT.md) |
| Configure console, files, colors, and JSON | [Output](docs/en/OUTPUT.md) |
| Mask sensitive strings | [Redaction](docs/en/REDACTION.md) |
| Handle exceptions and close resources | [Errors and lifetime](docs/en/ERRORS_AND_LIFETIME.md) |
| Implement a sink | [Extending](docs/en/EXTENDING.md) |
| Look up signatures and defaults | [API reference](docs/en/API.md) |
| Understand the implementation | [Architecture](docs/en/ARCHITECTURE.md) |
| Build, test, and maintain translations | [Development](docs/en/DEVELOPMENT.md) |
| Publish a tagged NuGet release | [Publishing](docs/en/PUBLISHING.md) |
| Investigate unexpected behavior | [Troubleshooting](docs/en/TROUBLESHOOTING.md) |
| See planned work | [Roadmap](docs/en/ROADMAP.md) |

## MVP limits

Writes are synchronous: a slow sink delays the calling thread. Rotation, retention,
a background queue, and a `Microsoft.Extensions.Logging` adapter are planned.
`WithCaller()` inspects the stack; source locations depend on PDBs and optimizations.
Trexon.Logging is an independent project inspired by the convenience of Python Loguru.
