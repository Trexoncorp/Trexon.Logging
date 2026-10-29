# Контекст: глобальные свойства, Bind и PushScope

[Русский](CONTEXT.md) · [English](en/CONTEXT.md) · [Deutsch](de/CONTEXT.md)

[Оглавление](README.md) · [Назад](MESSAGES.md) · [Далее](OUTPUT.md)

Контекст добавляет свойства ко всем событиям, чтобы не повторять их в каждом
шаблоне. В JSON они находятся в `properties`. Стандартный текстовый формат
показывает контекстное значение, когда оно упомянуто в шаблоне; весь словарь
свойств автоматически к строке не приписывается.

## Три области действия

| Способ | Когда использовать | Время жизни |
| --- | --- | --- |
| `Enrich.WithProperty` | Приложение, среда, имя компонента | Весь root-логгер |
| `logger.Bind` | Конкретный объект, компонент, операция | Возвращённое view |
| `logger.PushScope` | Запрос и вложенные async-операции | Область `using` в async-потоке |

## Постоянный контекст через Bind

`Bind` создаёт новый `Logger`, разделяющий sinks и ядро исходного. Его набор
свойств независим; вызов не меняет исходное view.

<!-- verify: bind-context -->
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

Только третья запись содержит `CustomerId` и `Region`. Второе `Bind` наследует
свойства первого. Закрытие `customer` не закрывает `orders` или `root`.
Закрытие root делает все связанные views недействительными.

## Область запроса

`PushScope` использует `AsyncLocal`: контекст передаётся дочерним async-операциям
и сохраняется через `await`. Разные параллельные операции не переписывают
свойства друг друга.

<!-- verify: async-scopes -->
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

У каждой из первых трёх записей `Expected` совпадает с её `RequestId`.
Последняя запись не содержит свойств scope. При передаче ExecutionContext
дочерняя задача наследует активную область. Отключённый вручную flow контекста
не восстанавливается библиотекой.

## Приоритет одинаковых имён

От меньшего приоритета к большему:

```text
WithProperty → внешние scopes → внутренние scopes → Bind → аргументы шаблона
```

<!-- verify: context-precedence -->
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

Сообщения по порядку: `outer`, `inner`, `bound`, `event`, `outer`, `global`.
Имена чувствительны к регистру.

## Порядок закрытия и снимки

Scope закрывается в обратном порядке создания. При попытке закрыть внешнюю
область раньше внутренней возникает `InvalidOperationException`, текущая цепочка
сохраняется. После закрытия внутренней внешнюю можно закрыть корректно.
Повторный `Dispose` уже снятого scope в том же контексте ничего не меняет.

Снятие унаследованного scope в дочернем ExecutionContext не снимает его у
родителя и не мешает родительскому `Dispose`. Такой сценарий проверяется
тестами, но для обычного кода проще держать scope в своей лексической области.

Значения фиксируются при вызове `Bind` / `PushScope`. Ошибка сериализации
сложного значения в этих методах выбрасывается вызывающему коду сразу.
Root-логгеры имеют независимые цепочки `AsyncLocal`, а их bound views разделяют
цепочку своего root.

## Работа через статический Log

`Log.Bind(name, value)` и `Log.PushScope(name, value)` действуют на текущий
процессный root. Dictionary-перегрузки есть у `Logger`; у статического фасада
они в MVP не объявлены. `Log.Configure` заменяет root: прежние views и scopes
относятся к прежнему экземпляру. Настраивайте фасад при запуске приложения.
