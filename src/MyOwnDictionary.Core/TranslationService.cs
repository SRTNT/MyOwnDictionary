namespace MyOwnDictionary.Core;

public sealed class TranslationService
{
    public const int DictionaryCacheWordLimit = 5;

    private readonly DictionaryRepository _dictionary;
    private readonly HistoryRepository _history;
    private readonly ITranslationProvider _provider;
    private readonly SettingsStore _settings;

    public TranslationService(
        DictionaryRepository dictionary,
        HistoryRepository history,
        ITranslationProvider provider,
        SettingsStore settings)
    {
        _dictionary = dictionary;
        _history = history;
        _provider = provider;
        _settings = settings;
    }

    public async Task<TranslationResult> TranslateAsync(
        string? input,
        Language? sourceOverride,
        bool recordHistory,
        CancellationToken cancellationToken = default)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return TranslationResult.Failed(string.Empty, Language.En, null);

        var source = sourceOverride ?? LanguageDetector.Detect(trimmed);
        var normalized = TextNormalizer.Normalize(trimmed, source);
        if (normalized.Length == 0)
            return TranslationResult.Failed(trimmed, source, "Enter text to translate.");

        var entry = await _dictionary.FindAsync(source, normalized, cancellationToken).ConfigureAwait(false);
        if (entry is not null)
        {
            if (recordHistory)
                await RememberAsync(source, trimmed, entry.Translation, entry.Origin, cancellationToken).ConfigureAwait(false);

            return new TranslationResult(
                true,
                trimmed,
                entry.Translation,
                source,
                LanguageDetector.Opposite(source),
                entry.Origin,
                null);
        }

        if (!_settings.Current.OnlineFallbackEnabled)
        {
            return TranslationResult.Failed(
                trimmed,
                source,
                "Online translation is turned off, and this text is not in the offline dictionary.");
        }

        string translated;
        try
        {
            translated = (await _provider.TranslateAsync(trimmed, source, LanguageDetector.Opposite(source), cancellationToken).ConfigureAwait(false)).Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TranslationUnavailableException)
        {
            return TranslationResult.Failed(trimmed, source, "No offline entry, and Google Translate is unavailable.");
        }

        if (translated.Length == 0)
            return TranslationResult.Failed(trimmed, source, "Google Translate returned an empty translation.");

        if (TextNormalizer.CountWords(trimmed) <= DictionaryCacheWordLimit)
        {
            await _dictionary.CacheGoogleAsync(source, trimmed, normalized, translated, cancellationToken).ConfigureAwait(false);
        }

        if (recordHistory)
            await RememberAsync(source, trimmed, translated, EntryOrigin.Google, cancellationToken).ConfigureAwait(false);

        return new TranslationResult(
            true,
            trimmed,
            translated,
            source,
            LanguageDetector.Opposite(source),
            EntryOrigin.Google,
            null);
    }

    private async Task RememberAsync(
        Language source,
        string input,
        string translation,
        EntryOrigin origin,
        CancellationToken cancellationToken)
    {
        var latest = await _history.LatestAsync(source, cancellationToken).ConfigureAwait(false);
        if (latest is not null
            && string.Equals(latest.InputText, input, StringComparison.Ordinal)
            && string.Equals(latest.Translation, translation, StringComparison.Ordinal))
        {
            return;
        }

        await _history.AddAsync(source, input, translation, origin, cancellationToken).ConfigureAwait(false);
    }
}
