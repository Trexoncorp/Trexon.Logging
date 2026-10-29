# Справочник публичного API

[Русский](API.md) · [English](en/API.md) · [Deutsch](de/API.md)

[Оглавление](README.md) · [Использование](GETTING_STARTED.md) · [Расширение](EXTENDING.md)

Namespace всех типов: `Trexon.Logging`. Ниже перечислен авторский публичный
контракт MVP. Блоки — декларации сигнатур, а не полные программы. Nullable `?`
показывает допустимый null. Доступность конструкторов моделей указана отдельно.
Автогенерируемые record-методы равенства и члены
базовых типов .NET отдельно не перечисляются.

## Logger

`public sealed class Logger : IDisposable`. Публичного конструктора нет;
root создаётся через `Create`, связанное view — через `Bind`.

```csharp
public static Logger Create(Action<LoggerConfiguration>? configure = null);
public bool IsEnabled(LogLevel level);

public Logger Bind(string name, object? value);
public Logger Bind(IReadOnlyDictionary<string, object?> properties);
public IDisposable PushScope(string name, object? value);
public IDisposable PushScope(IReadOnlyDictionary<string, object?> properties);

public void Write(LogLevel level, string messageTemplate, params object?[] values);
public void Write(LogLevel level, Exception exception, string messageTemplate,
    params object?[] values);

public void Trace(string messageTemplate, params object?[] values);
public void Debug(string messageTemplate, params object?[] values);
public void Info(string messageTemplate, params object?[] values);
public void Warning(string messageTemplate, params object?[] values);
public void Error(string messageTemplate, params object?[] values);
public void Error(Exception exception, string messageTemplate, params object?[] values);
public void Critical(string messageTemplate, params object?[] values);
public void Critical(Exception exception, string messageTemplate, params object?[] values);

public void Flush();
public void Dispose();
```

| Операция | Контракт |
| --- | --- |
| Create | Собирает настройки и открывает sinks. Без sinks добавляет Console |
| IsEnabled | Проверяет thresholds/состояние; пользовательские фильтры не вызывает |
| Bind | Снимок дополнительных свойств в view с общим root |
| PushScope | Снимок свойств в текущий async flow, возвращает scope lease |
| Write / короткие методы | Синхронная подготовка и dispatch события |
| Flush | Сброс всех distinct sinks, без закрытия |
| Dispose root | Flush и закрытие owned sinks, инвалидирует views |
| Dispose view | Закрывает только view |

Валидация и работа с закрытым logger могут выбрасывать исключения.
Pipeline/sink failures после входа в запись обрабатываются через `OnError`.
Подробная [таблица ошибок](ERRORS_AND_LIFETIME.md).

## Log

`public static class Log` — процессный фасад.

```csharp
public static void Configure(Action<LoggerConfiguration> configure);
public static bool IsEnabled(LogLevel level);
public static Logger Bind(string name, object? value);
public static IDisposable PushScope(string name, object? value);

public static void Write(LogLevel level, string template, params object?[] values);
public static void Write(LogLevel level, Exception exception, string template,
    params object?[] values);
public static void Trace(string template, params object?[] values);
public static void Debug(string template, params object?[] values);
public static void Info(string template, params object?[] values);
public static void Warning(string template, params object?[] values);
public static void Error(string template, params object?[] values);
public static void Error(Exception exception, string template, params object?[] values);
public static void Critical(string template, params object?[] values);
public static void Critical(Exception exception, string template, params object?[] values);

public static void Flush();
public static void Shutdown();
```

Начальный root использует defaults `Logger.Create()`. Configure заменяет root
и закрывает предыдущий. Shutdown закрывает текущий; новый Configure может
возобновить работу. Dictionary-перегрузок Bind/PushScope у Log пока нет.

## LoggerConfiguration

`public sealed class LoggerConfiguration`. Есть публичный конструктор без
параметров; для работы с Logger конфигурацию получает callback Create.
Отдельного метода Build на ней нет.

```csharp
public LoggerConfiguration();
public LoggerEnrichmentConfiguration Enrich { get; }
public LoggerSinkConfiguration WriteTo { get; }

public LoggerConfiguration MinimumLevel(LogLevel level);
public LoggerConfiguration Filter(Func<LogEvent, bool> predicate);
public LoggerConfiguration Redact(string pattern, string replacement = "***",
    RegexOptions options = RegexOptions.None, TimeSpan? timeout = null);
public LoggerConfiguration OnError(Action<LoggingFailure> handler);
public LoggerConfiguration WithTimeProvider(TimeProvider timeProvider);
```

| Метод | Default / эффект |
| --- | --- |
| MinimumLevel | Начальный уровень Information; None отключает всё |
| Filter | Добавляет глобальный AND-предикат после маскирования |
| Redact | Добавляет Regex.Replace-правило; timeout null = 100 мс |
| OnError | Заменяет обработчик сбоев; его исключения подавляются |
| WithTimeProvider | Заменяет TimeProvider.System |

`RegexOptions` находится в `System.Text.RegularExpressions`.
Настройки фиксируются при создании ядра; builder не является runtime controller.

## LoggerEnrichmentConfiguration

`public sealed class LoggerEnrichmentConfiguration`. Создаётся библиотекой;
публичного конструктора нет. Методы возвращают основной LoggerConfiguration.

```csharp
public LoggerConfiguration WithCaller(bool enabled = true);
public LoggerConfiguration WithProperty(string name, object? value);
```

WithCaller включает анализ стека для разрешённых событий. WithProperty добавляет
глобальное свойство; повторное имя заменяет значение. Имя не может быть пустым
или состоять из пробелов. Подробнее: [контекст](CONTEXT.md).

