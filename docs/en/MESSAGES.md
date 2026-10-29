# Messages and structured properties

[Русский](../MESSAGES.md) · [English](../en/MESSAGES.md) · [Deutsch](../de/MESSAGES.md)

[Index](README.md) · [Previous](CONFIGURATION.md) · [Next](CONTEXT.md)

## Templates and arguments

Pass named placeholders and values separately to retain the template, rendered
message, and typed properties:

<!-- verify: en-message-templates -->
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

The first event has `Id = 42` and `Price = 12.5m`. Its template remains
`Order {Id}: {Price:0.00}; repeat {Id}`, subject to configured redaction.
Interpolated strings such as `$"Order {id}"` arrive already rendered, so the logger
cannot recover a structured `Id` property.

## Supported syntax

| Form | Behavior |
| --- | --- |
| `{Name}` | Named property |
| `{Price:0.00}` | `IFormattable` format using invariant culture |
| `{{` / `}}` | Literal braces |
| Repeated `{Name}` | Reuses one property and one argument |
| Missing argument | Context value, otherwise literal placeholder |
| `{0}`, `{@Object}`, `{$Object}`, `{Name,10}` | Unsupported; remain literal |

Names follow `[A-Za-z_][A-Za-z0-9_]*` and are case-sensitive. The first occurrence
binds the next argument. Escaped braces are processed before placeholders; nested
braces within placeholders are unsupported. See [the parser](../../Trexon.Logging/Internal/MessageTemplate.cs).

## Missing and extra arguments

The renderer looks up missing arguments in combined context, so `"Request {RequestId}"`
can use a scope property. With no value, the placeholder stays unchanged.
Extra arguments use `_argN`, where N is their original zero-based position:
`Info("{Value}", 10, 20)` stores `Value = 10` and `_arg1 = 20`. A conflicting
template property name is avoided by appending `_` until the name is free.

Pass `(object?)null` for an explicit null value. A null `params` array is treated
as one null argument, although the array's public nullable annotation is non-null.

## Arrays as a single value

A reference-type array can become the `params object?[]` argument list. Cast it to
`object` to pass one structured array:

<!-- verify: en-array-property -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Json()));
string[] tags = ["new", "paid"];
logger.Info("Tags {Tags}", (object)tags);
```

`Tags` is a JSON array containing both strings.

## Formats

Formats apply to `IFormattable` values such as decimal, DateTime, DateTimeOffset,
and Guid. Invariant culture makes `12.5m` with `0.00` render as `12.50` regardless
of process culture. Property date formats are separate from the event `Timestamp`.
Complex objects become `JsonElement`; custom or numeric format strings do not apply
to their snapshots. An invalid simple-value format reports `Pipeline` and drops
the event via `OnError`.

## Value snapshots

| Input | Stored value |
| --- | --- |
| null, string, bool, built-in numbers | Original simple typed value |
| DateTime, DateTimeOffset, Guid, TimeSpan | Original typed value |
| char | Single-character string |
| enum | `ToString()` result |
| JsonElement | Independent clone |
| DTO, collection, dictionary, other object | System.Text.Json snapshot |

Arguments are captured during writing; global properties at core creation; bound
values during `Bind`; scopes during `PushScope`. Later collection mutations do not
change a captured event. A custom sink can inspect `JsonElement` properties:

```csharp
if (logEvent.Properties["Customer"] is System.Text.Json.JsonElement customer)
{
    string? name = customer.GetProperty("Name").GetString();
}
```

This fragment belongs inside `Emit(LogEvent logEvent)`. Object members use standard
System.Text.Json serialization rules, with readable Unicode output. There are no
public serializer options, destructuring policies, or custom converters yet.
Cycles, unsupported types, and throwing getters can fail capture: event arguments
report `Pipeline`; `Create`, `Bind`, and `PushScope` throw directly for their captures.
A JSON sink can independently fail on unsupported numeric values such as
`double.NaN`. See [errors](ERRORS_AND_LIFETIME.md) and [output](OUTPUT.md).
