# Ausnahmen, Logging-Fehler und Lebenszyklus

[Русский](../ERRORS_AND_LIFETIME.md) · [English](../en/ERRORS_AND_LIFETIME.md) · [Deutsch](../de/ERRORS_AND_LIFETIME.md)

[Übersicht](README.md) · [Zurück](REDACTION.md) · [Weiter](EXTENDING.md)

## Anwendungsausnahmen

Die Ausnahme separat übergeben. `Error` und `Critical` bieten eigene Überladungen;
`Write` erlaubt jede reguläre Stufe:

<!-- verify: de-exceptions -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Timestamp(null).ExceptionDetails()));
try
{
    throw new InvalidOperationException("Import failed",
        new ArgumentException("Invalid customer"));
}
catch (Exception exception)
{
    logger.Error(exception, "Ошибка импорта {BatchId}", 42);
    logger.Write(LogLevel.Warning, exception, "Попытка {Attempt} завершилась ошибкой", 1);
}
```

`Error("Failure {Exception}", exception)` behandelt die Ausnahme als normale
Eigenschaft. Für eine Ausnahmemomentaufnahme die Überladung mit `Exception` als
erstem Argument verwenden. Der Logger installiert keine globalen Handler und
fängt Fachlogikfehler nicht selbst ab. Über catch oder erneutes throw entscheidet
die Anwendung. Protokolliert wird der vorhandene Stacktrace, kein neuer Wurfstack.

## Momentaufnahme ExceptionInfo

Das Ereignis enthält eine Momentaufnahme statt einer Referenz auf die Originalausnahme:

| Feld | Inhalt |
| --- | --- |
| Type | Vollständiger Typname |
| Message | Ausnahmenachricht |
| StackTrace | Ursprünglicher Stack oder null |
| InnerExceptions | Momentaufnahmen innerer Ausnahmen |

Bei AggregateException werden alle unmittelbaren inneren Ausnahmen erfasst, sonst
eine vorhandene InnerException. Nach 16 Übergängen vom Ursprung endet die Rekursion.
Exception.Data und beliebige zusätzliche Eigenschaften fehlen.
`ExceptionDetails(false)` verbirgt Stack und innere Ausnahmen in der Ausgabe.
AggregateException.Message kann selbst bereits innere Nachrichten enthalten.
Andere Empfänger behalten die vollständige Momentaufnahme des Ereignisses.

## Interne Logging-Fehler

`OnError` meldet technische Fehler:

<!-- verify: de-error-handler -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .OnError(failure => Console.Error.WriteLine(
        $"Logging failure: {failure.Stage}, {failure.Exception.GetType().Name}"))
    .WriteTo.Console(console => console.Timestamp(null)));

logger.Info("Invalid format {Price:Q}", 12.5m); // Pipeline-Fehler; das Ereignis wird verworfen.
logger.Info("Следующая запись работает");
```

| Fehlerort | Verhalten |
| --- | --- |
| Ungültige API-Argumente oder Konfiguration | Ausnahme an Aufrufer |
| Dateiöffnung, Logger-Erstellung, globale Eigenschaftserfassung | Ausnahme aus Create |
| Erfassung in Bind / PushScope | Ausnahme aus der jeweiligen Methode |
| Ereigniserfassung, Regex, Argumentformat, globaler Filter | OnError; ganzes Ereignis verwerfen |
| Empfängerfilter oder Emit | OnError; andere Empfänger arbeiten weiter |
| Flush / Dispose eines Empfängers | OnError; andere Ressourcen werden weiter bearbeitet |
| OnError selbst | Ausnahme unterdrückt |
| Geschlossener Logger oder Ansicht | ObjectDisposedException |

Erstellungsfehler sind keine normalen Ereignisfehler. Bei fehlgeschlagener
Initialisierung werden bereits erstellte eigene Ressourcen geschlossen und der
Originalfehler weitergegeben. `LoggingFailure.Stage` lautet `Pipeline`, Empfängertyp,
`Flush:<Typ>` oder `Dispose:<Typ>`. Eingebaute Empfänger heißen `TextOutputSink`;
Dateipfade lassen sich daran nicht unterscheiden. Ohne Handler erhält Console.Error
eine kurze Diagnose mit Stufe und Typ, ohne technische Ausnahmenachricht.
`LoggingFailure.Exception` wird nicht maskiert.

