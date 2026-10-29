# Entwicklungsplan

[Русский](../ROADMAP.md) · [English](../en/ROADMAP.md) · [Deutsch](../de/ROADMAP.md)

[Übersicht](README.md) · [Architektur](ARCHITECTURE.md)

## MVP 0.1.0-alpha.1

Vorhanden: Fluent-API mit MinimumLevel/Enrich/Redact/Filter/OnError/WriteTo; Logger
und Log; strukturierte Ereignisse, sechs Stufen, Vorlagen, typisierte Eigenschaften
und Ausnahmemomentaufnahmen; Konsolenzeit/Farben/Aufrufer; UTF-8-Dateien zum Anhängen
und JSON Lines; Bind und verschachtelte asynchrone Scopes; Regex-Maskierung mit
Zeitlimits; isolierte Empfängerfehler; synchrones Schreiben, Flush, Besitz und
Dispose; Verhaltenstests für net8/net10; ausführbares Beispiel; NuGet-Paketierung
und Release-Workflows mit Tests vor Veröffentlichung; russische, englische und
deutsche Dokumentation.

Die erste Vorabversion ist `0.1.0-alpha.1`. Bis Version 1.0 kann sich die API
ändern; bei Updates die Kompatibilität prüfen.

## Nächste Schritte

| Schritt | Umfang | Fertigstellungskriterien |
| --- | --- | --- |
| Dateien | Größen-/zeitbasierte Rotation, Aufbewahrung, danach Komprimierung | Rotationsgrenzen, Parallelität, Datenträgerfehler und Aufbewahrung getestet |
| Warteschlange | Begrenzter Channel, Überlaufregeln, FlushAsync/DisposeAsync | Reihenfolge, erwarteter Verlust/Blockierung und Abschluss mit Restdaten getestet |
| Integration | Trexon.Logging.Extensions.Logging, ILoggerProvider, Scopes, DI/Host | Anwendung und ASP.NET Core teilen Verarbeitung ohne Zyklen/Duplikate |
| Absicherung | Benchmarks, Vorlagen, JSON-Optionen, Aufruferstrategie | Reale Verwendung und Aufwand aktivierter Funktionen geprüft |
| API 1.0 | Kompatibilität, Dokumentation, stabile Verträge | Verhalten auf Linux, Windows und macOS bestätigt |

C#-Attribute könnten später dieselbe Konfiguration alternativ aufbauen.
Die primäre API bleibt Fluent.
