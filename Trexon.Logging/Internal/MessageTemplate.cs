using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trexon.Logging.Internal;

internal static class MessageTemplate
{
    internal static Dictionary<string, object?> Bind(string template, object?[] values)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var index = 0;
        foreach (var part in Parse(template))
        {
            if (part.Name is not null && !properties.ContainsKey(part.Name) && index < values.Length)
                properties[part.Name] = PropertyValues.Capture(values[index++]);
        }
        while (index < values.Length)
        {
            var name = "_arg" + index.ToString(CultureInfo.InvariantCulture);
            while (properties.ContainsKey(name)) name += "_";
            properties[name] = PropertyValues.Capture(values[index++]);
        }
        return properties;
    }

    internal static string Render(string template, IReadOnlyDictionary<string, object?> properties)
    {
        var builder = new StringBuilder();
        foreach (var part in Parse(template))
        {
            if (part.Name is null || !properties.TryGetValue(part.Name, out var value))
            {
                builder.Append(part.Text);
                continue;
            }
            builder.Append(value switch
            {
                null => "null",
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                JsonElement element => JsonSerializer.Serialize(element, JsonFormatting.Options),
                IFormattable formattable => formattable.ToString(part.Format, CultureInfo.InvariantCulture),
                _ => value.ToString()
            });
        }
        return builder.ToString();
    }

    private static IEnumerable<Part> Parse(string template)
    {
        var literal = new StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (i + 1 < template.Length && ((c == '{' && template[i + 1] == '{') || (c == '}' && template[i + 1] == '}')))
            {
                literal.Append(c);
                i++;
                continue;
            }
            if (c == '{')
            {
                var end = template.IndexOf('}', i + 1);
                if (end >= 0)
                {
                    var body = template[(i + 1)..end];
                    var colon = body.IndexOf(':');
                    var name = colon < 0 ? body : body[..colon];
                    if (name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_')
                        && name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_'))
                    {
                        if (literal.Length > 0) { yield return new Part(literal.ToString()); literal.Clear(); }
                        yield return new Part(template[i..(end + 1)], name, colon < 0 ? null : body[(colon + 1)..]);
                    }
                    else literal.Append(template, i, end - i + 1);
                    i = end;
                    continue;
                }
            }
            literal.Append(c);
        }
        if (literal.Length > 0) yield return new Part(literal.ToString());
    }

    private sealed record Part(string Text, string? Name = null, string? Format = null);
}
