# Veröffentlichung auf NuGet

[Русский](../PUBLISHING.md) · [English](../en/PUBLISHING.md) · [Deutsch](../de/PUBLISHING.md)

[Übersicht](README.md) · [Entwicklung](DEVELOPMENT.md)

Diese Anleitung beschreibt den Release nach Fertigstellung der Arbeitskopie.
Tag-/Push-Befehle gehören zu einer separat freigegebenen Veröffentlichung.
Build und Dokumentationsprüfung veröffentlichen nichts.

## GitHub Actions

ci.yml verarbeitet Pushes und PRs nach main: Restore, Release-Build, net8/net10-Tests
und Pakete auf Linux, Windows und macOS mit herunterladbaren Artefakten.
publish.yml verarbeitet v*-Tags: Version und main-Abstammung prüfen, unter Ubuntu
bauen/testen, paketieren und nupkg/snupkg veröffentlichen. Testfehler stoppen Pack
und Veröffentlichung. Der Publish-Job nutzt die Umgebung `nuget` und OIDC.
NuGet/login liefert einen temporären Schlüssel; ein permanenter API-Schlüssel wird
nicht gespeichert. Veröffentlicht werden die Artefakte des Paketjobs ohne erneuten Build.

Vor dem Release-Build prüft der Workflow außerdem alle drei Dokumentationssprachen
und kompiliert/startet markierte Beispiele. Ein Fehler stoppt den Release.

## Einmalige Einrichtung

1. Auf [nuget.org](https://www.nuget.org/) anmelden und E-Mail bestätigen.
   Veröffentlichungsrechte für Trexon.Logging sicherstellen. Ein öffentliches
   GitHub-Repository reserviert keinen NuGet-Paketnamen.
2. Unter Trusted Publishing eine GitHub-Richtlinie erstellen:

   | Feld | Wert |
   | --- | --- |
   | Repository Owner | Trexoncorp |
   | Repository | Trexon.Logging |
   | Workflow File | publish.yml |
   | Environment | nuget |
   | Package scope / glob | Trexon.Logging |

   **Push new packages and package versions** auswählen. Unlist/relist ist unnötig.
   Für den ersten Release muss die Paketerstellung erlaubt sein, danach neue Versionen.
   Als Besitzer den gewünschten NuGet-Benutzer oder die NuGet-Organisation wählen.
   Eine GitHub-Organisation erzeugt keine gleichnamige NuGet-Organisation.
3. GitHub Settings → Secrets and variables → Actions → Variables: Repositoryvariable
   `NUGET_USER` mit dem NuGet-Profilnamen anlegen, nicht mit E-Mail oder API-Schlüssel.
   Das Profil muss zum Richtlinienkonto passen.
4. Unter Settings → Environments die Umgebung `nuget` anlegen. Für automatische
   Releases keine erforderlichen Prüfer festlegen. Bestehende Deploymentregeln
   müssen Tags nach Muster `v*` erlauben.
5. GitHub Actions in Organisation und Repository erlauben.

Ohne Verknüpfung kann der Build erfolgreich sein, die Veröffentlichung aber scheitern.
Offizielle Anleitungen: [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
und [Symbolpakete](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg).

## Lokale Prüfung

```sh
dotnet restore Trexon.Logging.slnx
dotnet build Trexon.Logging.slnx --configuration Release --no-restore -p:Version=1.0.0 -p:ContinuousIntegrationBuild=true -warnaserror
dotnet test --solution Trexon.Logging.slnx --configuration Release --no-build --no-restore
python3 scripts/check-docs.py
dotnet pack Trexon.Logging/Trexon.Logging.csproj --configuration Release --no-build --output artifacts -p:Version=1.0.0 -p:ContinuousIntegrationBuild=true -warnaserror
```

Build und pack --no-build benötigen dieselbe Version. Das nupkg enthält net8.0-DLL,
XML-Dokumentation, mehrsprachige READMEs, MIT-Lizenz und Repositorydaten. Das snupkg
enthält portable PDBs. SDK Source Link verknüpft versionierte Repositoryquellen.

## Version veröffentlichen

Vor dem ersten Release API, Tests und Dokumentation prüfen. Folgende Befehle sind
Anweisungen und werden bei der Projektvorbereitung nicht automatisch ausgeführt:

```sh
git switch main
git pull --ff-only origin main
git tag -a v1.0.0 -m "Trexon.Logging 1.0.0"
git push origin v1.0.0
```

Die Version stammt nur aus dem Tag, eine csproj-Änderung ist unnötig. Gültige Beispiele:
v1.2.3, v1.2.3-alpha.1, v1.2.3-rc.2. Führende Nullen, fehlende numerische Teile und
+Build-Metadaten werden abgelehnt. Ohne Veröffentlichung prüfen:

```sh
bash scripts/release-version.sh v1.0.0
```

Abschluss von **Publish to NuGet** abwarten und die Paketseite prüfen. Validierung
und Indexierung können länger dauern. Der Workflow erstellt keinen GitHub Release.

## Wiederholung

Bei vorübergehenden Fehlern fehlgeschlagene Jobs erneut starten. --skip-duplicate
erlaubt erneutes Senden bereits vorhandener Pakete. Symbole haben einen eigenen
Schritt. Nach Ablauf der Artefakte alle Jobs desselben Tags wiederholen.
Veröffentlichte Versionen und Release-Tags nicht umwidmen; Korrekturen erhalten neue
Tags. Reguläre CI baut 1.0.0-dev ohne Veröffentlichung. Version und Release-Kanal vor dem Tag ausdrücklich festlegen.

Vor dem Tag CI von main auf allen drei Betriebssystemen prüfen. Der Release-Workflow
testet erneut auf Ubuntu und wartet nicht automatisch auf einen separaten CI-Lauf.

## Fehlerdiagnose

| Symptom | Prüfung |
| --- | --- |
| Ungültiger Release-Tag | Drei Zahlenteile und gültige Vorabversionssyntax |
| Commit nicht von main erreichbar | Release-Commit muss in main enthalten sein |
| Tests fehlgeschlagen | Ziel, .NET-8-Runtime, aktuelle Binärdateien, Runner |
| Deployment abgelehnt | Umgebung nuget erlaubt Tag / v* |
| NUGET_USER fehlt | Repositoryvariable mit NuGet-Profilnamen |
| OIDC-Anmeldung scheitert | Konto, Besitzer/Repository, publish.yml, Umgebung, Bereich |
| Paket-ID abgelehnt | Besitz, Verfügbarkeit/Reservierung des Namens, Paketbereich |
| Version vorhanden | Überschreiben unmöglich; neuer Tag nötig |
| Symbole fehlgeschlagen | Fehlgeschlagenen Job wiederholen; separater snupkg-Schritt |

Eine aktive Richtlinie bestätigt die Konfiguration, nicht den tatsächlichen
OIDC-Austausch oder die NuGet-Annahme. Dafür ist ein erfolgreicher Release-Lauf
nötig; reguläre grüne CI prüft diesen Weg nicht.
