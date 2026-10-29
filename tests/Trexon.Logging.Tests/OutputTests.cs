using System.Text;
using System.Text.Json;

namespace Trexon.Logging.Tests;

public sealed class OutputTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Text_and_json_keep_multilingual_properties_readable(bool useJsonElement, bool redact)
    {
        const string name = "Антон — café Ελληνικά 中文 العربية <>&+'";
        var payload = new Dictionary<string, object?>
        {
            ["Имя"] = name,
            ["Вложенное"] = new { Values = new[] { name }, Token = "secret" }
        };
        object value = useJsonElement ? JsonSerializer.SerializeToElement(payload) : payload;
        using var directory = new TemporaryDirectory();
        var path = directory.File("unicode.jsonl");
        var writer = new StringWriter();
        using (var logger = Logger.Create(c =>
        {
            if (redact) c.Redact("secret", "скрыто");
            c.WriteTo.Console(o => o.Writer(writer).Timestamp(null))
                .WriteTo.File(path, o => o.Json());
        }))
        {
            logger.Error(new InvalidOperationException("Ошибка оплаты"), "Покупатель {Customer}", value);
        }

        var text = writer.ToString();
        var output = System.IO.File.ReadAllText(path, new UTF8Encoding(false, true));
        foreach (var rendered in new[] { text, output })
        {
            Assert.Contains("Покупатель", rendered);
            Assert.Contains(name, rendered);
            Assert.Contains("Имя", rendered);
            Assert.Contains("Ошибка оплаты", rendered);
            Assert.DoesNotContain("\\u041", rendered);
            if (redact)
            {
                Assert.Contains("скрыто", rendered);
                Assert.DoesNotContain("secret", rendered);
            }
        }
        using var json = JsonDocument.Parse(output);
        var customer = json.RootElement.GetProperty("properties").GetProperty("Customer");
        Assert.Equal(name, customer.GetProperty("Имя").GetString());
        Assert.Equal(name, customer.GetProperty("Вложенное").GetProperty("Values")[0].GetString());
        Assert.Contains(name, json.RootElement.GetProperty("message").GetString()!);
        Assert.Single(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Readable_json_still_escapes_syntax_and_round_trips_control_characters_and_emoji()
    {
        const string value = "Привет \"мир\"\n\r\t\0\\path \\u0410 😀";
        var writer = new StringWriter();
        using var logger = Logger.Create(c => c.WriteTo.Console(o => o.Writer(writer).Json()));
        logger.Info("Значение {Value}", value);

        var output = writer.ToString();
        using var json = JsonDocument.Parse(output);
        Assert.Contains("Привет", output);
        Assert.Contains("\\\"мир\\\"", output);
        Assert.Equal(value, json.RootElement.GetProperty("properties").GetProperty("Value").GetString());
        Assert.Equal("Значение " + value, json.RootElement.GetProperty("message").GetString());
        Assert.Single(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Console_text_uses_the_configured_timestamp_and_time_zone()
    {
        var writer = new StringWriter();
        var now = new DateTimeOffset(2026, 10, 29, 12, 34, 56, TimeSpan.Zero);
        using var logger = Logger.Create(c => c.WithTimeProvider(new FixedClock(now)).WriteTo.Console(o => o
            .Writer(writer).Timestamp("HH:mm:ss").TimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(3), "test", "test"))));
        logger.Info("Hello {Name}", "world");
        Assert.Equal("15:34:56 [INF] Hello world" + Environment.NewLine, writer.ToString());
    }

    [Theory]
    [InlineData(ConsoleColorMode.Always, true)]
    [InlineData(ConsoleColorMode.Never, false)]
    [InlineData(ConsoleColorMode.Auto, false)]
    public void Console_colors_follow_the_selected_mode(ConsoleColorMode mode, bool expected)
    {
        var writer = new StringWriter();
        using var logger = Logger.Create(c => c.WriteTo.Console(o => o.Writer(writer).Timestamp(null).Colors(mode)));
        logger.Warning("warning");
        Assert.Equal(expected, writer.ToString().Contains('\u001b'));
        Assert.Contains("[WRN] warning", writer.ToString());
    }

    [Fact]
    public void Json_is_one_plain_line_with_typed_properties_and_exception_details()
    {
        var writer = new StringWriter();
        using var logger = Logger.Create(c => c.WriteTo.Console(o => o.Writer(writer).Json().Colors(ConsoleColorMode.Always)));
        logger.Error(new InvalidOperationException("failed\nagain", new ArgumentException("inner")), "Order {Id}", 42);
        var output = writer.ToString();
        using var json = JsonDocument.Parse(output);
        Assert.Equal(42, json.RootElement.GetProperty("properties").GetProperty("Id").GetInt32());
        Assert.Equal("Order {Id}", json.RootElement.GetProperty("messageTemplate").GetString());
        Assert.Equal("Error", json.RootElement.GetProperty("level").GetString());
        Assert.Equal("inner", json.RootElement.GetProperty("exception").GetProperty("innerExceptions")[0].GetProperty("message").GetString());
        Assert.Single(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain('\u001b', output);
    }

    [Fact]
    public void Sinks_have_independent_levels_filters_and_formats()
    {
        var text = new StringWriter();
        var json = new StringWriter();
        using var logger = Logger.Create(c => c.MinimumLevel(LogLevel.Debug)
            .Filter(e => e.Message != "global-excluded")
            .WriteTo.Console(o => o.Writer(text).Timestamp(null).Filter(e => e.Message != "text-excluded"))
            .WriteTo.Console(o => o.Writer(json).Json().MinimumLevel(LogLevel.Error)));
        logger.Debug("debug");
        logger.Info("global-excluded");
        logger.Error("text-excluded");
        Assert.Equal("[DBG] debug" + Environment.NewLine, text.ToString());
        using var result = JsonDocument.Parse(json.ToString());
        Assert.Equal("text-excluded", result.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Text_exception_detail_can_be_turned_off_per_sink()
    {
        var writer = new StringWriter();
        using var logger = Logger.Create(c => c.WriteTo.Console(o => o.Writer(writer).Timestamp(null).ExceptionDetails(false)));
        logger.Error(new InvalidOperationException("outer", new ArgumentException("inner")), "failure");
        Assert.Contains("System.InvalidOperationException: outer", writer.ToString());
        Assert.DoesNotContain("inner", writer.ToString());
    }

    [Fact]
    public void Files_are_utf8_without_bom_and_append_across_logger_instances()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("nested/app.log");
        using (var logger = Logger.Create(c => c.WriteTo.File(path, o => o.Timestamp(null)))) logger.Info("Привет");
        using (var logger = Logger.Create(c => c.WriteTo.File(path, o => o.Timestamp(null)))) logger.Info("Мир");
        var bytes = System.IO.File.ReadAllBytes(path);
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Equal("[INF] Привет" + Environment.NewLine + "[INF] Мир" + Environment.NewLine, Encoding.UTF8.GetString(bytes));
        using var exclusive = System.IO.File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Concurrent_file_writes_preserve_complete_json_events()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("parallel.jsonl");
        using (var logger = Logger.Create(c => c.WriteTo.File(path, o => o.Json())))
        {
            Parallel.For(0, 500, id => logger.Info("Event {Id}", id));
            logger.Flush();
        }
        var ids = System.IO.File.ReadAllLines(path).Select(line =>
        {
            using var json = JsonDocument.Parse(line);
            return json.RootElement.GetProperty("properties").GetProperty("Id").GetInt32();
        }).Order().ToArray();
        Assert.Equal(Enumerable.Range(0, 500), ids);
    }
}
