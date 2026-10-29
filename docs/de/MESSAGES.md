# Nachrichten und strukturierte Eigenschaften

[Русский](../MESSAGES.md) · [English](../en/MESSAGES.md) · [Deutsch](../de/MESSAGES.md)

[Übersicht](README.md) · [Zurück](CONFIGURATION.md) · [Weiter](CONTEXT.md)

## Vorlagen und Argumente

Benannte Platzhalter und Werte getrennt übergeben. Dadurch bleiben Vorlage,
formatierte Nachricht und typisierte Eigenschaften erhalten:

<!-- verify: de-message-templates -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Timestamp(null)));

logger.Info("Order {Id}: {Price:0.00}; repeat {Id}", 42, 12.5m);
logger.Info("Literal {{Id}}, actual {Id}", 42);
logger.Info("Nullable {Value}", (object?)null);
logger.Info("Missing {Value}");
logger.Info("Customer {Customer}", new { Name = "Anton", Orders = 3 });
```

```text
[INF] Order 42: 12.50; repeat 42
[INF] Literal {Id}, actual 42
[INF] Nullable null
[INF] Missing {Value}
```

Das erste Ereignis enthält `Id = 42` und `Price = 12.5m`. Seine Vorlage bleibt
`Order {Id}: {Price:0.00}; repeat {Id}`, gegebenenfalls maskiert. Interpolierte
Zeichenfolgen wie `$"Order {id}"` sind bereits formatiert; daraus kann der Logger
keine strukturierte Eigenschaft `Id` zurückgewinnen.

## Unterstützte Syntax

| Form | Verhalten |
| --- | --- |
| `{Name}` | Benannte Eigenschaft |
| `{Price:0.00}` | `IFormattable`-Format mit kulturunabhängiger Formatierung |
| `{{` / `}}` | Wörtliche Klammern |
| Wiederholtes `{Name}` | Verwendet dieselbe Eigenschaft und dasselbe Argument |
| Fehlendes Argument | Kontextwert, andernfalls unveränderter Platzhalter |
| `{0}`, `{@Object}`, `{$Object}`, `{Name,10}` | Nicht unterstützt; bleiben unverändert |

Namen entsprechen `[A-Za-z_][A-Za-z0-9_]*`; Groß- und Kleinschreibung zählt.
Das erste Vorkommen bindet das nächste Argument. Maskierte Klammern werden vor
Platzhaltern verarbeitet. Verschachtelte Klammern im Platzhalter werden nicht
unterstützt. Siehe [Parser](../../Trexon.Logging/Internal/MessageTemplate.cs).

## Fehlende und zusätzliche Argumente

Fehlende Argumente sucht der Renderer im zusammengeführten Kontext. So kann
`"Request {RequestId}"` eine Scope-Eigenschaft verwenden. Ohne Wert bleibt der
Platzhalter unverändert. Zusätzliche Argumente heißen `_argN`, mit dem ursprünglichen
nullbasierten Argumentindex: `Info("{Value}", 10, 20)` speichert `Value = 10` und
`_arg1 = 20`. Bei Konflikten mit Vorlagennamen wird `_` angehängt, bis der Name frei ist.

Ein explizites null als `(object?)null` übergeben. Ein null-`params`-Array wird als
ein null-Argument interpretiert, obwohl die öffentliche Array-Annotation non-null ist.

## Array als einzelner Wert

Ein Array von Referenztypen kann selbst die `params object?[]`-Liste bilden.
Für einen strukturierten Arraywert nach `object` konvertieren:

<!-- verify: de-array-property -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Json()));
string[] tags = ["new", "paid"];
logger.Info("Tags {Tags}", (object)tags);
```

`Tags` wird ein JSON-Array mit beiden Zeichenfolgen.

## Formate

Formate gelten für `IFormattable`, etwa decimal, DateTime, DateTimeOffset oder Guid.
Die kulturunabhängige Formatierung ergibt für `12.5m` und `0.00` immer `12.50`.
Das Datumsformat einer Eigenschaft ist vom `Timestamp` des Ereignisses getrennt.
Komplexe Objekte werden zu `JsonElement`; Zahlen- oder benutzerdefinierte Formate
gelten für diese Momentaufnahme nicht. Ein ungültiges Format eines einfachen Wertes
meldet über `OnError` einen `Pipeline`-Fehler und verwirft das Ereignis.

## Momentaufnahmen von Werten

| Eingabe | Gespeicherter Wert |
| --- | --- |
| null, string, bool, eingebaute Zahlentypen | Einfacher typisierter Wert |
| DateTime, DateTimeOffset, Guid, TimeSpan | Ursprünglicher typisierter Wert |
| char | Zeichenfolge mit einem Zeichen |
| enum | Ergebnis von `ToString()` |
| JsonElement | Unabhängige Kopie |
| DTO, Sammlung, Wörterbuch, anderes Objekt | System.Text.Json-Momentaufnahme |

Argumente werden beim Schreiben erfasst, globale Eigenschaften beim Erstellen des
Kerns, gebundene Werte bei `Bind` und Bereiche bei `PushScope`. Spätere Änderungen
an Sammlungen verändern kein erfasstes Ereignis. Ein eigener Empfänger kann
`JsonElement` auswerten:

```csharp
if (logEvent.Properties["Customer"] is System.Text.Json.JsonElement customer)
{
    string? name = customer.GetProperty("Name").GetString();
}
```

Das Fragment gehört in `Emit(LogEvent logEvent)`. Objektmitglieder verwenden die
üblichen System.Text.Json-Regeln mit lesbarer Unicode-Ausgabe. Öffentliche
Serialisierungseinstellungen, Destrukturierungsregeln oder Konverter gibt es noch
nicht. Zyklen, nicht unterstützte Typen und werfende Getter können die Erfassung
verhindern: Ereignisargumente melden `Pipeline`; `Create`, `Bind` und `PushScope`
werfen bei ihrer Erfassung direkt. JSON-Ausgabe kann zusätzlich an Zahlen wie
`double.NaN` scheitern. Siehe [Fehler](ERRORS_AND_LIFETIME.md) und [Ausgabe](OUTPUT.md).
