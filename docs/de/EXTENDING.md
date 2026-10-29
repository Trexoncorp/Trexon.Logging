# Eigene ILogSink-Implementierung

[Русский](../EXTENDING.md) · [English](../en/EXTENDING.md) · [Deutsch](../de/EXTENDING.md)

[Übersicht](README.md) · [Zurück](ERRORS_AND_LIFETIME.md) · [API](API.md)

`ILogSink` ist die öffentliche Erweiterung für eigene Ziele und Formate. Es erhält
ein vorbereitetes, maskiertes `LogEvent`. Eingebaute Formatierer sind intern;
ein öffentliches `ILogFormatter` oder beliebige Enricher-Callbacks fehlen im MVP.

## Minimaler Empfänger

Ein vollständiges Programm, das Ereignisse im Speicher aufbewahrt:

<!-- verify: de-custom-sink -->
```csharp
using Trexon.Logging;

using var sink = new CollectingSink();
using (var logger = Logger.Create(config => config
    .Redact("demo-secret")
    .WriteTo.Sink(sink, minimumLevel: LogLevel.Warning,
        filter: e => !e.Properties.ContainsKey("Skip"))))
{
    logger.Info("Below sink threshold");
    logger.Warning("Order {OrderId}, token {Token}", 42, "demo-secret");
}

foreach (var entry in sink.Snapshot())
{
    Console.WriteLine($"{entry.Level}: {entry.Message}");
    Console.WriteLine($"OrderId = {entry.Properties["OrderId"]}");
}

sealed class CollectingSink : ILogSink
{
    private readonly object _gate = new();
    private readonly List<LogEvent> _events = [];
    private bool _disposed;

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _events.Add(logEvent);
        }
    }

    public LogEvent[] Snapshot()
    {
        lock (_gate) return _events.ToArray();
    }

    public void Flush() { }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
    }
}
```

Der Empfänger erhält ein Warning-Ereignis mit Token `***`. Standardmäßig besitzt
der Logger diesen Empfänger nicht; das äußere `using var sink` schließt ihn.
Nur dieses Beispiel speichert einen Verlauf; die Bibliothek führt selbst keine Ereignisliste.

## Besitz

Innerhalb des Konfigurationscallbacks:

```csharp
config.WriteTo.Sink(
    sink,
    minimumLevel: LogLevel.Trace,
    filter: null,
    disposeWithLogger: true);
```

Bei `true` führt der Root zuerst Flush und danach Dispose aus. Bei `false` leert
er ebenfalls, der Aufrufer schließt jedoch den Empfänger. Einen Besitzer und die
Abschlussreihenfolge festlegen. Bei mehreren Roots sind parallele Emit-Aufrufe
möglich, deshalb besitzt das Beispiel eine eigene Sperre. Der Ressourcenbesitz
zwischen Roots wird nicht koordiniert. Einen geteilten Empfänger erst schließen,
wenn alle Roots ihn nicht mehr verwenden.

## Ereignisdaten

LogEvent stellt Timestamp, Level, MessageTemplate, Message, Properties, Exception
und Caller bereit. Properties ist schreibgeschützt; komplexe Werte sind unabhängige
JsonElement-Momentaufnahmen. Spätere Änderungen der Eingaben ändern gespeicherte
Ereignisse nicht. Exception ist eine ExceptionInfo-Momentaufnahme, keine Referenz
auf die Originalausnahme. Caller kann fehlen; FilePath kann trotz Erfassung null sein.

`ExceptionDetails(false)` und `Caller(false)` betreffen eingebaute Formatierer.
Ein eigener Empfänger entscheidet selbst, welche erfassten und maskierten Felder er ausgibt.

## Fehler und Aufwand

Emit läuft synchron unter der Root-Sperre. Fehler gehen an OnError, andere Empfänger
arbeiten weiter. Wiederholungen, erneute Zustellung und Rücknahme bereits teilweise
geschriebener Daten sind nicht eingebaut. Flush leert eigene Puffer, Dispose gibt
Ressourcen frei. Beide laufen unter der Root-Sperre; Fehler werden isoliert.

Aus einem Empfänger weder denselben Logger noch seine Lebenszyklusmethoden aufrufen:
Rekursive Einträge werden übersprungen, Root-Dispose während der Ausgabe ist verboten.
Ein Netzwerkempfänger muss Zeitlimits, Warteschlange, Speichergrenzen und Verhalten
bei Ausfällen selbst definieren; ILogSink liefert diese Mechanismen nicht automatisch.
