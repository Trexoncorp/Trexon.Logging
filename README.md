# Trexon.Logging

C#-библиотека логирования, вдохновлённая простотой Python Loguru.

**Статус:** подготовлена инфраструктура NuGet и CI/CD. API логирования пока
не реализован; опубликованных релизов этого проекта пока нет.

## Совместимость

Пакет предназначен для приложений на .NET 8 и новее. Для сборки репозитория
нужен .NET SDK 10; подходящая версия задаётся в `global.json`.

## Установка

После первого релиза на nuget.org:

```sh
dotnet add package Trexon.Logging
```

Для предварительного релиза:

```sh
dotnet add package Trexon.Logging --prerelease
```

## Разработка

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts
```

В `artifacts/` появятся `.nupkg` и `.snupkg`. Версия локальной сборки —
`0.1.0-dev`; workflow публикации берёт версию из Git-тега.

CI проверяет сборку и упаковку на Linux, Windows и macOS. Поведенческие тесты
будут добавлены вместе с первой реализацией логгера; сейчас тестовых проектов нет.

## Релизы

Push тега `v0.1.0-alpha.1` создаёт пакет версии `0.1.0-alpha.1`,
а тега `v0.1.0` — версии `0.1.0`. Публикация в nuget.org автоматическая,
после однократной настройки Trusted Publishing.

Инструкция: [публикация пакета](docs/PUBLISHING.md).
Предложение для обсуждения: [этапы реализации](docs/ROADMAP.md).

## Лицензия

[MIT](LICENSE). Trexon.Logging — самостоятельный проект; упоминание Loguru
описывает источник идеи удобного API.
