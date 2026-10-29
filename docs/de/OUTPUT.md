# Empfänger und Ausgabeformate

[Русский](../OUTPUT.md) · [English](../en/OUTPUT.md) · [Deutsch](../de/OUTPUT.md)

[Übersicht](README.md) · [Zurück](CONTEXT.md) · [Weiter](REDACTION.md)

Ein Sink empfängt Ereignisse. Konsole und Datei teilen einen Text-/JSON-Formatierer;
ein eigener `ILogSink` erhält das strukturierte `LogEvent`. Jeder Empfänger besitzt
eigene Mindeststufen und Filter.

## Konsole und TextWriter

`WriteTo.Console()` verwendet `Console.Error`, damit stdout für Ergebnisse einer
Kommandozeilenanwendung frei bleibt. Alternativ `Writer(Console.Out)` oder einen
eigenen `TextWriter` wählen:

<!-- verify: de-captured-output -->
```csharp
using Trexon.Logging;

using var writer = new StringWriter();
using (var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Writer(writer).Timestamp(null))))
{
    logger.Info("Captured {Id}", 42);
}
Console.Write(writer.ToString());
writer.WriteLine("Writer is still open");
```

Der Logger leert den übergebenen Writer, schließt ihn aber nicht. Er wird bei der
Registrierung gespeichert; spätere `Console.SetError`-Aufrufe ersetzen ihn nicht.

## Textaufbau

Die Reihenfolge der Felder ist derzeit fest:

```text
<timestamp> [<level>] <message> (<member> at <file>:<line>)
<Ausnahmetyp>: <Ausnahmenachricht>
<Stacktrace und innere Ausnahmen>
```

Optionale Felder hängen von Einstellungen und erfassten Daten ab. Ohne Quelldatei
erscheint der Aufrufer als `(MemberName)`. Text enthält nur den Dateinamen, JSON den
verfügbaren vollständigen Pfad. Nachrichten und Ausnahmen können Zeilenumbrüche
enthalten. Für strikt zeilenweisen Import JSON Lines verwenden. Eigene Layouts
und eine andere Feldreihenfolge werden noch nicht unterstützt.

## Zeit und Zeitzonen

Ereignisse verwenden `TimeProvider.GetUtcNow()`. Empfängeroptionen ändern die Textdarstellung:

| Option | Wirkung |
| --- | --- |
| `Timestamp("HH:mm:ss.fff")` | Eigenes Zeitformat |
| `Timestamp(null)` | Zeitstempel im Text ausblenden |
| `TimeZone(TimeZoneInfo.Local)` | Lokale Zeitzone des Prozesses |
| `TimeZone(TimeZoneInfo.Utc)` | UTC, der Standard |
| `Json()` | ISO-Zeitstempel des Ereignisses; Textformat und Textzeitzone gelten nicht |

Formate werden bei der Konfiguration kulturunabhängig geprüft. Eigene
`TimeZoneInfo`-IDs müssen auf dem Zielbetriebssystem verfügbar sein.

## Farben

`Colors()` verwendet standardmäßig `ConsoleColorMode.Auto`.

| Modus | Verhalten |
| --- | --- |
| Auto | ANSI für aktuelles Console.Out/Error ohne Umleitung |
| Always | ANSI auch bei Umleitung oder eigenem Writer |
| Never | Einfacher Text |

| Stufe | Farbe | ANSI SGR |
| --- | --- | --- |
| Trace | Grau | 90 |
| Debug | Cyan | 36 |
| Information | Grün | 32 |
| Warning | Gelb | 33 |
| Error | Rot | 31 |
| Critical | Fett rot | 1;31 |

Der ganze Texteintrag wird eingefärbt und endet mit Reset `0`. Eine eigene Palette
ist nicht konfigurierbar. JSON und eingebaute Dateiausgabe enthalten keine
ANSI-Sequenzen, auch nicht bei `Colors(Always)` zusammen mit `Json()`.

## Dateien

