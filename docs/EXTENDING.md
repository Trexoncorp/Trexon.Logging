# Собственный получатель ILogSink

[Русский](EXTENDING.md) · [English](en/EXTENDING.md) · [Deutsch](de/EXTENDING.md)

[Оглавление](README.md) · [Назад](ERRORS_AND_LIFETIME.md) · [Справочник API](API.md)

`ILogSink` — публичная точка расширения для собственных приёмников и форматов.
Он получает уже сформированный и обработанный `LogEvent`. Встроенные текстовые
форматтеры — внутренние классы; публичного `ILogFormatter` и произвольного
enricher callback в MVP нет.

## Минимальный получатель

Полная программа с собственным sink, сохраняющим события в памяти:

<!-- verify: custom-sink -->
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

В sink придёт одна Warning-запись, `Token` будет равен `***`. Логгер не закрывает
переданный sink по умолчанию; в примере это делает внешний `using var sink`.
Список хранится только в этом примере — библиотека сама не накапливает историю.

## Передача владения

Точная регистрация:

```csharp
config.WriteTo.Sink(
    sink,
    minimumLevel: LogLevel.Trace,
    filter: null,
    disposeWithLogger: true);
```

Это фрагмент callback настройки. При `true` root сначала вызовет Flush,
затем Dispose. При `false` Flush также вызывается, а закрывает sink приложение.
Продумайте единственного владельца и порядок завершения.

Если один sink используется несколькими root-логгерами, они могут вызвать
Emit параллельно. Собственная блокировка в примере покрывает такой сценарий.
При этом владение ресурсами между roots автоматически не согласуется: закрытие
одним владельцем не должно происходить, пока другой ещё пишет.

## Доступ к данным

`LogEvent` имеет readonly-свойства: Timestamp, Level, MessageTemplate, Message,
Properties, Exception, Caller. Словарь доступен для чтения, сложные значения
зафиксированы как JsonElement. Сохранённый event не зависит от последующей
мутации исходной пользовательской коллекции.

`Exception` — снимок ExceptionInfo. Его тип, сообщение, stack trace и children
можно представить в своём формате. Ссылка на исходный Exception не выдаётся.
`Caller` может отсутствовать, а FilePath — быть null даже при включённом capture.

`ExceptionDetails(false)` и `Caller(false)` относятся к встроенным форматтерам.
Структурированный custom sink сам выбирает, какие поля выводить; переданный ему
event содержит захваченные данные после маскирования.

## Ошибки и стоимость работы

Emit выполняется синхронно под блокировкой root. Исключение sink будет передано
в `OnError`, а другие sinks продолжат запись. Встроенного retry и повторной
доставки события нет. При частичной внешней записи и последующем исключении
библиотека не может откатить уже отправленные данные.

Flush должен сбрасывать буферы своего получателя. Dispose освобождает его
ресурсы. Оба метода вызываются под блокировкой root, их ошибки изолируются.
Из методов sink не вызывайте собственный logger и его операции жизненного
цикла: повторные записи пропускаются, а Dispose при dispatch запрещён.

Новый sink с сетевой отправкой потребует отдельно определить timeout, очередь,
ограничения памяти и поведение при недоступности адресата. Эти механизмы
не добавляются интерфейсом ILogSink автоматически.
