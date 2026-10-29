# Исключения, сбои логирования и время жизни

[Русский](ERRORS_AND_LIFETIME.md) · [English](en/ERRORS_AND_LIFETIME.md) · [Deutsch](de/ERRORS_AND_LIFETIME.md)

[Оглавление](README.md) · [Назад](REDACTION.md) · [Далее](EXTENDING.md)

## Записать прикладное исключение

Передайте исключение отдельным аргументом. Специальные перегрузки есть у `Error`
и `Critical`, а `Write` позволяет выбрать любой стандартный уровень:

<!-- verify: exceptions -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .WriteTo.Console(console => console.Timestamp(null).ExceptionDetails()));
try
{
    throw new InvalidOperationException("Import failed",
        new ArgumentException("Invalid customer"));
}
catch (Exception exception)
{
    logger.Error(exception, "Ошибка импорта {BatchId}", 42);
    logger.Write(LogLevel.Warning, exception, "Попытка {Attempt} завершилась ошибкой", 1);
}
```

`Error("Failure {Exception}", exception)` передаёт исключение как обычное
свойство. Для гарантированного exception snapshot используйте перегрузку,
которая начинается с `Exception`.

Logger не устанавливает глобальные exception handlers и не перехватывает
исключения бизнес-кода сам. Решение о `catch` и повторном `throw` принимает
приложение. Передача исключения не создаёт новый stack trace: читается тот,
который есть у исходного объекта.

## Снимок ExceptionInfo

В событии сохраняется `ExceptionInfo`, а не ссылка на исходный Exception:

| Поле | Содержимое |
| --- | --- |
| `Type` | Полное имя типа исключения |
| `Message` | Сообщение |
| `StackTrace` | Исходный стек или null |
| `InnerExceptions` | Список снимков дочерних исключений |

У `AggregateException` сохраняются все непосредственные children, у обычного
исключения — `InnerException`, если он есть. Глубина ограничена 16 переходами
от корня; на этой границе новые children не добавляются. `Exception.Data`
и произвольные дополнительные свойства исключения не включаются.

`ExceptionDetails(false)` у sink выводит только Type и Message. При этом
`AggregateException.Message` сам по себе может уже содержать сообщения children.
Полный snapshot в `LogEvent` остаётся доступен другим sinks.

## Ошибки самой библиотеки

`OnError` получает техническую ошибку записи. Его стоит подключать сразу,
если отсутствие логов должно быть заметно:

<!-- verify: error-handler -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .OnError(failure => Console.Error.WriteLine(
        $"Logging failure: {failure.Stage}, {failure.Exception.GetType().Name}"))
    .WriteTo.Console(console => console.Timestamp(null)));

logger.Info("Invalid format {Price:Q}", 12.5m); // Ошибка Pipeline, событие отброшено.
logger.Info("Следующая запись работает");
```

| Где возникла ошибка | Что происходит |
| --- | --- |
| Неверный аргумент API / некорректная конфигурация | Исключение вызывающему коду |
| Открытие файла, создание logger, снимок глобального свойства | Ошибка из `Logger.Create` |
| Снимок свойства в `Bind` или `PushScope` | Ошибка из соответствующего метода |
| Подготовка события, regex, формат аргумента, глобальный фильтр | `OnError`, событие полностью отброшено |
| Фильтр конкретного sink или его `Emit` | `OnError`, остальные sinks продолжают |
| `Flush` / `Dispose` sink | `OnError`, обработка остальных ресурсов продолжается |
| Сам обработчик `OnError` | Его исключение подавляется |
| Использование закрытого logger/view | `ObjectDisposedException` |

Ошибки из `Create` не перенаправляются в `OnError` как обычные event failures.
Если открытие очередного sink не удалось, уже созданные owned sinks закрываются,
а исходная ошибка создания передаётся наружу.

`LoggingFailure.Stage` имеет одну из форм:

- `Pipeline`;
- имя типа sink при ошибке `Emit`/его фильтра;
- `Flush:<имя типа sink>`;
- `Dispose:<имя типа sink>`.

