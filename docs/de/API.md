# Öffentliche API-Referenz

[Русский](../API.md) · [English](../en/API.md) · [Deutsch](../de/API.md)

[Übersicht](README.md) · [Verwendung](GETTING_STARTED.md) · [Erweiterungen](EXTENDING.md)

Alle Typen liegen in `Trexon.Logging`. Aufgeführt ist der selbst definierte
öffentliche MVP-Vertrag. Codeblöcke zeigen Signaturen, keine vollständigen Programme.
`?` kennzeichnet zulässiges null. Generierte Record-Gleichheitsmethoden und geerbte
.NET-Mitglieder werden nicht einzeln aufgeführt.

## Logger

`public sealed class Logger : IDisposable`. Kein öffentlicher Konstruktor.
Create liefert einen Root, Bind eine Kontextansicht.

```csharp
public static Logger Create(Action<LoggerConfiguration>? configure = null);
public bool IsEnabled(LogLevel level);

public Logger Bind(string name, object? value);
public Logger Bind(IReadOnlyDictionary<string, object?> properties);
public IDisposable PushScope(string name, object? value);
public IDisposable PushScope(IReadOnlyDictionary<string, object?> properties);

public void Write(LogLevel level, string messageTemplate, params object?[] values);
public void Write(LogLevel level, Exception exception, string messageTemplate,
    params object?[] values);

public void Trace(string messageTemplate, params object?[] values);
public void Debug(string messageTemplate, params object?[] values);
public void Info(string messageTemplate, params object?[] values);
public void Warning(string messageTemplate, params object?[] values);
public void Error(string messageTemplate, params object?[] values);
public void Error(Exception exception, string messageTemplate, params object?[] values);
public void Critical(string messageTemplate, params object?[] values);
public void Critical(Exception exception, string messageTemplate, params object?[] values);

public void Flush();
public void Dispose();
```

| Operation | Vertrag |
| --- | --- |
| Create | Einstellungen erfassen, Empfänger öffnen; ohne Empfänger Console ergänzen |
| IsEnabled | Stufen/Zustand prüfen, ohne Benutzerfilter aufzurufen |
| Bind | Eigenschaften in einer Ansicht mit gemeinsamem Root erfassen |
| PushScope | Eigenschaften im aktuellen asynchronen Ablauf erfassen; Scope zurückgeben |
| Write / Stufenmethoden | Synchrone Vorbereitung und Weitergabe |
| Flush | Jede Empfängerinstanz leeren, ohne sie zu schließen |
| Root-Dispose | Leeren und eigene Empfänger schließen; alle Ansichten ungültig machen |
| Ansichts-Dispose | Nur diese Ansicht schließen |

Validierung und geschlossene Zustände können Ausnahmen werfen. Ereignis- und
Empfängerfehler laufen über OnError. Siehe [Fehlertabelle](ERRORS_AND_LIFETIME.md).

## Log

`public static class Log` ist die globale Fassade.

```csharp
public static void Configure(Action<LoggerConfiguration> configure);
public static bool IsEnabled(LogLevel level);
public static Logger Bind(string name, object? value);
public static IDisposable PushScope(string name, object? value);

public static void Write(LogLevel level, string template, params object?[] values);
public static void Write(LogLevel level, Exception exception, string template,
    params object?[] values);
public static void Trace(string template, params object?[] values);
public static void Debug(string template, params object?[] values);
public static void Info(string template, params object?[] values);
public static void Warning(string template, params object?[] values);
public static void Error(string template, params object?[] values);
public static void Error(Exception exception, string template, params object?[] values);
public static void Critical(string template, params object?[] values);
public static void Critical(Exception exception, string template, params object?[] values);

public static void Flush();
public static void Shutdown();
```

Der ursprüngliche Root verwendet die Logger.Create-Standardwerte. Configure ersetzt
und schließt ihn. Shutdown schließt den aktuellen Root; Configure kann die Fassade
danach wieder öffnen. Log hat keine Wörterbuchüberladungen für Bind/PushScope.

## LoggerConfiguration

