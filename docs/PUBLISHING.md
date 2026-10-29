# Публикация Trexon.Logging в NuGet

## Что делает GitHub Actions

- `ci.yml`: push в `main` и pull request в `main` → restore, Release build,
  упаковка на Linux, Windows и macOS. Пакеты доступны в артефактах запуска.
- `publish.yml`: push тега `v*` → проверка версии и принадлежности коммита
  истории `main`, сборка, упаковка, публикация `.nupkg` и `.snupkg` в nuget.org.
- Publish job использует GitHub environment `nuget` и OIDC. Постоянный API-ключ
  не хранится; временный ключ выдаёт `NuGet/login` перед отправкой пакетов.
- Publish job отправляет готовые артефакты package job и не пересобирает пакет.
- Сейчас проверяются сборка и упаковка. Когда появится реализация, перед
  публикацией нужно добавить запуск её тестов в оба workflow.

## Однократная настройка

1. Войти в свой аккаунт на [nuget.org](https://www.nuget.org/).
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
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:Version=0.1.0-alpha.1 -p:ContinuousIntegrationBuild=true -warnaserror
```

У `build` и `pack --no-build` версия должна совпадать, чтобы версия DLL
соответствовала версии пакета. `.nupkg` содержит DLL для `net8.0`, XML-документацию,
README, MIT-лицензию и метаданные репозитория. `.snupkg` содержит portable PDB.
Source Link поддерживается SDK; связь с исходниками появляется при сборке
закоммиченного кода из Git-репозитория.

## Выпуск версии

Первую версию выпускать после появления рабочего API и тестов.
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
выпускать новым тегом. CI-пакеты версии `0.1.0-dev` предназначены для локальных
проверок и не отправляются в nuget.org.
