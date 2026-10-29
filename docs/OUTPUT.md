# Получатели и форматы вывода

[Русский](OUTPUT.md) · [English](en/OUTPUT.md) · [Deutsch](de/OUTPUT.md)

[Оглавление](README.md) · [Назад](CONTEXT.md) · [Далее](REDACTION.md)

Sink — получатель события. Встроенные Console/File используют общий форматтер
текста или JSON; пользовательский `ILogSink` получает структурированный
`LogEvent` напрямую. У каждого получателя свои порог и фильтры.

## Консоль и TextWriter

`WriteTo.Console()` использует `Console.Error`. Это отделяет логи от обычного
stdout-результата утилиты. Для другого потока передайте `Writer(Console.Out)`
или собственный `TextWriter`:

<!-- verify: captured-output -->
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

Переданный writer остаётся собственностью вызывающего кода. Логгер его
сбрасывает через `Flush`, но не закрывает. Выбранный writer фиксируется при
настройке sink: последующий `Console.SetError` не заменяет уже сохранённый writer.

## Текстовый формат

Порядок полей фиксирован в MVP:

```text
<timestamp> [<level>] <message> (<member> at <file>:<line>)
<exception type>: <exception message>
<stack trace и вложенные исключения>
```

Дата, caller и подробности исключения зависят от настроек и наличия данных.
Если файла нет, caller отображается как `(MemberName)`. В тексте используется
имя файла без каталогов. В JSON сохраняется полный доступный `filePath`.
Текстовое сообщение и исключение могут содержать переводы строк: для строгого
построчного машинного разбора используйте JSON Lines.

Полного пользовательского layout-шаблона и смены порядка полей пока нет.

## Время и часовой пояс

Событие хранит время `GetUtcNow()` своего `TimeProvider`. Настройки sink влияют
на представление текста:

| Метод | Результат |
| --- | --- |
| `Timestamp("HH:mm:ss.fff")` | Свой формат даты |
| `Timestamp(null)` | Убрать дату из текстовой строки |
| `TimeZone(TimeZoneInfo.Local)` | Локальная зона процесса |
| `TimeZone(TimeZoneInfo.Utc)` | UTC, значение по умолчанию |
| `Json()` | ISO timestamp события; текстовый формат/зона не применяются |

Формат проверяется при настройке с invariant culture. Собственную зону можно
передать через `TimeZoneInfo`, учитывая доступность её ID на целевой ОС.

## Цвета

`Colors(ConsoleColorMode.Auto)` — значение по умолчанию для консоли.

| Режим | Поведение |
| --- | --- |
| `Auto` | ANSI только для текущего Console.Out/Error, если поток не перенаправлен |
| `Always` | ANSI даже при перенаправлении и в собственном writer |
| `Never` | Обычный текст |

| Уровень | Цвет | ANSI SGR |
| --- | --- | --- |
| Trace | Серый | `90` |
| Debug | Cyan | `36` |
| Information | Зелёный | `32` |
| Warning | Жёлтый | `33` |
| Error | Красный | `31` |
| Critical | Жирный красный | `1;31` |

Окрашивается вся текстовая запись, в конце идёт reset `0`.
Собственной палитры в API пока нет. JSON и встроенный файловый sink не добавляют
ANSI-последовательности; `Colors(Always)` при `Json()` также не окрашивает JSON.

## Файловый sink

<!-- verify: file-output -->
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

- Полный путь вычисляется при вызове `WriteTo.File` относительно текущей папки
  процесса. Это не обязательно каталог приложения.
- Папка и stream создаются при `Logger.Create`, до первой записи. Ошибки
  открытия передаются вызывающему коду.
- Используются append, UTF-8 без BOM, `FileShare.Read`, `StreamWriter.AutoFlush`.
  Существующий файл не очищается; каждая запись заканчивается newline платформы.
- Логгер владеет файловым writer. `Dispose` освобождает его даже после отдельного
  сбоя flush; ошибки операций закрытия передаются в `OnError`.
- AutoFlush и `Flush` сбрасывают буферы writer. Это не отдельная гарантия
  физического сохранения на носитель при аварийном отключении.

Планируйте один активный writer на файл. Два root-логгера или процесса с одним
путём не образуют общий синхронизированный sink; открытие второго writer может
завершиться ошибкой sharing. Ротация, retention и compression пока отсутствуют.

## JSON Lines

`Json()` выдаёт один компактный JSON-объект на событие. Ниже он развёрнут для
чтения; фактический output не имеет отступов:

```json
{
  "timestamp": "2026-10-29T12:00:00+00:00",
  "level": "Information",
  "messageTemplate": "Order {Id}",
  "message": "Order 42",
  "properties": { "Id": 42 }
}
```

| Поле | Тип и назначение |
| --- | --- |
| `timestamp` | ISO-строка DateTimeOffset, обычно UTC `+00:00` |
| `level` | Полное имя enum: `Information`, `Warning` и т. д. |
| `messageTemplate` | Шаблон после маскирования |
| `message` | Готовая строка сообщения после маскирования |
| `properties` | Объект с типизированными значениями |
| `caller` | Необязательный объект `memberName`, `filePath`, `lineNumber` |
| `exception` | Необязательный объект `type`, `message`, `stackTrace`, `innerExceptions` |

Системные поля имеют camelCase. Имена ключей `properties` и полей вложенного
пользовательского JSON сохраняют свой регистр. Null-поля системных объектов
опускаются; null-значение свойства в словаре остаётся JSON null. Пустой массив
`innerExceptions` при подробном выводе сохраняется.

Числа остаются числами, сложные объекты — JSON-структурами; enum фиксируется
как строка. Guid, дата и TimeSpan сериализуются стандартными правилами
System.Text.Json. Кириллица и другие алфавиты выводятся читаемыми символами
как в JSON-файле, так и во вложенных объектах текстового сообщения.
Используется `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`: кавычки,
обратные слэши и управляющие символы продолжают экранироваться по правилам JSON.
Некоторые символы, включая emoji, могут сохраняться как `\uXXXX` или пары
таких последовательностей; JSON-парсер восстановит их без потерь.
Вывод предназначен для UTF-8 JSON; при вставке в HTML требуется HTML-экранирование.
Настройка JsonSerializerOptions через публичный API пока не предоставлена.

`ExceptionDetails(false)` оставляет тип/сообщение и убирает stack/children.
`Caller(false)` скрывает caller из этого sink, сохраняя его в самом событии.
Глобальный `WithCaller` при этом продолжает выполнять захват.

## Разбор JSON обычным приложением

<!-- verify: json-schema -->
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
