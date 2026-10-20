namespace MyOwnDictionary.Core;

public static class LanguageDetector
{
    public static Language Detect(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Language.En;

        foreach (var ch in text)
        {
            if (IsPersianScript(ch))
                return Language.Fa;
        }

        return Language.En;
    }

    public static Language Opposite(Language language) =>
        language == Language.Fa ? Language.En : Language.Fa;

    public static bool IsPersianScript(char ch) =>
        ch is >= '\u0600' and <= '\u06FF'
            or >= '\u0750' and <= '\u077F'
            or >= '\u08A0' and <= '\u08FF'
            or >= '\uFB50' and <= '\uFDFF'
            or >= '\uFE70' and <= '\uFEFF';
}