Встроенные sinks имеют тип `TextOutputSink`, поэтому stage не различает пути
разных файлов. Для детального контроля можно использовать собственный sink.
При отсутствии `OnError` в `Console.Error` отправляется короткая диагностика
со stage и именем типа технического исключения. Его сообщение автоматически
не печатается. `LoggingFailure.Exception` не маскируется правилами события.

## Кто владеет ресурсами

| Объект | Кто закрывает |
| --- | --- |
| Root из `Logger.Create` | Вызывающий код через `using` / `Dispose` |
| File sink / его stream | Root-логгер |
| Console.Error / Console.Out | Логгер их не закрывает |
| Writer, переданный в Console | Вызывающий код |
| Свой `ILogSink`, default | Вызывающий код |
| Свой sink с `disposeWithLogger: true` | Root-логгер |
| Bound view из `Bind` | Его можно закрыть отдельно, root продолжает работать |
| Scope из `PushScope` | Обратный порядок `using` в том же async-потоке |

Logger сбрасывает и caller-owned sinks, но не вызывает их `Dispose`.
Один и тот же sink, зарегистрированный несколько раз, получит несколько `Emit`
на событие, но flush/dispose одного экземпляра выполняется один раз на операцию.

## Flush и Dispose

Вывод может сначала накапливать записи в памяти, чтобы отправить их одной
операцией. `Flush()` просит его передать накопленные данные дальше. Например,
пользовательский sink может собрать несколько событий перед записью в поток.
Метод не создаёт новое событие и не закрывает логгер: после него можно писать
следующие сообщения.

У встроенного файлового вывода включён `StreamWriter.AutoFlush`, поэтому
записи автоматически сбрасываются из буфера writer. В нашем консольном примере
явный `logger.Flush()` показывает API; обычно достаточно `using`, который
при завершении вызывает `Dispose()` с flush и закрытием файла.
Ни `Flush`, ни AutoFlush не обещают физическую запись на носитель при аварии.

`Flush()` синхронно сбрасывает получателей и оставляет logger открытым.
`Dispose()` root помечает ядро закрытым, выполняет flush, затем закрывает owned
sinks. Повторный Dispose безопасен. Flush, записи, Bind и PushScope закрытого
логгера выбрасывают `ObjectDisposedException`; `IsEnabled` возвращает false.

При закрытии bound view его root и другие views продолжают работу. При закрытии
root все views становятся недействительными. Отдельного счётчика ссылок,
который продлевает жизнь root, нет.

Встроенный file writer использует AutoFlush, но явное завершение всё равно
нужно для освобождения файла. Гарантии фоновой очереди / `FlushAsync` /
`DisposeAsync` отсутствуют: текущий pipeline полностью синхронный.

## Параллельная работа и callbacks

Запись, flush и закрытие одного root синхронизируются одной блокировкой.
Получатели наблюдают одинаковый порядок событий; порядок поступления задач
в блокировку не является обещанием порядка запуска этих задач.

Медленный sink, сериализация большого значения, regex или пользовательский
фильтр задерживают вызвавший поток. Обработчики должны быть короткими.

Повторная запись в тот же root из его sink/фильтра/OnError пропускается для
защиты от рекурсии. Вложенный Flush во время dispatch/flush ничего не делает.
Dispose root-логгера из собственного callback запрещён и выбрасывает InvalidOperationException;
если он возник внутри sink, он будет обработан как ошибка этого sink.
Не стройте взаимные вызовы между двумя синхронными логгерами из их callbacks:
разные root имеют независимые блокировки.

## Процессный Log

`Log.Configure` сначала создаёт replacement, затем под своей блокировкой
закрывает предыдущий root и заменяет его. Ошибка создания replacement сохраняет
предыдущий работающий logger. Старые Bind-представления после успешной замены
закрыты вместе с root.

Такой порядок может помешать повторному открытию того же файла, пока предыдущий
root ещё держит его writer. Практический режим — Configure один раз при запуске.
Для явного завершения старого root перед созданием нового используйте Shutdown
в момент, когда пользователи прежнего логгера уже закончили работу.

`Log.Shutdown()` закрывает текущий root; дальнейшая запись через фасад даст
ObjectDisposedException. `Log.Configure(...)` может установить новый root и
после Shutdown. Открытые views сами по себе не переключаются на replacement.
