using System.Text;
using System.Text.Json;

namespace MyOwnDictionary.Core;

public static class GoogleTranslateResponseParser
{
    public static string Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            throw new FormatException("Unexpected Google Translate response.");

        var segments = root[0];
        if (segments.ValueKind != JsonValueKind.Array)
            throw new FormatException("Unexpected Google Translate response.");

        var builder = new StringBuilder();
        foreach (var segment in segments.EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.Array || segment.GetArrayLength() == 0)
                continue;

            if (segment[0].ValueKind == JsonValueKind.String)
                builder.Append(segment[0].GetString());
        }

        var translation = builder.ToString().Trim();
        if (translation.Length == 0)
            throw new FormatException("Google Translate returned an empty translation.");

        return translation;
    }
}