## Ressourcenbesitz

| Ressource | Wer schließt sie? |
| --- | --- |
| Root aus Create | Anwendung über using / Dispose |
| Dateiempfänger und Stream | Root-Logger |
| Console.Out / Console.Error | Nie der Logger |
| Übergebener Console-Writer | Aufrufer |
| Eigener Empfänger, Standard | Aufrufer |
| Eigener Empfänger mit disposeWithLogger: true | Root-Logger |
| Gebundene Ansicht | Separat schließbar; Root bleibt offen |
| Scope | Umgekehrte using-Reihenfolge im selben asynchronen Ablauf |

Empfänger im Besitz des Aufrufers werden geleert, nicht geschlossen. Mehrfache
Registrierung desselben Empfängers erzeugt mehrere Emit-Aufrufe; Flush/Dispose
erfolgt je Instanz und Operation nur einmal.

## Flush und Dispose

Ein Empfänger kann Einträge vor der gemeinsamen Ausgabe im Speicher puffern.
`Flush()` fordert alle Empfänger auf, diese Puffer weiterzugeben. Es entsteht kein
neues Ereignis; der Logger bleibt offen und kann weitere Nachrichten schreiben.

Die eingebaute Dateiausgabe verwendet `StreamWriter.AutoFlush` und leert ihren
Writer-Puffer bereits nach Schreibvorgängen. Der ausdrückliche Flush-Aufruf im
Beispiel demonstriert die API. Üblicherweise genügt `using`, da Root-Dispose zuerst
leert und dann eigene Ressourcen schließt. Weder AutoFlush noch Flush garantieren
separat die physische Speicherung bei Stromausfall.

Root-Dispose markiert den Kern als geschlossen, leert und schließt eigene Empfänger.
Wiederholtes Dispose ist sicher. Schreiben, Flush, Bind und PushScope danach werfen
ObjectDisposedException; IsEnabled liefert false. Dispose einer Ansicht betrifft
nur diese; Dispose des Roots macht alle Ansichten ungültig. Kein Referenzzähler
verlängert dessen Lebensdauer. FlushAsync, DisposeAsync und Garantien einer
Hintergrundwarteschlange gibt es nicht.

## Parallelität und Callbacks

Eine Root-Sperre serialisiert Schreiben, Flush und Schließen. Empfänger sehen die
gleiche Ereignisfolge; die Startreihenfolge von Tasks garantiert keine Sperrreihenfolge.
Langsame Empfänger, große Momentaufnahmen, Regex und Filter verzögern den Aufrufer.
Callbacks kurz halten. Rekursive Einträge in denselben Root während Ausgabe/Flush
werden übersprungen; verschachteltes Flush bleibt wirkungslos. Root-Dispose aus
einem eigenen Callback wirft InvalidOperationException; im Empfänger wird das als
dessen Fehler behandelt. Zyklen zwischen Callbacks unabhängiger Roots vermeiden,
da diese eigene Sperren besitzen.

## Globales Log

Configure erstellt zuerst einen Ersatz. Unter seiner Sperre schließt es dann den
alten Root und tauscht ihn aus. Scheitert die Erstellung, arbeitet der bisherige
Logger weiter. Alte Ansichten werden nach erfolgreichem Austausch ungültig.
Das erneute Öffnen derselben Datei kann scheitern, solange der alte Writer offen
ist. Einmal beim Start konfigurieren. Falls nötig, nach Abschluss aller bisherigen
Nutzer zuerst Shutdown ausführen und danach einen neuen Root erstellen.
Shutdown schließt den aktuellen Root; weitere Schreibaufrufe werfen. Configure
kann danach einen neuen Root installieren, alte Ansichten wechseln aber nicht automatisch.
