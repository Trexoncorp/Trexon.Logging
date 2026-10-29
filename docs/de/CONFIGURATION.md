# Fluent-Konfiguration

[Русский](../CONFIGURATION.md) · [English](../en/CONFIGURATION.md) · [Deutsch](../de/CONFIGURATION.md)

[Übersicht](README.md) · [Zurück](GETTING_STARTED.md) · [Weiter](MESSAGES.md)

## Die Aufrufkette

`Logger.Create` übergibt dem Callback eine `LoggerConfiguration`. Deren Methoden
geben denselben Builder zurück. Auch `Enrich.WithProperty` / `WithCaller` sowie
`WriteTo.Console` / `File` / `Sink` geben ihn zurück. Im Callback eines Empfängers
geben Methoden dessen konkrete Konfiguration zurück.

<!-- verify: de-configuration -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .MinimumLevel(LogLevel.Debug)
    .Enrich.WithProperty("Application", "Orders")
    .Enrich.WithProperty("Environment", "Development")
    .WriteTo.Console(console => console
        .MinimumLevel(LogLevel.Warning)
        .Timestamp("HH:mm:ss")
        .Colors(ConsoleColorMode.Never))
    .WriteTo.File("logs/all.jsonl", file => file.Json()));

logger.Debug("Диагностика {OrderId}", 42); // Nur Datei.
logger.Warning("Повтор обработки {OrderId}", 42); // Konsole und Datei.
```

Ohne registrierten Empfänger fügt `Create` die Konsole hinzu. Bei ausschließlicher
Datei- oder eigener Ausgabe wird keine versteckte Konsolenausgabe ergänzt.

## Standardwerte

| Einstellung | Standard |
| --- | --- |
| Logger-Mindeststufe | `Information` |
| Empfänger-Mindeststufe | `Trace` |
| Uhr | `TimeProvider.System.GetUtcNow()` |
| Globale Eigenschaften, Filter und Regex-Regeln | Leer |
| Aufrufer erfassen | Aus |
| Interne Fehlerdiagnose | Kurze Meldung in `Console.Error` |
| Konsole | `Console.Error`, Text, `ConsoleColorMode.Auto` |
| Textzeitstempel | `yyyy-MM-dd HH:mm:ss.fff zzz`, UTC |
| Ausnahmedetails | Ein |
| Aufrufer anzeigen | Ein, sofern erfasst |
| Besitz eigener Empfänger | `disposeWithLogger: false` |

## Stufen

| Stufe | Wert | Methode | Textkennung |
| --- | --- | --- | --- |
| Trace | 0 | Trace | TRC |
| Debug | 1 | Debug | DBG |
| Information | 2 | Info | INF |
| Warning | 3 | Warning | WRN |
| Error | 4 | Error | ERR |
| Critical | 5 | Critical | CRT |
| None | 6 | — | Keine Ereignisse |

Ein Ereignis muss die Mindeststufe des Loggers und des Empfängers erfüllen.
`Trace` am Empfänger stellt nichts wieder her, was `Information` am Logger verworfen
hat. `None` deaktiviert Logger oder Empfänger. Ungültige Enum-Werte lösen
`ArgumentOutOfRangeException` aus. Registrierte Dateiausgaben werden bei `Create`
auch dann geöffnet, wenn die Mindeststufe `None` ist.

`IsEnabled(level)` prüft Stufen, verfügbare Empfänger und den Freigabezustand, aber
keine Filter. `true` garantiert daher keine Ausgabe. Nach Dispose liefert die
Methode für gültige Stufen `false`. Teure Argumente vorher absichern:

```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.Debug("Snapshot {Snapshot}", BuildExpensiveSnapshot());
}
```

`BuildExpensiveSnapshot` definiert die Anwendung. C# wertet Argumente normalerweise
vor dem Aufruf von `Debug` aus, selbst wenn das Ereignis verworfen wird. Eine API
zur verzögerten Auswertung gibt es noch nicht.

## Filter

Globale Filter sehen das vorbereitete, maskierte Ereignis. `false` stoppt die
Weitergabe an alle Empfänger. Mehrere Filter werden mit kurzschließendem AND
verknüpft. Lokale Filter betreffen nur ihren jeweiligen Empfänger.

<!-- verify: de-filters -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .Filter(e => !e.Properties.ContainsKey("SkipLogging"))
    .WriteTo.Console(console => console
        .Timestamp(null)
        .Filter(e => e.Level >= LogLevel.Warning)
        .Filter(e => e.Message != "ConsoleExcluded"))
    .WriteTo.File("logs/events.jsonl", file => file.Json()));

logger.Info("OnlyFile");
logger.Warning("ConsoleAndFile");
logger.Warning("ConsoleExcluded");
using var skipped = logger.Bind("SkipLogging", true);
skipped.Error("NoOutput");
```

Filter laufen nach Erfassung, Maskierung, Nachrichtenformatierung und Aufruferanalyse.
Für günstiges Ausschließen einer Stufe `MinimumLevel` verwenden. Eine Ausnahme im
globalen Filter meldet `Pipeline` und verwirft das Ereignis. Ein Fehler im lokalen
Filter betrifft nur diesen Empfänger. Callbacks laufen synchron im aufrufenden Thread.

## Momentaufnahme der Konfiguration

Ein erstellter Logger verwendet gespeicherte Einstellungen. Änderungen am Builder
konfigurieren ihn nicht neu. Einstellungen eingebauter Empfänger werden bei der
Registrierung erfasst, die des Haupt-Builders bei der Erstellung des Kerns.
Ein erneutes `WithProperty` mit gleichem Namen ersetzt den Wert. Delegates und eigene
Empfänger bleiben externe Objekte; veränderlicher Closure-Zustand wird nicht kopiert.
Den Builder vor dem Logging in einem Thread konfigurieren. Automatisches Lesen
von `appsettings.json` und Hot Reload fehlen; die Anwendung kann Werte selbst laden.

## Eigene Uhr

`WithTimeProvider` erlaubt vorhersehbare Zeitstempel. `GetUtcNow()` muss UTC liefern:

<!-- verify: de-fixed-clock -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WithTimeProvider(new FixedClock())
    .WriteTo.Console(console => console.Timestamp("yyyy-MM-dd HH:mm:ss zzz")));
logger.Info("Fixed time");

sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
}
```

Ausgabe: `2026-10-29 12:00:00 +00:00 [INF] Fixed time`. Der Zeitstempel wird nach
der Verarbeitung der Eigenschaften erfasst. Die Textzeitzone steht unter [Ausgabe](OUTPUT.md).