`public sealed class LoggerConfiguration` besitzt einen öffentlichen parameterlosen
Konstruktor. Logger.Create übergibt den Builder an seinen Callback; Build fehlt.

```csharp
public LoggerConfiguration();
public LoggerEnrichmentConfiguration Enrich { get; }
public LoggerSinkConfiguration WriteTo { get; }

public LoggerConfiguration MinimumLevel(LogLevel level);
public LoggerConfiguration Filter(Func<LogEvent, bool> predicate);
public LoggerConfiguration Redact(string pattern, string replacement = "***",
    RegexOptions options = RegexOptions.None, TimeSpan? timeout = null);
public LoggerConfiguration OnError(Action<LoggingFailure> handler);
public LoggerConfiguration WithTimeProvider(TimeProvider timeProvider);
```

| Methode | Standard / Wirkung |
| --- | --- |
| MinimumLevel | Information; None deaktiviert alle Ereignisse |
| Filter | Globalen AND-Filter nach der Maskierung ergänzen |
| Redact | Regex.Replace-Regel ergänzen; null-Zeitlimit entspricht 100 ms |
| OnError | Fehlerhandler ersetzen; dessen Ausnahmen unterdrücken |
| WithTimeProvider | TimeProvider.System ersetzen |

RegexOptions gehört zu System.Text.RegularExpressions. Haupteinstellungen werden
beim Erstellen des Kerns erfasst; der Builder steuert keinen bereits laufenden Logger.

## LoggerEnrichmentConfiguration

`public sealed class LoggerEnrichmentConfiguration`. Von der Bibliothek erstellt,
kein öffentlicher Konstruktor. Methoden liefern die Hauptkonfiguration zurück.

```csharp
public LoggerConfiguration WithCaller(bool enabled = true);
public LoggerConfiguration WithProperty(string name, object? value);
```

WithCaller untersucht den Stack für aktivierte Ereignisse. WithProperty ergänzt oder
ersetzt globale Eigenschaften; Namen dürfen nicht leer oder nur Leerraum sein.
Siehe [Kontext](CONTEXT.md).

## LoggerSinkConfiguration

`public sealed class LoggerSinkConfiguration`, über WriteTo verfügbar, ohne
öffentlichen Konstruktor.

```csharp
public LoggerConfiguration Console(Action<ConsoleSinkConfiguration>? configure = null);
public LoggerConfiguration File(string path,
    Action<FileSinkConfiguration>? configure = null);
public LoggerConfiguration Sink(ILogSink sink,
    LogLevel minimumLevel = LogLevel.Trace,
    Func<LogEvent, bool>? filter = null,
    bool disposeWithLogger = false);
```

Console merkt sich einen Writer im Besitz des Aufrufers. File wird beim Erstellen
des Kerns geöffnet und gehört dem Logger. Sink registriert eine externe Instanz,
standardmäßig im Besitz des Aufrufers. Jeder Aufruf ergänzt eine Registrierung.

## SinkConfiguration<TSelf>

Gemeinsame Basiskonfiguration:

```csharp
public abstract class SinkConfiguration<TSelf>
    where TSelf : SinkConfiguration<TSelf>
{
    public TSelf MinimumLevel(LogLevel level);
    public TSelf Timestamp(string? format);
    public TSelf TimeZone(TimeZoneInfo timeZone);
    public TSelf Json(bool enabled = true);
    public TSelf ExceptionDetails(bool enabled = true);
    public TSelf Caller(bool enabled = true);
    public TSelf Filter(Func<LogEvent, bool> predicate);
}
```

| Methode | Anfangswert | Bedeutung |
| --- | --- | --- |
| MinimumLevel | Trace | Zusätzliche Mindeststufe |
| Timestamp | yyyy-MM-dd HH:mm:ss.fff zzz | Textzeitformat; null blendet es aus |
| TimeZone | TimeZoneInfo.Utc | Zeitzone des Textzeitstempels |
| Json | false | Ein JSON-Objekt je Zeile |
| ExceptionDetails | true | Stack und innere Ausnahmen anzeigen |
| Caller | true | Erfassten Aufrufer anzeigen |
| Filter | Keiner | Lokalen AND-Filter ergänzen |

