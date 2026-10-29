# Dokumentation zu Trexon.Logging

[Русский](../README.md) · [English](../en/README.md) · [Deutsch](../de/README.md)

[Projektstartseite](../../README.de.md)

Diese Anleitungen beschreiben die stabile Version `1.0.0`. Mit dem ausführbaren
Beispiel beginnen und danach die gewünschte Funktion auswählen. Vollständige
C#-Programme mit `verify` werden durch `python3 scripts/check-docs.py` kompiliert
und ausgeführt. API-Signaturen sind Deklarationen, keine vollständigen Programme.
Die Übersetzungen teilen Beispieldaten, einschließlich russischer Unicode-Texte.

## Lernpfad

1. [Schnellstart](GETTING_STARTED.md): Anforderungen, Installation, erste Anwendung.
2. [Konfiguration](CONFIGURATION.md): Stufen, Filter, Momentaufnahmen und Uhr.
3. [Nachrichten](MESSAGES.md): Platzhalter, Formate und komplexe Werte.
4. [Kontext](CONTEXT.md): globale Eigenschaften, `Bind` und asynchroner `PushScope`.
5. [Ausgabe](OUTPUT.md): Konsole, Farben, Dateien und JSON-Schema.
6. [Maskierung](REDACTION.md): Regex-Regeln, Reihenfolge, Zeitlimits und Grenzen.
7. [Fehler und Lebenszyklus](ERRORS_AND_LIFETIME.md): Ausnahmen, `Flush` und Freigabe.
8. [Erweiterungen](EXTENDING.md): eigene `ILogSink`-Implementierung und Besitz.

## Referenz und Projektpflege

| Dokument | Inhalt |
| --- | --- |
| [API](API.md) | Alle öffentlichen Typen, Methoden, Parameter und Standardwerte |
| [Architektur](ARCHITECTURE.md) | Verarbeitung, Momentaufnahmen, Sperren, Besitz und Quelldateien |
| [Entwicklung](DEVELOPMENT.md) | Build, Tests, Dokumentationssprachen und lokale Pakete |
| [Veröffentlichung](PUBLISHING.md) | CI, Trusted Publishing, Versionen, Tags und Wiederholungen |
| [Fehlersuche](TROUBLESHOOTING.md) | Symptome, Ursachen und Prüfungen |
| [Plan](ROADMAP.md) | Vorhandene Funktionen und nächste Schritte |

## Vorhandene Funktionen

`Logger` und statisches `Log`; sechs Stufen und `None`; Vorlagen und typisierte
Eigenschaften; Ausnahmemomentaufnahmen und Aufruferdaten; Fluent-Konfiguration;
unabhängige Stufen und Filter; `TimeProvider`; `WithProperty`, `Bind` und
verschachtelte `AsyncLocal`-Bereiche; Konsole, eigener `TextWriter` und Dateien;
Text und JSON Lines; Zeitstempel, Zeitzonen und ANSI-Farben; Regex-Maskierung mit
Zeitlimit; eigene Empfänger und `OnError`; synchrones Flush und Dispose; NuGet-
und Symbolpakete, XML-Dokumentation, CI und Veröffentlichungsworkflow per Tag.

Dateizugriff mehrerer Prozesse, Warteschlangen, Rotation und ein fertiges
DI-/Host-Integrationspaket gehören zu den [nächsten Schritten](ROADMAP.md).
