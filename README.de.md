# Trexon.Logging

[English](https://github.com/Trexoncorp/Trexon.Logging/blob/main/README.md) · [Русский](https://github.com/Trexoncorp/Trexon.Logging/blob/main/README.ru.md) · [Deutsch](https://github.com/Trexoncorp/Trexon.Logging/blob/main/README.de.md)

Strukturiertes Logging für C# / .NET 8+: Fluent-Konfiguration, farbige
Konsolenausgabe, Text- und JSON-Dateien, Kontext und Maskierung von Daten.
Die Kernbibliothek hat keine externen NuGet-Abhängigkeiten. Lizenz: [MIT](LICENSE).

**Version:** `1.0.0`, die erste stabile Veröffentlichung. Die öffentliche API bleibt
innerhalb von 1.x kompatibel; inkompatible Änderungen erfordern eine neue Hauptversion.

## Mit einer Zeile beginnen

<!-- verify: de-readme-basic -->
```csharp
using Trexon.Logging;

Log.Info("Приложение запущено");
Log.Info("Заказ {OrderId} создан", 42);
Log.Shutdown();
```

Standardwerte: `Console.Error`, `Information`, UTC-Zeitstempel, Textausgabe und
automatische Farben in interaktiven Konsolen. `Shutdown` schließt den globalen Logger.

## Eine Instanz konfigurieren

<!-- verify: de-readme-configured -->
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

`Logger` besitzt seine Dateiressourcen und gibt sie über `using` frei. Instanzen
eignen sich für explizite Lebenszyklen und isolierte Tests. Die statische Fassade
`Log` eignet sich für kleine Anwendungen. Die Codebeispiele verwenden gemeinsame
Beispieldaten; russische Texte demonstrieren die Unicode-Unterstützung.

## Installation und Prüfung

Die neueste stabile Version aus NuGet installieren. Projektreferenzen und lokale
Pakete werden im [Schnellstart](docs/de/GETTING_STARTED.md) beschrieben.

```sh
dotnet add package Trexon.Logging
```

Für die Entwicklung werden SDK 10 und die .NET-8-Runtime benötigt. Im Projektstamm:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build
python3 scripts/check-docs.py
```

Die Dokumentationsprüfung kompiliert markierte C#-Programme und führt sie in
temporären Ordnern aus. Release-Tags und Veröffentlichungsbefehle führt sie nicht aus.

## Dokumentation

Einstieg: [Dokumentationsübersicht](docs/de/README.md).

| Aufgabe | Anleitung |
| --- | --- |
| Installieren und starten | [Schnellstart](docs/de/GETTING_STARTED.md) |
| Stufen, Filter und Uhr konfigurieren | [Konfiguration](docs/de/CONFIGURATION.md) |
| Vorlagen und strukturierte Daten übergeben | [Nachrichten](docs/de/MESSAGES.md) |
| Anwendungs- und Anfragekontext ergänzen | [Bind und PushScope](docs/de/CONTEXT.md) |
| Konsole, Dateien, Farben und JSON einstellen | [Ausgabe](docs/de/OUTPUT.md) |
| Vertrauliche Zeichenfolgen maskieren | [Maskierung](docs/de/REDACTION.md) |
| Ausnahmen behandeln und Ressourcen schließen | [Fehler und Lebenszyklus](docs/de/ERRORS_AND_LIFETIME.md) |
| Einen eigenen Empfänger implementieren | [Erweiterungen](docs/de/EXTENDING.md) |
| Signaturen und Standardwerte nachschlagen | [API-Referenz](docs/de/API.md) |
| Implementierung verstehen | [Architektur](docs/de/ARCHITECTURE.md) |
| Entwickeln, testen und Übersetzungen pflegen | [Entwicklung](docs/de/DEVELOPMENT.md) |
| NuGet-Pakete per Tag veröffentlichen | [Veröffentlichung](docs/de/PUBLISHING.md) |
| Unerwartetes Verhalten untersuchen | [Fehlersuche](docs/de/TROUBLESHOOTING.md) |
| Geplante Arbeit ansehen | [Plan](docs/de/ROADMAP.md) |

## Aktuelle Grenzen

Die Ausgabe ist synchron: Ein langsamer Empfänger verzögert den aufrufenden Thread.
Rotation, Aufbewahrungsregeln, Hintergrundwarteschlange und ein Adapter für
`Microsoft.Extensions.Logging` sind geplant. `WithCaller()` untersucht den Stack;
Quellpositionen hängen von PDB-Dateien und Optimierungen ab. Trexon.Logging ist ein
eigenständiges Projekt, inspiriert vom Bedienkomfort von Python Loguru.
