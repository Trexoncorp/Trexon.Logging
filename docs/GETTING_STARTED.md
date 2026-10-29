# Быстрый старт

[Русский](GETTING_STARTED.md) · [English](en/GETTING_STARTED.md) · [Deutsch](de/GETTING_STARTED.md)

[Оглавление](README.md) · Далее: [настройка](CONFIGURATION.md)

## Требования

| Сценарий | Требования |
| --- | --- |
| Приложение использует пакет | .NET 8 или совместимая более новая версия .NET |
| Сборка этого репозитория | .NET SDK 10; выбор версии описан в `global.json` |
| Полный запуск тестов | Runtime .NET 8 и .NET 10; SDK 10 включает свой runtime |
| Проверка документации | Python 3, SDK 10 и runtime .NET 8 |

Основной пакет имеет target `net8.0`, внешних runtime PackageReference нет.
Совместимость с .NET Framework и Native AOT в рамках MVP не заявлена.

## Вариант 1: запустить готовый пример

Из корня репозитория:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build
```

[Program.cs](../examples/Trexon.Logging.Sample/Program.cs) демонстрирует консоль,
JSON-файл, caller info, уровни, контекст, обработку исключения и маскирование.
Файл `logs/sample.jsonl` создаётся относительно `AppContext.BaseDirectory`
примера — в папке его сборки `bin/Release/net8.0/`. Повторный запуск дописывает
файл. Это короткое консольное приложение: оно записывает сообщения, выводит путь
к JSON-файлу и завершается. Ожидание ввода и отдельное окно не предусмотрены.

Демонстрация записи исключения включается отдельно:

```sh
dotnet run --project examples/Trexon.Logging.Sample --configuration Release --no-build -- --demo-error
```

В этом режиме программа специально бросает и перехватывает исключение,
записывает его вместе со stack trace и продолжает работу. Оба режима
завершаются с кодом `0`, если запуск и запись прошли успешно.

## Вариант 2: подключить проект напрямую

Команды выполняются из корня этого репозитория и создают соседнюю папку:

```sh
dotnet new console --framework net8.0 --output ../Trexon.Logging.Playground
dotnet add ../Trexon.Logging.Playground reference Trexon.Logging/Trexon.Logging.csproj
```

Замените `Program.cs` созданного приложения примером ниже и запустите:

```sh
dotnet run --project ../Trexon.Logging.Playground
```

## Вариант 3: установить локальный NuGet-пакет

Сначала собрать библиотеку и пакет:

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -warnaserror
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts
```

В уже созданном приложении:

```sh
trexon_package_source="$(pwd)/artifacts"
dotnet add ../Trexon.Logging.Playground package Trexon.Logging --version 0.1.0-dev --source "$trexon_package_source"
```

Выберите один способ подключения: при переходе с ProjectReference на пакет
удалите ProjectReference из consumer `.csproj`. Переменная выше вычисляется
из корня репозитория в Bash/zsh. В другой оболочке передайте полный путь
к `artifacts/` напрямую.

NuGet кэширует пакет по ID и версии. Если повторно собираете `0.1.0-dev`,
уже установленная копия может остаться в кэше. Для отдельной проверки удобна
уникальная версия; у `build` и `pack --no-build` она должна совпадать:

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -p:Version=0.1.0-local.1
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:Version=0.1.0-local.1
```

Затем установите `--version 0.1.0-local.1`. Это локальная проверка, команды
`dotnet nuget push` здесь не нужны.

## Первый логгер

Полная программа:

<!-- verify: getting-started -->
```csharp
using Trexon.Logging;

using var logger = Logger.Create();
logger.Info("Приложение запущено");
logger.Warning("Осталось {Attempts} попыток", 2);
logger.Info("Заказ {OrderId} создан", 42);
```

Пример вида вывода; время зависит от момента запуска:

```text
2026-10-29 12:00:00.000 +00:00 [INF] Приложение запущено
2026-10-29 12:00:00.001 +00:00 [WRN] Осталось 2 попыток
2026-10-29 12:00:00.002 +00:00 [INF] Заказ 42 создан
```

Поток по умолчанию — `Console.Error`. `Debug` и `Trace` при стандартном уровне
`Information` пропускаются. `using` гарантирует завершение записи при выходе
из области; для файлов это также закрывает stream.

## Процессный фасад

Для небольшого приложения доступен `Log.Info(...)` без создания экземпляра.
Настройте его один раз в начале и закройте в конце:

<!-- verify: static-facade -->
```csharp
using Trexon.Logging;

Log.Configure(config => config
    .MinimumLevel(LogLevel.Debug)
    .WriteTo.Console(console => console.Timestamp("HH:mm:ss")));

try
{
    using var request = Log.PushScope("RequestId", "request-42");
    Log.Debug("Обработка {OrderId}", 42);
    Log.Info("Готово");
}
finally
{
    Log.Shutdown();
}
```

`Log.Configure` заменяет предыдущий root-логгер и закрывает его ресурсы.
Сохранённые ранее `Log.Bind(...)`-представления после замены недействительны.
Подробности: [время жизни](ERRORS_AND_LIFETIME.md).

## Установка из NuGet

Для первого prerelease укажите версию явно; для последнего prerelease используйте
`--prerelease`:

```sh
dotnet add package Trexon.Logging --version 0.1.0-alpha.1
dotnet add package Trexon.Logging --prerelease
```

Текущее состояние релизов смотрите в реестре перед установкой. Подготовка
пакета и выпуск по тегу описаны в [публикации](PUBLISHING.md).