Timestamp/TimeZone ändern JSON-Zeitstempel nicht. Caller(true) aktiviert keine
Erfassung; dafür ist Enrich.WithCaller() nötig.

## ConsoleSinkConfiguration

`public sealed class ConsoleSinkConfiguration : SinkConfiguration<ConsoleSinkConfiguration>`.
Öffentlicher parameterloser Konstruktor; normalerweise durch den Console-Callback bereitgestellt.

```csharp
public ConsoleSinkConfiguration Colors(ConsoleColorMode mode = ConsoleColorMode.Auto);
public ConsoleSinkConfiguration Writer(TextWriter writer);
```

Geerbte Methoden liefern ConsoleSinkConfiguration zurück. Writer verwendet Console.Error
und lehnt null ab. Colors betrifft nur Text. Der Aufrufer behält den Writer-Besitz.

## FileSinkConfiguration

`public sealed class FileSinkConfiguration : SinkConfiguration<FileSinkConfiguration>`.
Öffentlicher parameterloser Konstruktor; nur geerbte Einstellungen. Methoden für
Rotation, Aufbewahrung, Komprimierung oder Colors fehlen. Den Pfad an WriteTo.File
übergeben; es gibt keinen Pfad-Setter.

## LogLevel

```csharp
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}
```

None ist eine abschaltende Mindeststufe, kein ausgegebenes Ereignis. Info entspricht Information.

## ConsoleColorMode

```csharp
public enum ConsoleColorMode
{
    Auto = 0,
    Always = 1,
    Never = 2
}
```

Auto prüft den aktuellen Standard-Writer und Umleitung. Beliebige TextWriter erhalten
keine automatischen Farben. Siehe [Palette](OUTPUT.md).

## LogEvent

`public sealed class LogEvent`, intern erstellt, ohne öffentlichen Konstruktor.

```csharp
public DateTimeOffset Timestamp { get; }
public LogLevel Level { get; }
public string MessageTemplate { get; }
public string Message { get; }
public IReadOnlyDictionary<string, object?> Properties { get; }
public ExceptionInfo? Exception { get; }
public CallerInfo? Caller { get; }
```

Vor der Weitergabe erfasst, mit schreibgeschütztem Wörterbuch und unabhängigen
JsonElement-Werten. Unterstützte Daten sind bereits maskiert. Exception enthält
eine Momentaufnahme statt der Originalausnahme.

## CallerInfo

Öffentlicher positional Record mit Konstruktor und generierten init-Eigenschaften:

```csharp
public sealed record CallerInfo(string MemberName, string? FilePath, int LineNumber);
```

FilePath kann null sein, LineNumber kann den Wert 0 haben. Die Erfassung hängt
von Stack, PDB und Optimierungen ab. Texte durchlaufen Redact-Regeln.

## ExceptionInfo

`public sealed class ExceptionInfo`, kein öffentlicher Konstruktor.

```csharp
public string Type { get; }
public string Message { get; }
public string? StackTrace { get; }
public IReadOnlyList<ExceptionInfo> InnerExceptions { get; }
```

Typ, Nachricht und Stack sind maskierte Momentaufnahmen. InnerExceptions enthält
auch AggregateException-Kinder bis zur Tiefengrenze. Siehe [Ausnahmen](ERRORS_AND_LIFETIME.md).

## ILogSink

```csharp
public interface ILogSink : IDisposable
{
    void Emit(LogEvent logEvent);
    void Flush();
}
```

Zusätzlich IDisposable.Dispose implementieren. Ein Root serialisiert seine Aufrufe.
Bei mehreren Roots ist eigene Synchronisierung nötig. [Vollständiges Beispiel](EXTENDING.md).

## LoggingFailure

Öffentlicher positional Record mit generierten init-Eigenschaften:

```csharp
public sealed record LoggingFailure(string Stage, Exception Exception);
```

Der Callback erhält den ursprünglichen technischen Fehler ohne automatische
Maskierung. Stage-Formate und OnError stehen unter [Fehler](ERRORS_AND_LIFETIME.md).
