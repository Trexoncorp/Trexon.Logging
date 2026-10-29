# Fluent-конфигурация

[Русский](CONFIGURATION.md) · [English](en/CONFIGURATION.md) · [Deutsch](de/CONFIGURATION.md)

[Оглавление](README.md) · [Назад](GETTING_STARTED.md) · [Далее](MESSAGES.md)

## Как строится цепочка

`Logger.Create` передаёт callback объект `LoggerConfiguration`. Методы основного
builder возвращают его же. `Enrich.WithProperty` / `WithCaller` и `WriteTo.Console`
/ `File` / `Sink` также возвращают основной builder. Внутри callback конкретного
получателя методы возвращают его конфигурацию.

<!-- verify: configuration -->
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

logger.Debug("Диагностика {OrderId}", 42); // Только файл.
logger.Warning("Повтор обработки {OrderId}", 42); // Консоль и файл.
```

Если ни один sink не добавлен, `Create` автоматически добавляет консоль.
Если добавлен только файл или собственный sink, скрытого консольного sink нет.

## Значения по умолчанию

| Параметр | Значение |
| --- | --- |
| Общий минимальный уровень | `Information` |
| Уровень конкретного sink | `Trace` |
| Часы | `TimeProvider.System`, вызов `GetUtcNow()` |
| Глобальные свойства | Пустой набор |
| Caller capture | Выключен |
| Глобальные и локальные фильтры | Отсутствуют |
| Регулярки | Отсутствуют |
| Диагностика сбоев | Короткая запись в `Console.Error` |
| Консоль | `Console.Error`, текст, `ConsoleColorMode.Auto` |
| Текстовая дата | `yyyy-MM-dd HH:mm:ss.fff zzz`, зона UTC |
| Исключение | Подробности включены |
| Отображение caller | Включено, если событие содержит caller |
| Владение собственным sink | `disposeWithLogger: false` |

## Уровни

| Значение | Число | Короткий метод | Текстовая метка |
| --- | --- | --- | --- |
| `Trace` | 0 | `Trace` | `TRC` |
| `Debug` | 1 | `Debug` | `DBG` |
| `Information` | 2 | `Info` | `INF` |
| `Warning` | 3 | `Warning` | `WRN` |
| `Error` | 4 | `Error` | `ERR` |
| `Critical` | 5 | `Critical` | `CRT` |
| `None` | 6 | — | События не создаются |

Событие должно пройти общий порог **и** порог получателя. Локальный `Trace`
не включает диагностику, отсечённую общим `Information`. `None` отключает
логгер целиком либо отдельный sink. Неопределённое значение enum отклоняется
с `ArgumentOutOfRangeException`.

Отключение порога относится к записи событий. Зарегистрированный файловый sink
всё равно открывается при Create, даже если общий порог равен None.

`IsEnabled(level)` учитывает общий порог, наличие подходящего sink и закрытое
состояние. Фильтры в нём не выполняются, поэтому `true` не гарантирует фактическую
запись. На закрытом логгере для корректного уровня возвращается `false`.

Проверка полезна перед вычислением дорогого аргумента:

```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.Debug("Диагностика {Snapshot}", BuildExpensiveSnapshot());
}
```

Это фрагмент приложения: `BuildExpensiveSnapshot` определяет вызывающий код.
У обычного вызова аргументы C# вычисляются до входа в `Debug`, даже если он
позже пропустит событие. Автоматического lazy API в MVP нет.

## Фильтры

Глобальный `Filter` получает сформированное и обработанное событие. `false`
останавливает dispatch во все sinks. Несколько фильтров объединяются через AND
с коротким замыканием. Фильтр sink действует только на этот sink.

<!-- verify: filters -->
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

Фильтры работают после снимков, маскирования, форматирования сообщения и
caller capture. Они подходят для отбора по содержимому. Для дешёвого
отключения уровня используйте `MinimumLevel`.

Исключение глобального фильтра — ошибка стадии `Pipeline`, событие отбрасывается.
Исключение фильтра sink влияет на конкретный sink; остальные продолжают запись.
Предикаты и другие пользовательские callbacks выполняются синхронно.

## Снимок настроек

После `Create` работают сохранённые значения конфигурации. Изменение builder
не перенастраивает существующий экземпляр. Параметры встроенного sink фиксируются
при его регистрации; свойства основного builder фиксируются при создании ядра.
`WithProperty` с тем же именем заменяет предыдущее значение.

Делегаты и переданный пользовательский sink остаются внешними объектами:
состояние, захваченное closure, не клонируется. Настраивайте builder в одном
потоке до начала записи. Автоматического чтения `appsettings.json` и hot reload
в текущем API нет; приложение может прочитать значения и передать их в builder.

## Собственные часы

`WithTimeProvider` позволяет использовать предсказуемое время без зависимости
от текущей даты. Реализация должна возвращать UTC из `GetUtcNow()`:

<!-- verify: fixed-clock -->
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

Вывод: `2026-10-29 12:00:00 +00:00 [INF] Fixed time`.
Timestamp фиксируется при подготовке события после обработки его свойств.
Зона текстового вывода настраивается отдельно: [форматы](OUTPUT.md).
