# Fehlersuche

[Русский](../TROUBLESHOOTING.md) · [English](../en/TROUBLESHOOTING.md) · [Deutsch](../de/TROUBLESHOOTING.md)

[Übersicht](README.md) · [Konfiguration](CONFIGURATION.md) · [Fehler](ERRORS_AND_LIFETIME.md)

## Beispiel endet sofort oder zeigt eine Ausnahme

Das Konsolenbeispiel schreibt mehrere Ereignisse und endet. Es ist kein dauerhafter
Dienst und keine Oberfläche. Abschlussmeldung und JSON-Pfad zeigen das Programmende.
Mit `--demo-error` wird eine Ausnahme absichtlich ausgelöst, abgefangen und an Error
übergeben. Der Stacktrace ist dann Loginhalt. Im Normalmodus entfällt diese Demonstration.
Siehe [Startbefehle](GETTING_STARTED.md#beispiel-starten).

## Ein Ereignis fehlt

| Prüfung | Maßnahme |
| --- | --- |
| Logger-Mindeststufe | Debug/Trace sind standardmäßig aus; MinimumLevel setzen |
| Empfänger-Mindeststufe | Beide Mindeststufen müssen erfüllt sein |
| None | Logger oder Empfänger könnte deaktiviert sein |
| Ausgabestrom | Standard ist Console.Error, nicht Console.Out |
| Filter | Globaler/lokaler Filter könnte false liefern |
| Pipeline-Fehler | OnError aktivieren; Momentaufnahme, Format und Regex-Zeitlimit prüfen |
| Empfängerfehler | OnError enthält Typ und Originalausnahme |
| Rekursiver Aufruf | Logging aus demselben Root-Callback wird übersprungen |
| Nur Datei konfiguriert | File fügt keine Konsole automatisch hinzu |

IsEnabled true prüft Stufen/Zustand, keine Filter und keine Zustellgarantie.

## Geschlossener Logger oder alte Ansicht

ObjectDisposedException nach using/Shutdown gehört zum Vertrag. Der Root muss länger
leben als seine Nutzer. Log.Configure schließt den Root alter Ansichten. Dispose
einer Ansicht schließt den Root nicht; Root-Dispose macht alle Ansichten ungültig.

## RequestId fehlt im Text

Kontext steht in Properties. Text hängt das Wörterbuch nicht automatisch an.
`{RequestId}`, Json() oder einen eigenen Empfänger verwenden. Namenspriorität
und aktiven asynchronen Ablauf prüfen.

## Scope wird nicht oder zu früh geschlossen

Verschachtelte using-Blöcke in umgekehrter Reihenfolge schließen. Falsche Reihenfolge
wirft InvalidOperationException und erhält die Kette. Untergeordneter ExecutionContext
erbt Scopes; dessen Dispose verändert den Elternkontext nicht. Siehe [Kontext](CONTEXT.md).

## Keine Farben oder sichtbare ANSI-Sequenzen

Auto benötigt einen aktuellen Standard-Writer ohne Umleitung. Für eigene Writer
Always nur bei ANSI-Unterstützung wählen, sonst Never. JSON bleibt ohne Farben.
Console.Error vor Logger.Create umleiten oder Writer explizit übergeben; der Writer
wird bei der Registrierung gespeichert.

## Zeit weicht von lokaler Zeit ab

Text verwendet standardmäßig UTC. TimeZone(TimeZoneInfo.Local) oder eine explizite
Zone setzen. JSON behält den Ereigniszeitstempel. HH:mm:ss verbirgt den Offset;
bei Bedarf zzz ergänzen.

## Aufrufer zeigt äußeren Frame oder keine Quellposition

WithCaller global einschalten und nicht mit Caller(false) verbergen. StackTrace
hängt von Inlining, Optimierung, asynchronen Zustandsmaschinen und PDB ab. Fehlende
Pfade entfallen im JSON; lineNumber kann 0 sein. Build-Pfade können normalisiert
sein, beispielsweise /_/.

## Ausnahme ohne Stacktrace

Die besondere Exception-Überladung verwenden und ExceptionDetails prüfen. Eine nur
erstellte, nie geworfene Ausnahme besitzt gewöhnlich keinen Stack. Logging erzeugt
keinen. AggregateException.Message kann trotz deaktivierter Details innere Nachrichten enthalten.

## Platzhalter bleibt stehen oder Array wird mehrere Argumente

Nur benannte `{Name}` und `{Name:format}` werden unterstützt. Zahlennamen, @, $,
Ausrichtung und verschachtelte Klammern fehlen. Fehlende Werte bleiben ohne Kontext
wörtlich stehen. Für einen Arraywert `(object)array` verwenden. Siehe [Nachrichten](MESSAGES.md).

## JSON enthält Backslashes oder Unicode-Escapes

Neue Einträge zeigen Kyrillisch und andere Alphabete lesbar. Alte Zeilen werden
nicht umgeschrieben; die Datei wird ergänzt. Anführungszeichen, Backslashes,
Steuerzeichen und manche Zeichen wie Emoji bleiben maskiert. JSON-Parser stellen
Originalwerte wieder her. Komplexe Message-Werte können selbst JSON-Text enthalten.
Zur maschinellen Verarbeitung properties statt der formatierten Nachricht auswerten.

## Komplexes Ereignis fehlt mit Pipeline-Diagnose

Zyklen, ungeeignete Typen, werfende Getter, ungültige Formate, Filterausnahmen und
Regex-Zeitlimits prüfen. Die Argumenterfassung erfolgt vor der Ausgabe; bei Fehlern
entfällt das ganze Ereignis. Kleines DTO übergeben und OnError auswerten. NaN/Infinity
können zusätzlich nur die JSON-Ausgabe verhindern, während Text funktioniert.

## Datei liegt woanders oder lässt sich nicht öffnen

Relative Pfade beziehen sich bei Registrierung auf das Arbeitsverzeichnis. Für
anwendungsrelative Pfade AppContext.BaseDirectory verwenden. Die Datei öffnet bei
Create; Rechte-/Pfadfehler treten sofort auf. Nur einen aktiven Writer pro Datei
vorsehen. Wiederholtes Log.Configure kann den neuen Writer vor dem Schließen des
alten öffnen. Einmal beim Start konfigurieren oder den alten Root sicher beenden.

## Regex erfasst nicht alle Werte

Regeln verändern Texte; typisierte Zahlen, Guid, Daten und Schlüssel bleiben erhalten.
Eingabeform, RegexOptions und Ersetzung prüfen. Verschachtelte Texte werden vor
JSON-Escaping maskiert, die fertige Nachricht erneut. Stabile Ersetzungen wie ***
wählen. LoggingFailure.Exception ist keine maskierte ExceptionInfo-Momentaufnahme.
Siehe [Maskierungsgrenzen](REDACTION.md).

## Flush scheint wirkungslos

Die Dateiausgabe verwendet AutoFlush und leert ihren Writer-Puffer bereits.
Flush ist für gepufferte Ziele nützlich und lässt den Logger offen. Dispose schließt
zusätzlich eigene Ressourcen. Beide garantieren keine physische Speicherung bei
Stromausfall. Siehe [Flush und Dispose](ERRORS_AND_LIFETIME.md#flush-und-dispose).

## Build- oder Testfehler

| Symptom | Maßnahme |
| --- | --- |
| Ziel/slnx nicht unterstützt | SDK 10 aus global.json verwenden |
| net8.0-Framework fehlt | .NET-8-Runtime installieren |
| Alter Runner-Befehl abgelehnt | dotnet test --solution Trexon.Logging.slnx verwenden |
| Nach Runner-Wechsel null Tests | Restore und Build wiederholen, danach test --no-build |
| Altes lokales Paket | Eindeutige Vorabversion oder eigenen Paketcache verwenden |
| GitHub-Veröffentlichung scheitert | NUGET_USER, Richtlinie, Umgebung und Tag prüfen |

Siehe [Entwicklung](DEVELOPMENT.md) und [Release-Diagnose](PUBLISHING.md).
