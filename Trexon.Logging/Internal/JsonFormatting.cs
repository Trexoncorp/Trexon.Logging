using System.Text.Encodings.Web;
using System.Text.Json;

namespace Trexon.Logging.Internal;

internal static class JsonFormatting
{
    // Log output is UTF-8 JSON, so keep readable characters while escaping JSON syntax.
    internal static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
