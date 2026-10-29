# Публикация Trexon.Logging в NuGet

[Русский](PUBLISHING.md) · [English](en/PUBLISHING.md) · [Deutsch](de/PUBLISHING.md)

[Оглавление](README.md) · [Локальная разработка](DEVELOPMENT.md)

Эта глава описывает выпуск после готовности рабочей копии. Команды с тегами
и push являются инструкцией для отдельного согласованного релиза.
Обычная сборка или проверка документации ничего не публикует.

## Что делает GitHub Actions

- `ci.yml`: push в `main` и pull request в `main` → restore, Release build,
  тесты на .NET 8/.NET 10 и упаковка на Linux, Windows и macOS.
  Пакеты доступны в артефактах запуска.
- `publish.yml`: push тега `v*` → проверка версии и принадлежности коммита
  истории `main`, сборка и тесты на Ubuntu, упаковка, публикация `.nupkg` и `.snupkg` в nuget.org.
- Publish job использует GitHub environment `nuget` и OIDC. Постоянный API-ключ
  не хранится; временный ключ выдаёт `NuGet/login` перед отправкой пакетов.
- Publish job отправляет готовые артефакты package job и не пересобирает пакет.
- Падение тестов останавливает упаковку и публикацию.

Перед релизной сборкой workflow также проверяет документацию на трёх языках
и компилирует/запускает помеченные примеры. Ошибка проверки останавливает релиз.

## Однократная настройка

1. Войти в свой аккаунт на [nuget.org](https://www.nuget.org/).
   Подтвердить email нового аккаунта: без этого сайт не разрешает создать policy.
   Убедиться, что он имеет право публиковать пакет `Trexon.Logging`.
   Публичный GitHub-репозиторий сам по себе не резервирует имя в NuGet.
2. На nuget.org открыть **Trusted Publishing**, создать GitHub policy:

   | Поле | Значение |
   | --- | --- |
   | Repository Owner | `Trexoncorp` |
   | Repository | `Trexon.Logging` |
   | Workflow File | `publish.yml` |
   | Environment | `nuget` |
   | Package scope / glob pattern | `Trexon.Logging` |

   Policy должна иметь scope **Push new packages and package versions**.
   Разрешение unlist/relist для данного workflow не требуется.

   Для первого релиза разрешить публикацию нового пакета, а для следующих —
   новых версий. Владельцем policy выбрать аккаунт или NuGet-организацию,
   которой должен принадлежать пакет. GitHub-организация автоматически
   не создаёт одноимённую организацию в NuGet.
3. В GitHub открыть **Settings → Secrets and variables → Actions → Variables**,
   добавить repository variable `NUGET_USER`. Значение — имя профиля
   пользователя nuget.org, выполняющего публикацию, **не email** и не API-ключ.
   Оно должно соответствовать аккаунту, настроившему policy.
4. В **Settings → Environments** создать environment `nuget`.
   Если нужна полностью автоматическая публикация по тегу, не добавлять
   required reviewers. При наличии deployment branch/tag rules разрешить `v*`.
5. Убедиться, что GitHub Actions разрешены в настройках организации и репозитория.

Без этой привязки workflow сможет собрать пакет, но не сможет его опубликовать.

Официальная документация:
[NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing),
[NuGet symbol packages](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg).

## Проверка пакета локально

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -p:Version=0.1.0-alpha.1 -p:ContinuousIntegrationBuild=true -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
python3 scripts/check-docs.py
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:Version=0.1.0-alpha.1 -p:ContinuousIntegrationBuild=true -warnaserror
```

У `build` и `pack --no-build` версия должна совпадать, чтобы версия DLL
соответствовала версии пакета. `.nupkg` содержит DLL для `net8.0`, XML-документацию,
README на трёх языках, MIT-лицензию и метаданные репозитория. `.snupkg` содержит portable PDB.
Source Link поддерживается SDK; связь с исходниками появляется при сборке
закоммиченного кода из Git-репозитория.

## Выпуск версии

Первую версию выпускать после проверки рабочего API, тестов и документации.
Следующие команды — инструкция, они не выполняются автоматически при подготовке
репозитория:

```sh
git switch main
git pull --ff-only origin main
git tag -a v0.1.0-alpha.1 -m "Trexon.Logging 0.1.0-alpha.1"
git push origin v0.1.0-alpha.1
```

Версия берётся только из тега; редактировать `.csproj` перед релизом не нужно.
Допустимы `v1.2.3`, `v1.2.3-alpha.1`, `v1.2.3-rc.2`. Не допускаются ведущие
нули в числовых компонентах, пропущенные компоненты и build metadata `+...`.

Проверка формата без публикации:

```sh
bash scripts/release-version.sh v0.1.0-alpha.1
```

Проверить завершение workflow **Publish to NuGet**, затем страницу пакета на
nuget.org: индексация и валидация могут занять дополнительное время.
Workflow не создаёт GitHub Release — он публикует NuGet-пакет по Git-тегу.

## Повторный запуск

При временной ошибке повторно запустить failed jobs в GitHub Actions.
`--skip-duplicate` позволяет повторить отправку уже опубликованного пакета;
символы отправляются отдельным шагом, чтобы можно было повторить их отправку.
Если артефакт истёк, повторно запустить все jobs для того же тега.

Опубликованные версии и релизные теги не переиспользовать. Исправления
выпускать новым тегом. Обычный CI собирает пакеты версии `0.1.0-dev`
для локальных проверок и сам не вызывает publish. Отправка версии определяется
отдельным релизным тегом; для первого релиза используйте согласованный prerelease.

Перед тегом проверьте CI main на всех трёх ОС. Релизный workflow повторно
запускает свои тесты на Ubuntu и автоматически не ожидает отдельный запуск CI.

## Диагностика workflow

| Сбой | Что проверить |
| --- | --- |
| Invalid release tag | `scripts/release-version.sh`: три числовые части, корректный prerelease |
| Tag commit not reachable from main | В main должен присутствовать выпускаемый commit |
| Test job failed | Неисправный target/тест, runtime .NET 8, актуальные бинарники и runner |
| Environment rejected deployment | В окружении nuget должен быть разрешён тип Tag с pattern v* |
| NUGET_USER missing | Repository variable содержит имя NuGet-профиля |
| OIDC login failed | Аккаунт, owner/repository, publish.yml, environment и scope policy |
| NuGet rejected package ID | Права владельца, занятость/резервирование имени и package scope |
| Version already exists | Версию нельзя перезаписать; исправление требует нового тега |
| Symbols failed | Повторить failed job; шаг snupkg отделён от основного push |

Активная policy и совпадающие настройки подтверждают конфигурацию. Фактический
обмен OIDC и приём пакета NuGet подтверждаются успешным релизным запуском;
зелёный CI без релизного тега этого не проверяет.
