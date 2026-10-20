using System.Text;

namespace MyOwnDictionary.Core;

public static class CsvPairReader
{
    public static IReadOnlyList<(string English, string Persian)> Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var pairs = new List<(string English, string Persian)>();
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            if (!TrySplit(trimmed, out var english, out var persian))
                continue;

            if (english.Length == 0 || persian.Length == 0)
                continue;

            pairs.Add((english, persian));
        }

        return pairs;
    }

    public static bool TrySplit(string line, out string left, out string right)
    {
        left = string.Empty;
        right = string.Empty;
        var separator = IndexOfSeparator(line);
        if (separator <= 0 || separator >= line.Length - 1)
            return false;

        left = Unquote(line[..separator].Trim());
        right = Unquote(line[(separator + 1)..].Trim());
        return left.Length > 0 && right.Length > 0;
    }

    private static int IndexOfSeparator(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
                return i;
        }

        return -1;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);

        return value;
    }
}
