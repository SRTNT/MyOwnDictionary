namespace MyOwnDictionary.Core;

public sealed class GoogleFreeTranslationProvider : ITranslationProvider
{
    private const int ChunkSize = 1800;
    private readonly HttpClient _httpClient;
    private readonly SettingsStore _settings;

    public GoogleFreeTranslationProvider(HttpClient httpClient, SettingsStore settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
    }

    public async Task<string> TranslateAsync(
        string text,
        Language source,
        Language target,
        CancellationToken cancellationToken)
    {
        var parts = Chunk(text, ChunkSize).ToList();
        if (parts.Count == 1)
            return await TranslateWithRetryAsync(parts[0], source, target, cancellationToken).ConfigureAwait(false);

        var builder = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            var translated = await TranslateWithRetryAsync(part, source, target, cancellationToken).ConfigureAwait(false);
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(translated);
        }

        return builder.ToString();
    }

    private async Task<string> TranslateWithRetryAsync(
        string text,
        Language source,
        Language target,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await TranslateOnceAsync(text, source, target, attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or FormatException)
            {
                lastError = ex;
            }
        }

        throw new TranslationUnavailableException("Google Translate is unavailable.", lastError);
    }

    private async Task<string> TranslateOnceAsync(
        string text,
        Language source,
        Language target,
        int attempt,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = _settings.Current.TimeoutSeconds;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await _httpClient.GetAsync(BuildUrl(text, source, target, attempt), timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        return GoogleTranslateResponseParser.Parse(json);
    }

    internal static string BuildUrl(string text, Language source, Language target, int attempt = 0)
    {
        var sourceCode = source == Language.Fa ? "fa" : "en";
        var targetCode = target == Language.Fa ? "fa" : "en";
        var query = Uri.EscapeDataString(text);
        // The gtx host is the requested endpoint. It often answers 429, so the single retry
        // uses the same Google response format on clients5.
        var root = attempt == 0
            ? "https://translate.googleapis.com/translate_a/single?client=gtx"
            : "https://clients5.google.com/translate_a/single?client=dict-chrome-ex";
        return root + "&sl=" + sourceCode + "&tl=" + targetCode + "&dt=t&q=" + query;
    }

    internal static IEnumerable<string> Chunk(string text, int size)
    {
        if (text.Length <= size)
        {
            yield return text;
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(size, text.Length - start);
            if (start + length < text.Length)
            {
                var window = text.Substring(start, length);
                var breakAt = Math.Max(window.LastIndexOf('\n'), Math.Max(window.LastIndexOf('.'), Math.Max(window.LastIndexOf('؟'), window.LastIndexOf(' '))));
                if (breakAt > size / 3)
                    length = breakAt + 1;
            }

            var part = text.Substring(start, length).Trim();
            if (part.Length > 0)
                yield return part;

            start += length;
        }
    }
}
