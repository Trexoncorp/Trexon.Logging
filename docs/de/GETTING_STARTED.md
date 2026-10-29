# Schnellstart

[Русский](../GETTING_STARTED.md) · [English](../en/GETTING_STARTED.md) · [Deutsch](../de/GETTING_STARTED.md)

[Übersicht](README.md) · [Weiter: Konfiguration](CONFIGURATION.md)

## Anforderungen

| Aufgabe | Anforderungen |
| --- | --- |
| Paket verwenden | .NET 8 oder eine kompatible neuere .NET-Version |
| Repository bauen | .NET SDK 10; Auswahl über `global.json` |
| Alle Tests ausführen | .NET-8- und .NET-10-Runtime; SDK 10 enthält seine Runtime |
| Dokumentation prüfen | Python 3, SDK 10 und .NET-8-Runtime |

Das Paket zielt auf `net8.0` und besitzt keine externen Runtime-PackageReferences.
Kompatibilität mit .NET Framework oder Native AOT wird für das MVP nicht zugesichert.

## Beispiel starten

Im Projektstamm ausführen:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build
```

[Program.cs](../../examples/Trexon.Logging.Sample/Program.cs) zeigt Konsole,
JSON-Datei, Aufruferdaten, Stufen, Kontext, Maskierung und optionale Ausnahmen.
`logs/sample.jsonl` liegt relativ zu `AppContext.BaseDirectory`, normalerweise
unter `bin/Release/net8.0/` des Beispiels. Weitere Starts hängen Einträge an.
Die kurze Konsolenanwendung schreibt Nachrichten, zeigt den Dateipfad und endet.
Sie wartet nicht auf Eingaben und öffnet kein zusätzliches Fenster.

Die absichtlich ausgelöste und abgefangene Ausnahme separat aktivieren:

```sh
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build -- --demo-error
```

Das Programm protokolliert den Stacktrace und läuft weiter. Erfolgreiche Starts
beider Varianten enden mit Rückgabecode `0`.

## Projekt direkt referenzieren

Diese Befehle im Projektstamm erstellen eine benachbarte Anwendung:

```sh
dotnet new console --framework net8.0 --output ../Trexon.Logging.Playground
dotnet add ../Trexon.Logging.Playground reference Trexon.Logging/Trexon.Logging.csproj
```

Deren `Program.cs` durch das vollständige Beispiel unten ersetzen und starten:

```sh
dotnet run --project ../Trexon.Logging.Playground
```

## Lokales NuGet-Paket installieren

Zuerst bauen und paketieren:

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -warnaserror
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts
```

Dann das Paket zur Anwendung hinzufügen:

```sh
trexon_package_source="$(pwd)/artifacts"
dotnet add ../Trexon.Logging.Playground package Trexon.Logging --version 0.1.0-dev --source "$trexon_package_source"
```

Entweder ProjectReference oder Paketreferenz verwenden. Beim Wechsel zum Paket
die ProjectReference entfernen. Die Bash-/zsh-Variable bildet im Projektstamm
einen absoluten Quellpfad; in anderen Shells den vollständigen Pfad angeben.

NuGet speichert Pakete nach ID und Version im Cache. Beim erneuten Bauen von
`0.1.0-dev` kann eine alte Kopie erhalten bleiben. Für eine separate Prüfung
eine eindeutige lokale Version verwenden. Bei `build` und `pack --no-build`
müssen die Versionen übereinstimmen:

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -p:Version=0.1.0-local.1
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:Version=0.1.0-local.1
```

Danach `--version 0.1.0-local.1` installieren. Ein `dotnet nuget push` ist lokal unnötig.

## Erster Logger

<!-- verify: de-getting-started -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create();
logger.Info("Приложение запущено");
logger.Warning("Осталось {Attempts} попыток", 2);
logger.Info("Заказ {OrderId} создан", 42);
```

Die Standardausgabe ist `Console.Error` mit UTC-Zeitstempeln. `Information` schließt
`Debug` und `Trace` aus. `using` leert und schließt den Root-Logger samt Dateistreams.

## Globale Fassade

Einmal beim Start konfigurieren und nach Abschluss der Anwendung schließen:

<!-- verify: de-static-facade -->
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

`Log.Configure` schließt den bisherigen Root-Logger; gespeicherte Bind-Ansichten
werden ungültig. Siehe [Lebenszyklus](ERRORS_AND_LIFETIME.md).

## Aus NuGet installieren

Die erste Vorabversion explizit wählen oder mit `--prerelease` die neueste verwenden:

```sh
dotnet add package Trexon.Logging --version 0.1.0-alpha.1
dotnet add package Trexon.Logging --prerelease
```

Die aktuelle Verfügbarkeit im Paketregister prüfen. Vorbereitung und Release-Tags
werden unter [Veröffentlichung](PUBLISHING.md) beschrieben.
