# Entwicklung und lokale Prüfung

[Русский](../DEVELOPMENT.md) · [English](../en/DEVELOPMENT.md) · [Deutsch](../de/DEVELOPMENT.md)

[Übersicht](README.md) · [Architektur](ARCHITECTURE.md) · [Veröffentlichung](PUBLISHING.md)

## Werkzeuge

SDK 10 verwenden. `global.json` wählt `10.0.100`, `rollForward: latestFeature` innerhalb
der gewählten Haupt-/Nebenversion und keine Vorabversionen. Tests zielen auf net8.0
und net10.0; beide Runtimes installieren. .NET 10 allein führt das ältere Ziel nicht
aus. Python 3 wird zur Dokumentationsprüfung benötigt, actionlint optional für Workflows.

```sh
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

## Struktur

| Pfad | Zweck |
| --- | --- |
| Trexon.Logging.slnx | Projektmappe |
| Trexon.Logging/ | net8.0-Kernbibliothek mit interner Implementierung |
| tests/Trexon.Logging.Tests/ | xUnit v3 / Microsoft Testing Platform |
| examples/Trexon.Logging.Sample/ | Ausführbares net8.0-Konsolenbeispiel |
| README.md / README.ru.md / README.de.md | Englische, russische und deutsche Projektseiten; README.en.md erhält bestehende englische Links |
| docs/ / docs/en/ / docs/de/ | Anleitungen in drei Sprachen |
| scripts/check-docs.py | Übersetzungsvollständigkeit, Links und ausführbare Beispiele |
| scripts/release-version.sh | Prüfung des Release-Tags und Versionsableitung |
| .github/workflows/ | CI und Veröffentlichung per Tag |
| global.json | SDK- und Test-Runner-Auswahl |

Der Kern enthält keine PackageReferences. Testabhängigkeiten bleiben im nicht
paketierbaren Testprojekt; auch das Beispiel ist nicht paketierbar.

## Entwicklungsablauf

Im Projektstamm:

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
python3 scripts/check-docs.py
git diff --check
```

Der gewählte Runner ist Microsoft Testing Platform; dessen `--solution`-Syntax
verwenden. Vor `--no-build` aktuelle Binärdateien bauen, besonders nach Änderungen
an Paketen oder Runner. Tests laufen auf beiden Zielen. Theory-Fälle und Runtimes
zählen zusammen; maßgeblich ist die Runner-Ausgabe statt einer festen Dokumentationszahl.

## Testabdeckung

| Datei | Szenarien |
| --- | --- |
| [LoggerTests.cs](../../tests/Trexon.Logging.Tests/LoggerTests.cs) | Stufen, Vorlagen, Kultur, Momentaufnahmen, Aufrufer, Validierung |
| [ContextTests.cs](../../tests/Trexon.Logging.Tests/ContextTests.cs) | Scopes, asynchrone Isolation, LIFO, untergeordneter ExecutionContext |
| [OutputTests.cs](../../tests/Trexon.Logging.Tests/OutputTests.cs) | Zeit, Zonen, Farben, JSON, Unicode, UTF-8-Anhängen, Parallelität |
| [RedactionTests.cs](../../tests/Trexon.Logging.Tests/RedactionTests.cs) | Verschachtelte Maskierung, Escaping, Reihenfolge, Regex-Timeout |
| [LifecycleTests.cs](../../tests/Trexon.Logging.Tests/LifecycleTests.cs) | Fehler, Besitz, Flush/Dispose, Initialisierung, Rekursion, Wettläufe |
| [StaticLogTests.cs](../../tests/Trexon.Logging.Tests/StaticLogTests.cs) | Standardwerte, Configure, Shutdown, alte Ansichten, fehlgeschlagener Austausch |

Tests globaler Zustände laufen in einer nicht parallelen Sammlung. Temporäre Ordner
werden entfernt. Aufrufertests verwenden Methoden ohne Inlining und verfügbare PDBs.
Erfolgreiche Tests bestätigen geprüfte Szenarien, keine Leistungs-SLA oder Native-AOT-Unterstützung.

