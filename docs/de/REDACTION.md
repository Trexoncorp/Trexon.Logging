# Maskierung mit regulären Ausdrücken

[Русский](../REDACTION.md) · [English](../en/REDACTION.md) · [Deutsch](../de/REDACTION.md)

[Übersicht](README.md) · [Zurück](OUTPUT.md) · [Weiter](ERRORS_AND_LIFETIME.md)

`Redact` verarbeitet Daten vor globalen Filtern und der Weitergabe. Alle Empfänger,
auch eigene `ILogSink`, sehen das bearbeitete Ereignis. Die Anwendung definiert
Regeln; Geheimnisse werden nicht automatisch erkannt.

## Beispiel

<!-- verify: de-redaction -->
```csharp
using System.Text.RegularExpressions;
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .Redact(@"Bearer\s+[^\s""}]+", "Bearer ***", RegexOptions.IgnoreCase)
    .Redact(@"token=[A-Za-z0-9_-]+", "token=***")
    .WriteTo.Console(console => console.Timestamp(null))
    .WriteTo.File("logs/redacted.jsonl", file => file.Json()));

logger.Info("Authorization: {Authorization}", "Bearer demo-secret");
logger.Info("Payload {Payload}", new { Token = "token=abc123", Tags = new[] { "safe" } });
logger.Error(new InvalidOperationException("token=error123"), "Import failed");
```

Das Bearer-Beispiel berücksichtigt nur die angegebenen Trennzeichen. Andere
Tokenformate benötigen eigene Muster.

## Vertrag

```csharp
LoggerConfiguration Redact(
    string pattern,
    string replacement = "***",
    RegexOptions options = RegexOptions.None,
    TimeSpan? timeout = null);
```

Ein null-Zeitlimit wählt 100 ms. Explizite Zeitlimits müssen endlich und positiv
sein; weitere Beschränkungen prüft `Regex`. Ungültige Muster, Optionen oder Zeitlimits
scheitern bei der Konfiguration. Ersetzungen verwenden Regex.Replace-Platzhalter,
etwa `$1`. Regeln laufen in Registrierungsreihenfolge und sehen jeweils das vorige
Ergebnis. Für Vergleiche ohne Beachtung der Großschreibung `RegexOptions.IgnoreCase` setzen.

## Betroffene Daten

| Daten | Behandlung |
| --- | --- |
| MessageTemplate | Alle Regeln |
| Message | Aus verarbeiteten Eigenschaften bilden, anschließend Gesamttext maskieren |
| Zeichenfolgenwerte in Eigenschaften | Alle Regeln |
| Zeichenfolgen in JSON-Arrays/-Objekten | Rekursive Maskierung |
| Type/Message/StackTrace der Ausnahme einschließlich innerer Ausnahmen | Maskierte Texte |
| MemberName/FilePath des Aufrufers | Maskierte Texte |
| Zahlen und andere einfache typisierte Werte | Typisiert und unverändert |
| Eigenschaftsschlüssel und verschachtelte JSON-Schlüssel | Unverändert |
| Zeitstempel, Stufe, Zeilennummer | Unverändert |

Guid/DateTime/TimeSpan werden zur Eigenschaftsmaskierung nicht in Text umgewandelt.
Ihre Darstellung in Message kann sich ändern, während Properties den typisierten
Wert behalten. Muss die Eigenschaft textuell maskiert werden, eine Zeichenfolge übergeben.

Verschachtelte Texte werden vor JSON-Escaping verarbeitet; tatsächliche Zeilenumbrüche
können daher zu einem Muster passen. Anschließend laufen Regeln auf der gesamten
Nachricht und erfassen auch Grenzen zwischen Vorlagentext und Argument. Ersetzungen
sollten bei Wiederholung stabil sein, etwa `***`. Passt eine Regel auf ihre eigene
Ersetzung, kann sie die fertige Nachricht erneut verändern.

## Zeitlimits und Fehler

Das Zeitlimit gilt je Regex-Operation, nicht für alle Eigenschaften und Regeln
zusammen. Große Objekte benötigen viele Operationen im aufrufenden Thread. Ein
Timeout oder Vorbereitungsfehler verwirft das gesamte Ereignis und meldet `Pipeline`
an `OnError`. Spätere Aufrufe können weiterarbeiten. Es gibt keine unmaskierte Ersatzmeldung.

## Diagnose und Grenzen

`LoggingFailure.Exception` ist der ursprüngliche technische Fehler und wird nicht
maskiert. Diagnosedetails separat auswählen; Stufe und Ausnahmetyp sind ohne die
Nachricht verfügbar. Filter sehen maskierte Werte; ein Vergleich mit dem Originaltoken
kann deshalb fehlschlagen. Stabile Eigenschaftsnamen und getrennte Werte verwenden,
Regeln mit realistischen Eingaben prüfen und verworfene Ereignisse über `OnError` beobachten.
