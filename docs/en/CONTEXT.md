# Context: global properties, Bind, and PushScope

[Русский](../CONTEXT.md) · [English](../en/CONTEXT.md) · [Deutsch](../de/CONTEXT.md)

[Index](README.md) · [Previous](MESSAGES.md) · [Next](OUTPUT.md)

Context attaches properties to events without repeating arguments. JSON stores
them in `properties`. Text shows a context value when named in the template;
it does not append the entire property dictionary automatically.

## Three scopes of context

| API | Typical use | Lifetime |
| --- | --- | --- |
| `Enrich.WithProperty` | Application, environment | Root logger |
| `Bind` | Object, component, operation | Returned view |
| `PushScope` | Request and nested async work | `using` in the async flow |

## Bound context

`Bind` returns a new `Logger` sharing the root and sinks, with an independent
property set. It does not mutate the original view:

<!-- verify: en-bind-context -->
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

Only the third event contains `CustomerId` and `Region`. Chained `Bind` inherits
prior bound properties. Disposing a view does not close its parent or root;
disposing the root invalidates every view.

## Request scopes

`PushScope` uses `AsyncLocal` and survives `await`. Parallel flows do not overwrite
each other's scopes:

<!-- verify: en-async-scopes -->
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

Each request's `Expected` matches its `RequestId`; the final event has no scope
properties. Child operations inherit context when ExecutionContext flows. The
library does not restore explicitly suppressed context flow.

## Name precedence

From lowest to highest, with case-sensitive names:

```text
WithProperty → outer scopes → inner scopes → Bind → template arguments
```

<!-- verify: en-context-precedence -->
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

Messages in order: `outer`, `inner`, `bound`, `event`, `outer`, `global`.

## Disposal order and snapshots

Dispose scopes in reverse creation order. Disposing an outer scope first throws
`InvalidOperationException` and preserves the chain. After disposing the inner
scope, the outer can close normally. Repeated disposal of an already removed
scope in the same context has no effect.

Disposing an inherited scope in a child ExecutionContext does not remove its
parent's scope or prevent parent cleanup. Prefer lexical `using` blocks.
Values are captured during `Bind` / `PushScope`; complex-value serialization errors
throw directly from those calls. Independent roots have independent `AsyncLocal`
chains; bound views share their root's chain.

## Static facade

`Log.Bind(name, value)` and `Log.PushScope(name, value)` use the current process root.
Dictionary overloads exist only on `Logger`. `Log.Configure` replaces the root;
old scopes and views still refer to the old instance. Configure once at startup.
