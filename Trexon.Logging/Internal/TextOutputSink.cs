using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trexon.Logging.Internal;

internal sealed record OutputOptions(LogLevel Level, string? TimestampFormat, TimeZoneInfo TimeZone,
    bool Json, bool ExceptionDetails, bool Caller, Func<LogEvent, bool>? Predicate);

internal sealed record SinkRegistration(ILogSink Sink, LogLevel Level, Func<LogEvent, bool>? Filter, bool Owned);

internal sealed class TextOutputSink(TextWriter writer, OutputOptions options, ConsoleColorMode colorMode, bool ownsWriter) : ILogSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonFormatting.Options)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Emit(LogEvent logEvent) => writer.WriteLine(options.Json ? FormatJson(logEvent) : FormatText(logEvent));
    public void Flush() => writer.Flush();
    public void Dispose() { if (ownsWriter) writer.Dispose(); }

    private string FormatJson(LogEvent e) => JsonSerializer.Serialize(new
    {
        e.Timestamp,
        Level = e.Level.ToString(),
        e.MessageTemplate,
        e.Message,
        e.Properties,
        Caller = options.Caller ? e.Caller : null,
        Exception = e.Exception is null ? null : ExceptionObject(e.Exception)
    }, JsonOptions);

    private object ExceptionObject(ExceptionInfo exception) => new
    {
        exception.Type,
        exception.Message,
        StackTrace = options.ExceptionDetails ? exception.StackTrace : null,
        InnerExceptions = options.ExceptionDetails ? exception.InnerExceptions.Select(ExceptionObject).ToArray() : null
    };

    private string FormatText(LogEvent e)
    {
        var text = new StringBuilder();
        if (options.TimestampFormat is not null)
            text.Append(TimeZoneInfo.ConvertTime(e.Timestamp, options.TimeZone)
                .ToString(options.TimestampFormat, CultureInfo.InvariantCulture)).Append(' ');
        text.Append('[').Append(e.Level switch
        {
            LogLevel.Trace => "TRC", LogLevel.Debug => "DBG", LogLevel.Information => "INF",
            LogLevel.Warning => "WRN", LogLevel.Error => "ERR", LogLevel.Critical => "CRT", _ => "???"
        }).Append("] ").Append(e.Message);
        if (options.Caller && e.Caller is { } caller)
        {
            text.Append(" (").Append(caller.MemberName);
            if (caller.FilePath is not null)
                text.Append(" at ").Append(Path.GetFileName(caller.FilePath)).Append(':').Append(caller.LineNumber);
            text.Append(')');
        }
        if (e.Exception is not null) AppendException(text, e.Exception, "");
        if (!UseColors()) return text.ToString();
        var color = e.Level switch
        {
            LogLevel.Trace => "90", LogLevel.Debug => "36", LogLevel.Information => "32",
            LogLevel.Warning => "33", LogLevel.Error => "31", LogLevel.Critical => "1;31", _ => "0"
        };
        return $"\u001b[{color}m{text}\u001b[0m";
    }

    private void AppendException(StringBuilder text, ExceptionInfo exception, string indent)
    {
        text.AppendLine().Append(indent).Append(exception.Type).Append(": ").Append(exception.Message);
        if (!options.ExceptionDetails) return;
        if (exception.StackTrace is not null) text.AppendLine().Append(indent).Append(exception.StackTrace);
        foreach (var inner in exception.InnerExceptions) AppendException(text, inner, indent + "  ");
    }

    private bool UseColors() => colorMode switch
    {
        ConsoleColorMode.Always => true,
        ConsoleColorMode.Never => false,
        _ => (ReferenceEquals(writer, Console.Error) && !Console.IsErrorRedirected)
            || (ReferenceEquals(writer, Console.Out) && !Console.IsOutputRedirected)
    };
}
