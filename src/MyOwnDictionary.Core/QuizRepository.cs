using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MyOwnDictionary.Core;

public sealed class QuizRepository
{
    public const int MasteryThreshold = 10;

    private readonly AppDatabase _database;

    public QuizRepository(AppDatabase database)
    {
        _database = database;
    }

    public async Task<QuizPoolCounts> CountAsync(Language language, CancellationToken cancellationToken = default)
    {
        var words = await LoadWordsAsync(language, cancellationToken).ConfigureAwait(false);
        var mastered = words.Count(word => word.CorrectCount >= MasteryThreshold);
        return new QuizPoolCounts(words.Count - mastered, mastered);
    }

    public async Task<IReadOnlyList<QuizWord>> ListAsync(
        Language language,
        QuizPool pool,
        CancellationToken cancellationToken = default)
    {
        var words = await LoadWordsAsync(language, cancellationToken).ConfigureAwait(false);
        IEnumerable<QuizWord> filtered = pool == QuizPool.Mastered
            ? words.Where(word => word.CorrectCount >= MasteryThreshold)
            : words.Where(word => word.CorrectCount < MasteryThreshold);

        return filtered
            .OrderBy(word => word.CorrectCount)
            .ThenBy(word => word.LastAskedAt ?? DateTimeOffset.MinValue)
            .ThenBy(_ => Random.Shared.Next())
            .ToList();
    }

    public async Task<IReadOnlyList<string>> BuildChoicesAsync(
        Language sourceLanguage,
        string correctTranslation,
        CancellationToken cancellationToken = default)
    {
        var target = LanguageDetector.Opposite(sourceLanguage);
        var wrong = await WrongAnswersAsync(sourceLanguage, correctTranslation, 3, cancellationToken).ConfigureAwait(false);
        var choices = new List<string> { correctTranslation.Trim() };
        choices.AddRange(wrong);
        return choices.OrderBy(_ => Random.Shared.Next()).ToList();
    }