## Dokumentation und Übersetzungen

NuGet verwendet `README.md` über `PackageReadmeFile`. Die Beispiele sind englisch
und Links absolut, damit sie auf GitHub und nuget.org funktionieren. Änderungen
am eingebetteten README eines veröffentlichten Pakets benötigen eine neue Paketversion.

```sh
python3 scripts/check-docs.py --links-only
python3 scripts/check-docs.py
```

Der erste Befehl prüft Sprachvollständigkeit, lokale Datei-/Überschriftenlinks,
Codebegrenzungen und Marker. Der vollständige Modus extrahiert C#-Programme mit
`<!-- verify: unique-name -->`, baut temporäre net8.0-Anwendungen mit Projektreferenz
und führt sie aus. Jedes Programm enthält eigene usings, Anweisungen und nötige Typen.
Signaturblöcke und unmarkierte Fragmente werden nicht ausgeführt. Shell-Blöcke,
Git-Tags, Paketveröffentlichung und Netzwerkeinstellungen führt das Skript nie aus.
Das SDK kann beim Bauen reguläres NuGet-Restore ausführen.

Russische Seiten bleiben unter docs/, gleichnamige Übersetzungen unter docs/en/
und docs/de/. Jede Seite verlinkt ihre Sprachversionen. Bei API- oder Verhaltensänderungen
alle drei Fassungen aktualisieren. Verify-Namen sind global eindeutig; Übersetzungen
verwenden `en-` beziehungsweise `de-`. Neue russische Anleitungen benötigen beide
Übersetzungen; die Prüfung meldet fehlende Dateien. Struktur und Programme werden
geprüft, nicht die sprachliche Richtigkeit. Für weitere Sprachen `LANGUAGES` im
Prüfskript erweitern, passende Seiten und ein Root-README erstellen und Sprachlinks ergänzen.

## Lokale Pakete

```sh
dotnet build Trexon.Logging/Trexon.Logging.csproj --configuration Release -p:ContinuousIntegrationBuild=true -warnaserror
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:ContinuousIntegrationBuild=true -warnaserror
```

Unter `artifacts/` entstehen nupkg und snupkg der Version `1.0.0-dev`. Das Hauptpaket
enthält DLL, XML-Dokumentation, mehrsprachige READMEs, MIT-Lizenz und Repositorydaten.
Das Symbolpaket enthält portable PDBs. SDK Source Link verknüpft versionierte Quellen
mit dem Repository; nicht erfasste Quellen können in PDBs eingebettet werden.
Uncommittete Änderungen sind kein neuer veröffentlichter Commit. Version bei Build
und Pack identisch setzen. Siehe [lokale Verwendung](GETTING_STARTED.md).
bin, obj, TestResults und artifacts werden von Git ignoriert.

## CI

Die Dokumentationsprüfung läuft in der Linux-CI und im Release-Workflow vor dem Haupt-Build.
Sie prüft Sprachversionen, Links und ausführbare Beispiele.

Die lokalen Workflows definieren:

- ci.yml: Push/PR nach main; Linux, Windows und macOS; Restore, Release-Build,
  net8/net10-Tests, Pack und Paketartefakte für 14 Tage.
- publish.yml: v*-Tag; Version/main-Abstammung prüfen, Build, Tests, Pack,
  Artefakte für 30 Tage, OIDC-Anmeldung und nupkg/snupkg-Veröffentlichung.
- dependabot.yml: monatlich gruppierte Updates der GitHub Actions.

Actions sind auf SHAs festgelegt. setup-dotnet installiert das SDK aus global.json
und .NET 8. Geänderte Workflows laufen auf GitHub erst nach dem Push dieser Änderungen.
Falls actionlint vorhanden ist:

```sh
actionlint
```

Die Veröffentlichung bleibt eine getrennte [Release-Operation](PUBLISHING.md).
