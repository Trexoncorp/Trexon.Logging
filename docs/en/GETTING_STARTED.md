# Getting started

[Русский](../GETTING_STARTED.md) · [English](../en/GETTING_STARTED.md) · [Deutsch](../de/GETTING_STARTED.md)

[Index](README.md) · [Next: configuration](CONFIGURATION.md)

## Requirements

| Task | Requirements |
| --- | --- |
| Use the package | .NET 8 or a compatible later .NET version |
| Build this repository | .NET SDK 10, selected by `global.json` |
| Run all tests | .NET 8 and .NET 10 runtimes; SDK 10 includes its runtime |
| Verify documentation | Python 3, SDK 10, and .NET 8 runtime |

The package targets `net8.0` with no external runtime PackageReferences.
.NET Framework and Native AOT compatibility are not claimed for this MVP.

## Run the sample

From the repository root:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build
```

[Program.cs](../../examples/Trexon.Logging.Sample/Program.cs) demonstrates console
and JSON output, caller info, levels, context, redaction, and optional exceptions.
`logs/sample.jsonl` is relative to `AppContext.BaseDirectory`, normally the sample's
`bin/Release/net8.0/` directory. Reruns append. This short console application
writes messages, prints the file path, and exits; it does not wait for input.

Enable the deliberately thrown and caught exception separately:

```sh
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build -- --demo-error
```

The program logs its stack trace and continues. Successful runs in either mode
exit with code `0`.

## Reference the project directly

Run these from the repository root to create a sibling application:

```sh
dotnet new console --framework net8.0 --output ../Trexon.Logging.Playground
dotnet add ../Trexon.Logging.Playground reference Trexon.Logging/Trexon.Logging.csproj
```

Replace that application's `Program.cs` with the complete example below, then run:

```sh
dotnet run --project ../Trexon.Logging.Playground
```

## Install a local NuGet package

Build and pack first:

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -warnaserror
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts
```

Add it to the application:

```sh
trexon_package_source="$(pwd)/artifacts"
dotnet add ../Trexon.Logging.Playground package Trexon.Logging --version 0.1.0-dev --source "$trexon_package_source"
```

Use either a ProjectReference or a package reference. Remove the ProjectReference
when switching to the package. The Bash/zsh variable above resolves an absolute
source path from the repository root; use an explicit absolute path in other shells.

NuGet caches by package ID and version. Rebuilding `0.1.0-dev` can leave an old
cached copy in the consumer. For a separate check, use a unique local version;
`build` and `pack --no-build` must use the same version:

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -p:Version=0.1.0-local.1
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:Version=0.1.0-local.1
```

Then install `--version 0.1.0-local.1`. No `dotnet nuget push` is needed locally.

## Your first logger

<!-- verify: en-getting-started -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create();
logger.Info("Приложение запущено");
logger.Warning("Осталось {Attempts} попыток", 2);
logger.Info("Заказ {OrderId} создан", 42);
```

The default output is `Console.Error`, with UTC timestamps. `Information` excludes
`Debug` and `Trace`. `using` flushes and disposes the root, including its file streams.

## Process-wide facade

Configure once at startup and shut down when application work ends:

<!-- verify: en-static-facade -->
```csharp
using Trexon.Logging;

Log.Configure(config => config
    .MinimumLevel(LogLevel.Debug)
    .WriteTo.Console(console => console.Timestamp("HH:mm:ss")));

try
{
    using var request = Log.PushScope("RequestId", "request-42");
    Log.Debug("Обработка {OrderId}", 42);
    Log.Info("Готово");
}
finally
{
    Log.Shutdown();
}
```

`Log.Configure` disposes the previous root. Previously saved bound views become
invalid. See [lifetime](ERRORS_AND_LIFETIME.md).

## Install from NuGet

The plain `dotnet add package Trexon.Logging` command selects a stable release.
While only alpha versions exist, use `--prerelease` or an explicit `--version`.

Select the first prerelease explicitly, or use `--prerelease` for the latest one:

```sh
dotnet add package Trexon.Logging --version 0.1.0-alpha.1
dotnet add package Trexon.Logging --prerelease
```

Check the registry for current release availability. See [publishing](PUBLISHING.md)
for local package preparation and tag releases.