    public Task<QuizScore> RecordAsync(
        Language language,
        string normalizedText,
        bool correct,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var lang = LanguageCodes.ToCode(language);
        return _database.ExecuteAsync(async (connection, token) =>
        {
            var beforeCorrect = 0;
            string? masteredAt = null;
            var beforeWrong = 0;
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = """
                    SELECT CorrectCount, WrongCount, MasteredAt
                    FROM QuizProgress
                    WHERE SourceLang = $lang AND NormalizedText = $norm
                    """;
                read.Parameters.AddWithValue("$lang", lang);
                read.Parameters.AddWithValue("$norm", normalizedText);
                await using var reader = await read.ExecuteReaderAsync(token).ConfigureAwait(false);
                if (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    beforeCorrect = reader.GetInt32(0);
                    beforeWrong = reader.GetInt32(1);
                    masteredAt = reader.IsDBNull(2) ? null : reader.GetString(2);
                }
            }

            var afterCorrect = beforeCorrect + (correct ? 1 : 0);
            var afterWrong = beforeWrong + (correct ? 0 : 1);
            var justMastered = correct && beforeCorrect < MasteryThreshold && afterCorrect >= MasteryThreshold;
            if (masteredAt is null && afterCorrect >= MasteryThreshold)
                masteredAt = now;

            await using var write = connection.CreateCommand();
            write.CommandText = """
                INSERT INTO QuizProgress (SourceLang, NormalizedText, CorrectCount, WrongCount, LastAskedAt, MasteredAt)
                VALUES ($lang, $norm, $correctCount, $wrongCount, $asked, $mastered)
                ON CONFLICT(SourceLang, NormalizedText) DO UPDATE SET
                    CorrectCount = excluded.CorrectCount,
                    WrongCount = excluded.WrongCount,
                    LastAskedAt = excluded.LastAskedAt,
                    MasteredAt = excluded.MasteredAt
                """;
            write.Parameters.AddWithValue("$lang", lang);
            write.Parameters.AddWithValue("$norm", normalizedText);
            write.Parameters.AddWithValue("$correctCount", afterCorrect);
            write.Parameters.AddWithValue("$wrongCount", afterWrong);
            write.Parameters.AddWithValue("$asked", now);
            write.Parameters.AddWithValue("$mastered", (object?)masteredAt ?? DBNull.Value);
            await write.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            return new QuizScore(afterCorrect, afterWrong, afterCorrect >= MasteryThreshold, justMastered);
        }, cancellationToken);
    }

    public static bool AnswersMatch(string? expected, string? typed, Language answerLanguage)
    {
        var typedNorm = TextNormalizer.Normalize(typed, answerLanguage);
        if (typedNorm.Length == 0)
            return false;

        foreach (var part in SplitMeanings(expected))
        {
            var expectedNorm = TextNormalizer.Normalize(part, answerLanguage);
            if (expectedNorm.Length > 0 && expectedNorm == typedNorm)
                return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<QuizWord>> LoadWordsAsync(Language language, CancellationToken cancellationToken)
    {
        var lang = LanguageCodes.ToCode(language);
        var progress = await _database.ExecuteAsync(async (connection, token) =>
        {
            var map = new Dictionary<string, (int Correct, DateTimeOffset? Asked)>(StringComparer.Ordinal);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT NormalizedText, CorrectCount, LastAskedAt
                FROM QuizProgress
                WHERE SourceLang = $lang
                """;
            command.Parameters.AddWithValue("$lang", lang);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                DateTimeOffset? asked = reader.IsDBNull(2)
                    ? null
                    : DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                map[reader.GetString(0)] = (reader.GetInt32(1), asked);
            }

            return map;
        }, cancellationToken).ConfigureAwait(false);

        var rows = await _database.ExecuteAsync(async (connection, token) =>
        {
            var list = new List<(string Input, string Translation)>();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT InputText, Translation
                FROM History
                WHERE SourceLang = $lang
                ORDER BY Id DESC
                """;
            command.Parameters.AddWithValue("$lang", lang);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                list.Add((reader.GetString(0), reader.GetString(1)));

            return list;
        }, cancellationToken).ConfigureAwait(false);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var words = new List<QuizWord>();
        foreach (var (input, translation) in rows)
        {
            if (TextNormalizer.CountWords(input) > TranslationService.DictionaryCacheWordLimit)
                continue;
            if (string.IsNullOrWhiteSpace(translation))
                continue;

            var normalized = TextNormalizer.Normalize(input, language);
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;

            progress.TryGetValue(normalized, out var score);
            words.Add(new QuizWord(input.Trim(), normalized, translation.Trim(), language, score.Correct, score.Asked));
        }

        return words;
    }

    private async Task<IReadOnlyList<string>> WrongAnswersAsync(
        Language sourceLanguage,
        string correctTranslation,
        int count,
        CancellationToken cancellationToken)
    {
        var target = LanguageDetector.Opposite(sourceLanguage);
        var banned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in SplitMeanings(correctTranslation))
        {
            var norm = TextNormalizer.Normalize(part, target);
            if (norm.Length > 0)
                banned.Add(norm);
        }

        var result = new List<string>();
        void TryAdd(string? text)
        {
            if (result.Count >= count || string.IsNullOrWhiteSpace(text))
                return;

            var norm = TextNormalizer.Normalize(text, target);
            if (norm.Length == 0 || !banned.Add(norm))
                return;

            result.Add(text.Trim());
        }

        var history = await LoadWordsAsync(sourceLanguage, cancellationToken).ConfigureAwait(false);
        foreach (var word in history.OrderBy(_ => Random.Shared.Next()))
            TryAdd(word.Translation);

        if (result.Count < count)
        {
            var fromEntries = await TranslationsAsync(sourceLanguage, true, cancellationToken).ConfigureAwait(false);
            foreach (var text in fromEntries)
                TryAdd(text);
        }

        if (result.Count < count)
        {
            var targetWords = await TranslationsAsync(target, false, cancellationToken).ConfigureAwait(false);
            foreach (var text in targetWords)
                TryAdd(text);
        }

        return result;
    }

    private Task<IReadOnlyList<string>> TranslationsAsync(Language language, bool translationColumn, CancellationToken cancellationToken)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            var list = new List<string>();
            await using var command = connection.CreateCommand();
            command.CommandText = translationColumn
                ? """
                  SELECT Translation FROM Entries
                  WHERE SourceLang = $lang AND Translation <> ''
                  ORDER BY RANDOM()
                  LIMIT 40
                  """
                : """
                  SELECT SourceText FROM Entries
                  WHERE SourceLang = $lang AND SourceText <> ''
                  ORDER BY RANDOM()
                  LIMIT 40
                  """;
            command.Parameters.AddWithValue("$lang", LanguageCodes.ToCode(language));
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                list.Add(reader.GetString(0));

            return (IReadOnlyList<string>)list;
        }, cancellationToken);
    }

    private static IEnumerable<string> SplitMeanings(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var part in text.Split(['،', ',', ';', '؛'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return part;
    }
}
