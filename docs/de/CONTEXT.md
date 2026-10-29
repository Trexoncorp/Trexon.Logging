# Kontext: globale Eigenschaften, Bind und PushScope

[Русский](../CONTEXT.md) · [English](../en/CONTEXT.md) · [Deutsch](../de/CONTEXT.md)

[Übersicht](README.md) · [Zurück](MESSAGES.md) · [Weiter](OUTPUT.md)

Kontext ergänzt Eigenschaften ohne wiederholte Argumente. JSON speichert sie in
`properties`. Text zeigt einen Kontextwert, wenn die Vorlage ihn nennt; das gesamte
Wörterbuch wird nicht automatisch an die Nachricht angehängt.

## Drei Gültigkeitsbereiche

| API | Typische Verwendung | Lebensdauer |
| --- | --- | --- |
| `Enrich.WithProperty` | Anwendung, Umgebung | Root-Logger |
| `Bind` | Objekt, Komponente, Operation | Zurückgegebene Ansicht |
| `PushScope` | Anfrage und verschachtelte asynchrone Arbeit | `using` im asynchronen Ablauf |

## Gebundener Kontext

`Bind` liefert einen neuen `Logger` mit gemeinsamem Kern und gemeinsamen Empfängern,
aber eigenem Eigenschaftssatz. Die ursprüngliche Ansicht wird nicht verändert:

<!-- verify: de-bind-context -->
```csharp
using Trexon.Logging;

using var root = Logger.Create(config => config
    .Enrich.WithProperty("Application", "Orders")
    .WriteTo.Console(console => console.Json()));
using var orders = root.Bind("Component", "Orders");
using var customer = orders.Bind(new Dictionary<string, object?>
{
    ["CustomerId"] = 17,
    ["Region"] = "North"
});

root.Info("Root event");
orders.Info("Order {OrderId}", 42);
customer.Info("Customer event");
```

Nur das dritte Ereignis enthält `CustomerId` und `Region`. Weitere `Bind`-Aufrufe
übernehmen bereits gebundene Eigenschaften. Dispose einer Ansicht schließt weder
Elternansicht noch Root. Das Schließen des Roots macht alle Ansichten ungültig.

## Anfragebereiche

`PushScope` verwendet `AsyncLocal` und bleibt über `await` erhalten. Parallele
Abläufe überschreiben ihre Bereiche nicht gegenseitig:

<!-- verify: de-async-scopes -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Json()));

await Task.WhenAll(Enumerable.Range(1, 3).Select(async id =>
{
    using var scope = logger.PushScope(new Dictionary<string, object?>
    {
        ["RequestId"] = id,
        ["Operation"] = "LoadOrder"
    });
    await Task.Yield();
    logger.Info("Request {Expected}", id);
}));
logger.Info("Outside requests");
```

`Expected` stimmt jeweils mit `RequestId` überein. Das letzte Ereignis hat keine
Scope-Eigenschaften. Bei fließendem ExecutionContext erben untergeordnete Abläufe
den Kontext. Bewusst unterdrückten Kontextfluss stellt die Bibliothek nicht wieder her.

## Priorität gleicher Namen

Von niedrig nach hoch; Groß- und Kleinschreibung ist relevant:

```text
WithProperty → äußere Scopes → innere Scopes → Bind → Vorlagenargumente
```

<!-- verify: de-context-precedence -->
```csharp
using Trexon.Logging;

using var root = Logger.Create(config => config
    .Enrich.WithProperty("Name", "global")
    .WriteTo.Console(console => console.Timestamp(null)));
using var bound = root.Bind("Name", "bound");
using (root.PushScope("Name", "outer"))
{
    root.Info("{Name}");
    using (root.PushScope("Name", "inner"))
    {
        root.Info("{Name}");
        bound.Info("{Name}");
        bound.Info("{Name}", "event");
    }
    root.Info("{Name}");
}
root.Info("{Name}");
```

Nachrichtenfolge: `outer`, `inner`, `bound`, `event`, `outer`, `global`.

## Freigabereihenfolge und Momentaufnahmen

Scopes in umgekehrter Erstellungsreihenfolge schließen. Wird zuerst der äußere
Scope geschlossen, entsteht `InvalidOperationException`; die Kette bleibt erhalten.
Nach dem inneren kann der äußere Scope regulär geschlossen werden. Wiederholtes
Dispose eines bereits entfernten Scopes im selben Kontext hat keine Wirkung.

Das Schließen eines geerbten Scopes im untergeordneten ExecutionContext entfernt
nicht den Bereich des Elternkontexts und verhindert dessen Freigabe nicht.
Lexikalische `using`-Blöcke bevorzugen. Werte werden bei `Bind` / `PushScope` erfasst;
Serialisierungsfehler komplexer Werte werden direkt geworfen. Unabhängige Roots
haben eigene `AsyncLocal`-Ketten; gebundene Ansichten teilen die Kette ihres Roots.

## Statische Fassade

`Log.Bind(name, value)` und `Log.PushScope(name, value)` verwenden den aktuellen
globalen Root. Wörterbuchüberladungen gibt es nur bei `Logger`. `Log.Configure`
ersetzt den Root; alte Scopes und Ansichten bleiben dem alten Objekt zugeordnet.
Die Fassade beim Anwendungsstart konfigurieren.
