using System.Globalization;
using System.Text;

namespace MyOwnDictionary.Core;

public static class TextNormalizer
{
    public static string Normalize(string? input, Language language)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var text = input.Trim().Normalize(NormalizationForm.FormC);
        text = text.Trim('"', '\'', '“', '”', '«', '»', '.', ',', '!', '?', ':', ';', '،', '؛', '؟', '(', ')', '[', ']');

        if (language == Language.En)
            return CollapseWhitespace(text.ToLower(CultureInfo.InvariantCulture));

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case 'ي':
                case 'ى':
                case 'ې':
                    builder.Append('ی');
                    break;
                case 'ك':
                    builder.Append('ک');
                    break;
                case 'ة':
                    builder.Append('ه');
                    break;
                case 'أ':
                case 'إ':
                case 'ٱ':
                    builder.Append('ا');
                    break;
                case 'ؤ':
                    builder.Append('و');
                    break;
                case '\u0640':
                case '\u200C':
                case '\u200D':
                case '\u200B':
                case '\uFEFF':
                    break;
                case '\u00A0':
                    builder.Append(' ');
                    break;
                case '۰':
                case '٠':
                    builder.Append('0');
                    break;
                case '۱':
                case '١':
                    builder.Append('1');
                    break;
                case '۲':
                case '٢':
                    builder.Append('2');
                    break;
                case '۳':
                case '٣':
                    builder.Append('3');
                    break;
                case '۴':
                case '٤':
                    builder.Append('4');
                    break;
                case '۵':
                case '٥':
                    builder.Append('5');
                    break;
                case '۶':
                case '٦':
                    builder.Append('6');
                    break;
                case '۷':
                case '٧':
                    builder.Append('7');
                    break;
                case '۸':
                case '٨':
                    builder.Append('8');
                    break;
                case '۹':
                case '٩':
                    builder.Append('9');
                    break;
                default:
                    if (IsDiacritic(ch))
                        break;
                    builder.Append(ch);
                    break;
            }
        }

        return CollapseWhitespace(builder.ToString());
    }

    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || ch == '\u200C')
            {
                inWord = false;
                continue;
            }

            if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    private static bool IsDiacritic(char ch) =>
        ch is >= '\u064B' and <= '\u065F' or '\u0670' or >= '\u06D6' and <= '\u06ED';

    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
