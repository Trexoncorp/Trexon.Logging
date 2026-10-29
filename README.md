# Trexon.Logging

[Русский](README.md) · [English](README.en.md) · [Deutsch](README.de.md)

Структурированное логирование для C# / .NET 8+: простой старт, fluent-настройка,
консоль с цветами, текстовые и JSON-файлы, контекст и маскирование данных.
Основная библиотека не имеет внешних NuGet-зависимостей. Лицензия — [MIT](LICENSE).

**Версия:** `0.1.0-alpha.1` — первый prerelease MVP. API может меняться до
версии 1.0. Документация описывает API этой версии.

## Начать с одной строки

<!-- verify: readme-basic -->
```csharp
using Trexon.Logging;

Log.Info("Приложение запущено");
Log.Info("Заказ {OrderId} создан", 42);
Log.Shutdown();
```

По умолчанию: `Console.Error`, уровень `Information`, время UTC, текстовый вывод.
Цвет включается автоматически для интерактивной консоли. `Shutdown` закрывает
процессный логгер при завершении приложения.

## Настроить отдельный экземпляр

<!-- verify: readme-configured -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create(config => config
    .MinimumLevel(LogLevel.Debug)
    .Enrich.WithCaller()
    .Enrich.WithProperty("Application", "Orders")
    .Redact(@"Bearer\s+\S+", "Bearer ***")
    .OnError(failure => Console.Error.WriteLine($"Сбой логирования: {failure.Stage}"))
    .WriteTo.Console(console => console.Timestamp("HH:mm:ss.fff").Colors())
    .WriteTo.File("logs/app.jsonl", file => file.Json()));

using (logger.PushScope("RequestId", "request-42"))
using (var orders = logger.Bind("Component", "Orders"))
{
    orders.Info("Заказ {OrderId} на сумму {Price:0.00}", 42, 199.9m);
    orders.Debug("Authorization: {Authorization}", "Bearer demo-token");
}
```

`Logger` владеет файловыми ресурсами и закрывает их через `Dispose`. Отдельные
экземпляры подходят для явного управления временем жизни и изолированных тестов.
Глобальный фасад `Log` удобен для небольших приложений и скриптов.

## Установка и проверка

Установить первый prerelease из NuGet:
Для ссылки на проект или локального пакета см. [пошаговую установку](docs/GETTING_STARTED.md).

```sh
dotnet add package Trexon.Logging --version 0.1.0-alpha.1
```

Для разработки нужен .NET SDK 10 и runtime .NET 8. Из корня репозитория:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build
python3 scripts/check-docs.py
```

Проверки документации компилируют и запускают помеченные C#-примеры во временных
папках. Инструкции с Git-тегами и публикацией при этом не выполняются.

## Документация

Начальная точка — [оглавление всей документации](docs/README.md).

| Задача | Руководство |
| --- | --- |
| Установить библиотеку и получить первый лог | [Быстрый старт](docs/GETTING_STARTED.md) |
| Настроить уровни, фильтры и время | [Fluent-конфигурация](docs/CONFIGURATION.md) |
| Писать шаблоны и передавать данные | [Сообщения и свойства](docs/MESSAGES.md) |
| Добавить контекст приложения и запроса | [Bind и PushScope](docs/CONTEXT.md) |
| Настроить цвета, файлы и JSON | [Получатели и форматы](docs/OUTPUT.md) |
| Настроить маскирование | [Регулярки и редактирование данных](docs/REDACTION.md) |
| Передавать исключения и завершать запись | [Ошибки и время жизни](docs/ERRORS_AND_LIFETIME.md) |
| Реализовать собственный получатель | [Расширение библиотеки](docs/EXTENDING.md) |
| Найти точную сигнатуру или значение по умолчанию | [Справочник публичного API](docs/API.md) |
| Разобраться во внутреннем устройстве | [Архитектура](docs/ARCHITECTURE.md) |
| Собирать, тестировать и менять проект | [Разработка](docs/DEVELOPMENT.md) |
| Выпустить NuGet-пакет по тегу | [Публикация](docs/PUBLISHING.md) |
| Найти причину неожиданного поведения | [Решение проблем](docs/TROUBLESHOOTING.md) |
| Посмотреть следующие этапы | [План](docs/ROADMAP.md) |

## Границы MVP

Запись синхронная: медленный получатель задерживает поток, который пишет лог.
Ротация файлов, retention, фоновая очередь и адаптер `Microsoft.Extensions.Logging`
запланированы отдельно. `WithCaller()` анализирует стек; доступность файла и
строки зависит от PDB и оптимизации сборки.

Trexon.Logging — самостоятельный проект, вдохновлённый удобством Python Loguru.
