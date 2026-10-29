# Разработка, тестирование и локальная проверка

[Русский](DEVELOPMENT.md) · [English](en/DEVELOPMENT.md) · [Deutsch](de/DEVELOPMENT.md)

[Оглавление](README.md) · [Архитектура](ARCHITECTURE.md) · [Публикация](PUBLISHING.md)

## Инструменты

Нужен .NET SDK 10. `global.json` задаёт стартовую версию `10.0.100`,
`rollForward: latestFeature` внутри выбранной major/minor и запрещает prerelease.
Это позволяет использовать установленную совместимую стабильную feature band.

Тестовый проект имеет targets `net8.0;net10.0`. Установите оба runtime, иначе
более старый target не запустится только за счёт наличия runtime .NET 10.
Для проверки Markdown-примеров нужен Python 3. Для локальной проверки workflow
необязателен `actionlint`.

```sh
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

## Структура

```text
Trexon.Logging.slnx
Trexon.Logging/                     основная библиотека net8.0
  Logger.cs, Log.cs                 публичные точки входа
  LoggerConfiguration.cs           основной fluent builder и регистрация sinks
  SinkConfiguration.cs             параметры встроенного вывода
  LogEvent.cs, LogLevel.cs          события, caller, исключения и уровни
  ILogSink.cs                       расширение и технические сбои
  Internal/                        реализация pipeline, шаблонов, снимков и вывода
tests/Trexon.Logging.Tests/         xUnit v3 / Microsoft Testing Platform
examples/Trexon.Logging.Sample/    запускаемый пример net8.0
docs/                              руководство и справочники
scripts/release-version.sh         преобразование релизного тега в версию
scripts/check-docs.py              ссылки и исполняемые примеры документации
.github/workflows/                 CI и публикация по тегам
global.json                        SDK и выбор тестового runner
```

Основная библиотека не содержит PackageReference. Тестовые пакеты принадлежат
только непакуемому тестовому проекту. Пример также имеет `IsPackable=false`.

## Обычный цикл

Из корня репозитория:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
python3 scripts/check-docs.py
git diff --check
```

Тесты используют выбранный в global.json Microsoft Testing Platform. Синтаксис
`--solution` соответствует этому runner. Сначала собирайте актуальные бинарники,
затем используйте `--no-build`. Это особенно важно после смены PackageReference
или настроек runner.

`dotnet test` запускает набор на обоих targets. Одна теория с несколькими
InlineData даёт несколько случаев; число успешных запусков суммирует оба runtime.
Результаты текущего запуска сообщает runner — фиксированная цифра в документации
не заменяет этот результат.

## Что проверяют тесты

| Файл | Сценарии |
| --- | --- |
| [LoggerTests.cs](../tests/Trexon.Logging.Tests/LoggerTests.cs) | Уровни, шаблоны, культура, снимки, caller, конфигурация, валидация |
| [ContextTests.cs](../tests/Trexon.Logging.Tests/ContextTests.cs) | Вложенность scopes, async-изоляция, LIFO, дочерний ExecutionContext |
| [OutputTests.cs](../tests/Trexon.Logging.Tests/OutputTests.cs) | Даты, зоны, цвета, JSON, независимые sinks, UTF-8 и append, конкурентный файл |
| [RedactionTests.cs](../tests/Trexon.Logging.Tests/RedactionTests.cs) | Вложенное маскирование, JSON-экранирование, порядок правил и timeout |
| [LifecycleTests.cs](../tests/Trexon.Logging.Tests/LifecycleTests.cs) | Сбои sinks, владение, flush/dispose, частичная сборка, рекурсия, конкуренция |
| [StaticLogTests.cs](../tests/Trexon.Logging.Tests/StaticLogTests.cs) | Консоль по умолчанию, Configure, Shutdown, старые views и ошибка перенастройки |

Тесты процессного состояния выделены в непараллельную коллекцию. Тестовые
временные папки удаляются после использования. Проверка caller использует
метод, защищённый от inlining, и доступные PDB.