## LoggerSinkConfiguration

`public sealed class LoggerSinkConfiguration`. Доступна через WriteTo,
публичного конструктора нет.

```csharp
public LoggerConfiguration Console(Action<ConsoleSinkConfiguration>? configure = null);
public LoggerConfiguration File(string path,
    Action<FileSinkConfiguration>? configure = null);
public LoggerConfiguration Sink(ILogSink sink,
    LogLevel minimumLevel = LogLevel.Trace,
    Func<LogEvent, bool>? filter = null,
    bool disposeWithLogger = false);
```

Console фиксирует выбранный writer и не передаёт владение стандартным потоком.
File открывается при создании ядра и принадлежит logger. Sink регистрирует
внешний объект; default-владелец — приложение. Каждый вызов добавляет регистрацию.

## SinkConfiguration<TSelf>

Общий базовый тип:

```csharp
public abstract class SinkConfiguration<TSelf>
    where TSelf : SinkConfiguration<TSelf>
{
    public TSelf MinimumLevel(LogLevel level);
    public TSelf Timestamp(string? format);
    public TSelf TimeZone(TimeZoneInfo timeZone);
    public TSelf Json(bool enabled = true);
    public TSelf ExceptionDetails(bool enabled = true);
    public TSelf Caller(bool enabled = true);
    public TSelf Filter(Func<LogEvent, bool> predicate);
}
```

| Метод | Начальное значение | Что меняет |
| --- | --- | --- |
| MinimumLevel | Trace | Дополнительный порог sink |
| Timestamp | yyyy-MM-dd HH:mm:ss.fff zzz | Формат текста; null скрывает дату |
| TimeZone | TimeZoneInfo.Utc | Зону текстового timestamp |
| Json | false | Один JSON-объект на строку |
| ExceptionDetails | true | Stack/children в выводе исключения |
| Caller | true | Отображение уже захваченного caller |
| Filter | Нет | Добавляет локальный AND-предикат |

Timestamp/TimeZone не меняют JSON timestamp. Caller(true) не включает глобальный
capture; для этого нужен Enrich.WithCaller().

## ConsoleSinkConfiguration

`public sealed class ConsoleSinkConfiguration : SinkConfiguration<ConsoleSinkConfiguration>`.
Есть публичный конструктор без параметров; обычно экземпляр приходит в callback Console.

```csharp
public ConsoleSinkConfiguration Colors(ConsoleColorMode mode = ConsoleColorMode.Auto);
public ConsoleSinkConfiguration Writer(TextWriter writer);
```

Наследует все методы SinkConfiguration с возвратом ConsoleSinkConfiguration.
Writer по умолчанию — Console.Error; null не допускается. Colors влияет только
на текст. Владение переданным writer остаётся у вызывающего кода.

## FileSinkConfiguration

`public sealed class FileSinkConfiguration : SinkConfiguration<FileSinkConfiguration>`.
Есть публичный конструктор без параметров. Объявляет только наследуемые
настройки; методов ротации, retention, compression или Colors в нём нет.
Путь передаётся в WriteTo.File, отдельного setter пути в этом типе нет.

## LogLevel

```csharp
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}
```

None — служебный порог, а не уровень выпускаемого события.
Короткий метод Info соответствует Information.

## ConsoleColorMode

```csharp
public enum ConsoleColorMode
{
    Auto = 0,
    Always = 1,
    Never = 2
}
```

Auto проверяет интерактивность текущего стандартного writer; для произвольного
TextWriter по умолчанию цветов не будет. [Точная палитра](OUTPUT.md).

## LogEvent

`public sealed class LogEvent`. Создаётся pipeline; публичного конструктора нет.

```csharp
public DateTimeOffset Timestamp { get; }
public LogLevel Level { get; }
public string MessageTemplate { get; }
public string Message { get; }
public IReadOnlyDictionary<string, object?> Properties { get; }
public ExceptionInfo? Exception { get; }
public CallerInfo? Caller { get; }
```

Событие фиксируется до dispatch. Его словарь доступен только для чтения;
сложные значения — независимые JsonElement. Данные, к которым применяются
Redact-правила, уже обработаны. Exception содержит snapshot, а не исходный объект.

## CallerInfo

Публичный позиционный record с конструктором:

```csharp
public sealed record CallerInfo(string MemberName, string? FilePath, int LineNumber);
```

Генерирует init-свойства MemberName, FilePath и LineNumber. FilePath может быть
null, LineNumber — 0. Захват использует стек и зависит от PDB/оптимизации;
обычные строки подвергаются Redact-правилам.

## ExceptionInfo

`public sealed class ExceptionInfo`. Публичного конструктора нет.

```csharp
public string Type { get; }
public string Message { get; }
public string? StackTrace { get; }
public IReadOnlyList<ExceptionInfo> InnerExceptions { get; }
```

Тип/сообщение/стек — снимки после Redact. Children включают AggregateException
и ограничены глубиной. [Поведение исключений](ERRORS_AND_LIFETIME.md).

## ILogSink

```csharp
public interface ILogSink : IDisposable
{
    void Emit(LogEvent logEvent);
    void Flush();
}
```

Также нужно реализовать Dispose из IDisposable. Вызовы от одного root
сериализованы. Общий sink для разных roots требует собственной синхронизации.
[Полный пример реализации](EXTENDING.md).

## LoggingFailure

Публичный позиционный record:

```csharp
public sealed record LoggingFailure(string Stage, Exception Exception);
```

Stage и Exception — init-свойства. Callback получает исходное техническое
исключение, которое автоматически не маскируется. Формы Stage и поведение
OnError описаны в [руководстве по ошибкам](ERRORS_AND_LIFETIME.md).
