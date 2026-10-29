using Trexon.Logging;

// Аргумент --demo-error включает демонстрацию записи перехваченного исключения.
var demonstrateError = args.Contains("--demo-error", StringComparer.Ordinal);
Console.WriteLine("Trexon.Logging: консольный пример записи логов.");

// Файл создаётся в папке сборки приложения, например bin/Debug/net8.0/logs/.
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "sample.jsonl");

// Create собирает логгер из fluent-настроек. using закрывает его и файловый writer при выходе.
using var logger = Logger.Create(config => config
    // Пропускает Debug и более высокие уровни: Information, Warning, Error, Critical.
    .MinimumLevel(LogLevel.Debug)
    // Добавляет сведения о месте вызова: метод, путь к исходнику и номер строки.
    // Путь и строка зависят от доступности отладочной информации (PDB).
    .Enrich.WithCaller()
    // Добавляет постоянное свойство Application ко всем событиям этого логгера.
    .Enrich.WithProperty("Application", "Trexon.Sample")
    // Маскирует токены по регулярному выражению до передачи события в вывод.
    .Redact(@"Bearer\s+[^\s""}]+", "Bearer ***")
    // Обрабатывает внутренние сбои логирования, например ошибку записи файла.
    // Ошибки самого приложения передаются отдельно через logger.Error(...).
    .OnError(failure => Console.Error.WriteLine($"Ошибка логирования: {failure.Stage}"))
    // Выводит текст в Console.Error. Timestamp задаёт формат времени (по умолчанию UTC),
    // Colors автоматически включает ANSI-цвета в интерактивной консоли.
    .WriteTo.Console(console => console.Timestamp("HH:mm:ss.fff").Colors())
    // Добавляет второй вывод: UTF-8 файл с одним JSON-событием на строку.
    // Каталог создаётся автоматически; существующий файл дополняется.
    .WriteTo.File(logPath, file => file.Json()));

// Info записывает событие уровня Information сразу в оба настроенных вывода.
logger.Info("Приложение запущено");

// PushScope временно добавляет RequestId ко всем записям через этот логгер и его Bind-представления.
// Контекст передаётся во вложенные async-вызовы и снимается при выходе из using.
using (logger.PushScope("RequestId", Guid.NewGuid()))
// Bind создаёт представление с закреплённым Component, не меняя исходный logger.
// Его using закрывает только представление; основной логгер продолжает работать.
using (var orderLogger = logger.Bind("Component", "Orders"))
{
    // Debug записывает диагностическое сообщение. {Authorization} связывается с аргументом,
    // сохраняется отдельным свойством, а Redact заменяет секрет на Bearer ***.
    orderLogger.Debug("Authorization: {Authorization}", "Bearer demo-secret");
    // Именованные параметры сохраняются в Properties со своими типами.
    // Формат :0.00 влияет на текст суммы, а в JSON Amount остаётся числом.
    orderLogger.Info("Заказ {OrderId} на сумму {Amount:0.00} создан", 42, 199.9m);
    // Объект сохраняется как JSON-структура в Properties и отображается JSON-текстом в сообщении.
    // Контекст Application, RequestId и Component виден в JSON; текстовый вывод не перечисляет его автоматически.
    orderLogger.Info("Покупатель {Customer}", new { Name = "Антон", Items = 2 });
    if (demonstrateError)
    {
        Console.WriteLine("Демонстрация: следующее исключение будет перехвачено и записано в лог.");
        try
        {
            throw new InvalidOperationException("Демонстрационная ошибка оплаты");
        }
        catch (Exception exception)
        {
            // Error записывает сообщение вместе с типом, текстом и стеком исключения.
            // Исключение уже перехвачено: после записи программа продолжает выполнение.
            orderLogger.Error(exception, "Ошибка заказа {OrderId}", 42);
        }
    }
}

// Здесь scope снят: RequestId и свойство Bind-представления Component уже отсутствуют.
logger.Info("Контекст запроса завершён");
// Flush просит все выводы сбросить буферы. Файл закроется при Dispose через using выше.
// Файловый вывод уже использует AutoFlush; явный вызов показывает API и полезен для выводов с накоплением данных.
logger.Flush();
Console.WriteLine("Пример завершён. Сообщения записаны в консоль и JSON-файл.");
Console.WriteLine($"JSON-лог: {logPath}");
if (!demonstrateError)
{
    Console.WriteLine("Для демонстрации записи исключения добавьте к команде запуска: -- --demo-error");
}
