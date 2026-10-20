namespace MyOwnDictionary.Core;

public enum Language
{
    En,
    Fa
}

public enum EntryOrigin
{
    Seed,
    Google,
    User
}

public enum DictionaryWriteResult
{
    Saved,
    Duplicate,
    NotFound,
    Invalid
}

public static class LanguageCodes
{
    public static string ToCode(Language language) => language == Language.Fa ? "fa" : "en";

    public static Language Parse(string code) =>
        string.Equals(code, "fa", StringComparison.OrdinalIgnoreCase) ? Language.Fa : Language.En;
}

public sealed record DictionaryEntry(
    long Id,
    Language SourceLanguage,
    string SourceText,
    string NormalizedText,
    string Translation,
    EntryOrigin Origin,
    DateTimeOffset CreatedAt);

public sealed record HistoryItem(
    long Id,
    Language SourceLanguage,
    string InputText,
    string Translation,
    EntryOrigin Origin,
    DateTimeOffset CreatedAt);

public sealed record TranslationResult(
    bool Success,
    string InputText,
    string Translation,
    Language SourceLanguage,
    Language TargetLanguage,
    EntryOrigin? Origin,
    string? Error)
{
    public static TranslationResult Failed(string input, Language source, string? error) =>
        new(false, input, string.Empty, source, LanguageDetector.Opposite(source), null, error);
}

public sealed record DictionaryStats(
    int TotalEntries,
    int GoogleCached,
    int EnglishHistory,
    int PersianHistory);

public sealed record AppSettings
{
    public const int MinimumTimeoutSeconds = 3;
    public const int MaximumTimeoutSeconds = 60;
    public const int DefaultTimeoutSeconds = 10;

    public bool IsDarkTheme { get; init; } = true;
    public bool OnlineFallbackEnabled { get; init; } = true;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;
    public string DataFolder { get; init; } = "";

    public AppSettings Normalized() => this with
    {
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, MinimumTimeoutSeconds, MaximumTimeoutSeconds),
        DataFolder = DataFolder?.Trim() ?? ""
    };
}

public enum QuizPool
{
    Learning,
    Mastered
}

public sealed record QuizWord(
    string Prompt,
    string NormalizedText,
    string Translation,
    Language SourceLanguage,
    int CorrectCount,
    DateTimeOffset? LastAskedAt);

public sealed record QuizScore(int CorrectCount, int WrongCount, bool Mastered, bool JustMastered);

public sealed record QuizPoolCounts(int Learning, int Mastered);