Прохождение тестов подтверждает проверенные сценарии; отдельного SLA по
производительности и гарантий Native AOT в MVP нет.

## Проверка документации

```sh
python3 scripts/check-docs.py --links-only
python3 scripts/check-docs.py
```

Первый режим проверяет наличие всех языковых версий, относительные Markdown-ссылки
на файлы/заголовки и корректность маркеров. Полный режим также извлекает блоки C# с маркером
`<!-- verify: unique-name -->`, собирает отдельные временные приложения,
ссылающиеся на текущий проект, и выполняет их на .NET 8.

Каждый такой блок — полная программа: свои using, верхнеуровневые инструкции
и при необходимости типы в конце. Блоки сигнатур API и короткие фрагменты
без маркера не запускаются. Shell-блоки, создание Git-тегов, отправка пакетов
и сетевые настройки скриптом не выполняются. Скрипт может выполнять обычный
NuGet restore .NET SDK при сборке временных проектов.

Проверка не требует редактировать библиотеку, создавать коммит или пушить код.
Если изменяется публичная сигнатура, обновите API.md и связанные примеры.

### Языковые версии

В NuGet основным выбран `README.en.md` через `PackageReadmeFile`. Его примеры
на английском, а ссылки полные, чтобы работать и на GitHub, и на nuget.org.
Обновление встроенного README опубликованного пакета требует новой версии.

Документация доступна на русском, английском и немецком. Русские руководства
остаются в `docs/`, переводы с теми же именами — в `docs/en/` и `docs/de/`.
Стартовые страницы проекта: `README.md`, `README.en.md`, `README.de.md`.
Переключатель в начале каждой страницы ведёт к соответствующим переводам.

При изменении API или поведения обновляйте все три версии. Для новых русских
руководств нужны оба перевода: скрипт сообщает об отсутствующих файлах.
Маркеры исполняемых примеров должны быть уникальны во всём наборе; в переводах
используются префиксы `en-` и `de-`. Примеры используют общие данные, включая
русские строки для демонстрации Unicode. Проверки подтверждают структуру и
работоспособность примеров, но не лингвистическую точность текста.

Для нового языка дополните `LANGUAGES` в `scripts/check-docs.py`, создайте README
и соответствующие руководства, затем обновите переключатели языка.

## Собрать пакет

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -p:ContinuousIntegrationBuild=true -warnaserror
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:ContinuousIntegrationBuild=true -warnaserror
```

В `artifacts/` появятся `Trexon.Logging.0.1.0-dev.nupkg` и соответствующий
`.snupkg`. Основной пакет содержит DLL, XML-документацию, README на трёх языках, MIT-лицензию
и repository metadata; символы — portable PDB. Source Link включён средствами
SDK, untracked sources могут встраиваться в PDB. Метаданные commit для
незакоммиченной рабочей копии не являются новым опубликованным commit.

Смена версии через `-p:Version=...` должна быть одинаковой у build и pack.
Проверка потребления локального пакета: [быстрый старт](GETTING_STARTED.md).
Папки `bin`, `obj`, `TestResults` и `artifacts` игнорируются Git.

## CI

Проверка документации выполняется в CI на Linux и в релизном workflow перед основной сборкой.
Она проверяет языковые версии, ссылки и запускаемые примеры.

Локальные workflow подготовлены так:

- `ci.yml`: push/PR в main, Linux + Windows + macOS, restore → build →
  тесты net8/net10 → pack → артефакты на 14 дней.
- `publish.yml`: push тега `v*`, проверка версии/истории main → build →
  тесты → pack → артефакт на 30 дней → OIDC login → отправка nupkg/snupkg.
- `dependabot.yml`: ежемесячная группа обновлений GitHub Actions.

Actions закреплены по SHA. setup-dotnet устанавливает SDK из global.json и
.NET 8 для тестов. Фактическое выполнение новой версии workflow на GitHub
происходит только после отправки соответствующих изменений в репозиторий.

Если actionlint установлен:

```sh
actionlint
```

Выпуск — отдельная операция по [инструкции публикации](PUBLISHING.md).
