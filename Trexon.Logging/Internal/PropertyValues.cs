using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trexon.Logging.Internal;

internal static class PropertyValues
{
    internal static object? Capture(object? value) => value switch
    {
        null or string or bool or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal or DateTime or DateTimeOffset or Guid or TimeSpan => value,
        char c => c.ToString(),
        Enum e => e.ToString(),
        JsonElement element => element.Clone(),
        _ => JsonSerializer.SerializeToElement(value, value.GetType(), JsonFormatting.Options)
    };

    internal static object? Redact(object? value, RedactionRule[] rules)
    {
        if (rules.Length == 0) return value;
        if (value is string text) return Apply(text, rules);
        if (value is not JsonElement element) return value;
        var node = JsonNode.Parse(element.GetRawText());
        return JsonSerializer.SerializeToElement(RedactNode(node, rules), JsonFormatting.Options);
    }

    private static JsonNode? RedactNode(JsonNode? node, RedactionRule[] rules)
    {
        if (node is JsonValue scalar && scalar.TryGetValue<string>(out var text))
            return JsonValue.Create(Apply(text, rules));
        if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
                array[i] = RedactNode(array[i], rules);
        }
        else if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(pair => pair.Key).ToArray())
                obj[key] = RedactNode(obj[key], rules);
        }
        return node;
    }

    internal static string Apply(string text, RedactionRule[] rules)
    {
        foreach (var rule in rules) text = rule.Pattern.Replace(text, rule.Replacement);
        return text;
    }
}

internal sealed record RedactionRule(System.Text.RegularExpressions.Regex Pattern, string Replacement);