<!-- verify: de-file-output -->
```csharp
using Trexon.Logging;

using (var logger = Logger.Create(config => config
    .WriteTo.File("logs/app.log", file => file.Timestamp("HH:mm:ss"))
    .WriteTo.File("logs/errors.jsonl", file => file
        .MinimumLevel(LogLevel.Error)
        .Json())))
{
    logger.Info("Started");
    logger.Error("Failed {Operation}", "Import");
    logger.Flush();
}
Console.WriteLine(File.ReadAllText("logs/errors.jsonl"));
```

`WriteTo.File` bildet bei der Registrierung einen absoluten Pfad relativ zum
Arbeitsverzeichnis des Prozesses; dieses kann vom Anwendungsverzeichnis abweichen.
`Logger.Create` erstellt Verzeichnisse und öffnet den Stream vor dem ersten Ereignis.
Öffnungsfehler werden weitergegeben. Verwendet werden Anhängen, UTF-8 ohne BOM,
`FileShare.Read`, `StreamWriter.AutoFlush` und ein plattformüblicher Zeilenabschluss.
Vorhandener Inhalt bleibt erhalten.

Der Logger besitzt den Datei-Writer. Dispose gibt ihn auch nach einem Flush-Fehler
frei; Schließfehler gehen an `OnError`. AutoFlush und Flush leeren Writer-Puffer,
garantieren aber keine physische Speicherung bei Stromausfall. Pro Datei nur einen
aktiven Writer vorsehen. Unabhängige Roots oder Prozesse koordinieren sich nicht;
ein zweiter Writer kann mit einem Freigabefehler scheitern. Rotation,
Aufbewahrungsregeln und Komprimierung fehlen noch.

## JSON Lines

`Json()` schreibt ein kompaktes Objekt pro Ereignis. Hier zur besseren Lesbarkeit eingerückt:

```json
{
  "timestamp": "2026-10-29T12:00:00+00:00",
  "level": "Information",
  "messageTemplate": "Order {Id}",
  "message": "Order 42",
  "properties": { "Id": 42 }
}
```

| Feld | Bedeutung |
| --- | --- |
| timestamp | ISO-DateTimeOffset, üblicherweise UTC +00:00 |
| level | Vollständiger Enum-Name, etwa Information |
| messageTemplate | Maskierte Vorlage |
| message | Formatierte und maskierte Nachricht |
| properties | Objekt mit typisierten Eigenschaften |
| caller | Optional: memberName, filePath, lineNumber |
| exception | Optional: type, message, stackTrace, innerExceptions |

Systemfelder verwenden camelCase. Schlüssel und verschachtelte JSON-Namen behalten
ihre Schreibweise. Null-Systemfelder entfallen, null-Wörterbuchwerte bleiben JSON
null. Bei detaillierter Ausnahmeausgabe bleibt ein leeres `innerExceptions`-Array
erhalten. Zahlen bleiben Zahlen, komplexe Werte Strukturen, Enums werden Zeichenfolgen;
Guid, Datum und TimeSpan folgen den System.Text.Json-Regeln.

Kyrillisch und andere Alphabete bleiben in JSON und Objekten im Nachrichtentext lesbar.
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` maskiert weiterhin Anführungszeichen,
Backslashes und Steuerzeichen. Manche Zeichen, etwa Emoji, können als Unicode-Escapes
oder Surrogatpaare erscheinen; Parser stellen sie verlustfrei wieder her. Die Ausgabe
ist für UTF-8-JSON gedacht; Einbettung in HTML erfordert HTML-Escaping.
Öffentliche JsonSerializerOptions werden nicht angeboten.

`ExceptionDetails(false)` behält Typ/Nachricht und verbirgt Stack/innere Ausnahmen.
`Caller(false)` verbirgt erfasste Aufruferdaten für diesen Empfänger. Das strukturierte
Ereignis und die globale Aufrufererfassung bleiben unverändert.

## Ereignis einlesen

<!-- verify: de-json-schema -->
```csharp
using System.Text.Json;
using Trexon.Logging;

using var writer = new StringWriter();
using (var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Writer(writer).Json())))
{
    logger.Info("Order {Id}", 42);
}
using var json = JsonDocument.Parse(writer.ToString());
int id = json.RootElement.GetProperty("properties").GetProperty("Id").GetInt32();
Console.WriteLine($"Parsed order: {id}");
```
