using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MyOwnDictionary.Core;

public sealed class DictionaryRepository
{
    private readonly AppDatabase _database;

    public DictionaryRepository(AppDatabase database)
    {
        _database = database;
    }

    public Task<DictionaryEntry?> FindAsync(Language language, string normalizedText, CancellationToken cancellationToken = default)
    {
        if (normalizedText.Length == 0)
            return Task.FromResult<DictionaryEntry?>(null);

        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, SourceLang, SourceText, NormalizedText, Translation, Origin, CreatedAt
                FROM Entries
                WHERE SourceLang = $lang AND NormalizedText = $norm
                LIMIT 1
                """;
            command.Parameters.AddWithValue("$lang", LanguageCodes.ToCode(language));
            command.Parameters.AddWithValue("$norm", normalizedText);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            return await reader.ReadAsync(token).ConfigureAwait(false) ? ReadEntry(reader) : null;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<DictionaryEntry>> SearchAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        var english = TextNormalizer.Normalize(trimmed, Language.En);
        var persian = TextNormalizer.Normalize(trimmed, Language.Fa);
        var limitValue = Math.Clamp(limit, 1, 500);

        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, SourceLang, SourceText, NormalizedText, Translation, Origin, CreatedAt
                FROM Entries
                WHERE $empty = 1
                   OR NormalizedText LIKE $english ESCAPE '\'
                   OR NormalizedText LIKE $persian ESCAPE '\'
                   OR Translation LIKE $english ESCAPE '\'
                   OR Translation LIKE $persian ESCAPE '\'
                ORDER BY
                    CASE WHEN NormalizedText = $exactEn OR NormalizedText = $exactFa THEN 0 ELSE 1 END,
                    length(NormalizedText),
                    Id DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$empty", trimmed.Length == 0 ? 1 : 0);
            command.Parameters.AddWithValue("$english", Like(english));
            command.Parameters.AddWithValue("$persian", Like(persian));
            command.Parameters.AddWithValue("$exactEn", english);
            command.Parameters.AddWithValue("$exactFa", persian);
            command.Parameters.AddWithValue("$limit", limitValue);

            var rows = new List<DictionaryEntry>();
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                rows.Add(ReadEntry(reader));

            return (IReadOnlyList<DictionaryEntry>)rows;
        }, cancellationToken);
    }

    public Task<DictionaryWriteResult> AddUserAsync(
        Language language,
        string sourceText,
        string translation,
        CancellationToken cancellationToken = default)
    {
        var trimmedSource = sourceText.Trim();
        var trimmedTranslation = translation.Trim();
        var normalized = TextNormalizer.Normalize(trimmedSource, language);
        if (trimmedSource.Length == 0 || trimmedTranslation.Length == 0 || normalized.Length == 0)
            return Task.FromResult(DictionaryWriteResult.Invalid);

        return InsertAsync(language, trimmedSource, normalized, trimmedTranslation, EntryOrigin.User, ignoreDuplicates: false, cancellationToken);
    }

    public Task<DictionaryWriteResult> UpdateUserAsync(
        long id,
        Language language,
        string sourceText,
        string translation,
        CancellationToken cancellationToken = default)
    {
        var trimmedSource = sourceText.Trim();
        var trimmedTranslation = translation.Trim();
        var normalized = TextNormalizer.Normalize(trimmedSource, language);
        if (trimmedSource.Length == 0 || trimmedTranslation.Length == 0 || normalized.Length == 0)
            return Task.FromResult(DictionaryWriteResult.Invalid);

        return _database.ExecuteAsync(async (connection, token) =>
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE Entries
                    SET SourceLang = $lang,
                        SourceText = $source,
                        NormalizedText = $norm,
                        Translation = $translation,
                        Origin = $origin
                    WHERE Id = $id
                    """;
                command.Parameters.AddWithValue("$lang", LanguageCodes.ToCode(language));
                command.Parameters.AddWithValue("$source", trimmedSource);
                command.Parameters.AddWithValue("$norm", normalized);
                command.Parameters.AddWithValue("$translation", trimmedTranslation);
                command.Parameters.AddWithValue("$origin", nameof(EntryOrigin.User));
                command.Parameters.AddWithValue("$id", id);
                var changed = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return changed == 0 ? DictionaryWriteResult.NotFound : DictionaryWriteResult.Saved;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return DictionaryWriteResult.Duplicate;
            }
        }, cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Entries WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task CacheGoogleAsync(
        Language language,
        string sourceText,
        string normalizedText,
        string translation,
        CancellationToken cancellationToken = default)
    {
        return InsertAsync(language, sourceText.Trim(), normalizedText, translation.Trim(), EntryOrigin.Google, ignoreDuplicates: true, cancellationToken);
    }

    public Task<int> ImportSeedAsync(
        IReadOnlyList<(string English, string Persian)> pairs,
        CancellationToken cancellationToken = default)
    {
        var createdAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO Entries (SourceLang, SourceText, NormalizedText, Translation, Origin, CreatedAt)
                VALUES ($lang, $source, $norm, $translation, $origin, $created)
                """;
            var language = command.Parameters.Add("$lang", SqliteType.Text);
            var source = command.Parameters.Add("$source", SqliteType.Text);
            var normalized = command.Parameters.Add("$norm", SqliteType.Text);
            var translation = command.Parameters.Add("$translation", SqliteType.Text);
            command.Parameters.AddWithValue("$origin", nameof(EntryOrigin.Seed));
            command.Parameters.AddWithValue("$created", createdAt);

            var inserted = 0;
            foreach (var (english, persian) in pairs)
            {
                inserted += await InsertDirectionAsync(command, language, source, normalized, translation, Language.En, english, persian, token).ConfigureAwait(false);
                inserted += await InsertDirectionAsync(command, language, source, normalized, translation, Language.Fa, persian, english, token).ConfigureAwait(false);
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return inserted;
        }, cancellationToken);
    }

    public Task<DictionaryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM Entries),
                    (SELECT COUNT(*) FROM Entries WHERE Origin = 'Google'),
                    (SELECT COUNT(*) FROM History WHERE SourceLang = 'en'),
                    (SELECT COUNT(*) FROM History WHERE SourceLang = 'fa')
                """;
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await reader.ReadAsync(token).ConfigureAwait(false);
            return new DictionaryStats(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
        }, cancellationToken);
    }

    private async Task<DictionaryWriteResult> InsertAsync(
        Language language,
        string sourceText,
        string normalizedText,
        string translation,
        EntryOrigin origin,
        bool ignoreDuplicates,
        CancellationToken cancellationToken)
    {
        if (sourceText.Length == 0 || normalizedText.Length == 0 || translation.Length == 0)
            return DictionaryWriteResult.Invalid;

        return await _database.ExecuteAsync(async (connection, token) =>
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ignoreDuplicates
                    ? """
                      INSERT OR IGNORE INTO Entries (SourceLang, SourceText, NormalizedText, Translation, Origin, CreatedAt)
                      VALUES ($lang, $source, $norm, $translation, $origin, $created)
                      """
                    : """
                      INSERT INTO Entries (SourceLang, SourceText, NormalizedText, Translation, Origin, CreatedAt)
                      VALUES ($lang, $source, $norm, $translation, $origin, $created)
                      """;
                command.Parameters.AddWithValue("$lang", LanguageCodes.ToCode(language));
                command.Parameters.AddWithValue("$source", sourceText);
                command.Parameters.AddWithValue("$norm", normalizedText);
                command.Parameters.AddWithValue("$translation", translation);
                command.Parameters.AddWithValue("$origin", origin.ToString());
                command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                var changed = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return changed == 0 ? DictionaryWriteResult.Duplicate : DictionaryWriteResult.Saved;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return DictionaryWriteResult.Duplicate;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> InsertDirectionAsync(
        SqliteCommand command,
        SqliteParameter language,
        SqliteParameter source,
        SqliteParameter normalized,
        SqliteParameter translation,
        Language sourceLanguage,
        string sourceText,
        string translationText,
        CancellationToken cancellationToken)
    {
        var trimmedSource = sourceText.Trim();
        var trimmedTranslation = translationText.Trim();
        var normalizedText = TextNormalizer.Normalize(trimmedSource, sourceLanguage);
        if (trimmedSource.Length == 0 || trimmedTranslation.Length == 0 || normalizedText.Length == 0)
            return 0;

        language.Value = LanguageCodes.ToCode(sourceLanguage);
        source.Value = trimmedSource;
        normalized.Value = normalizedText;
        translation.Value = trimmedTranslation;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Like(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    internal static DictionaryEntry ReadEntry(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        LanguageCodes.Parse(reader.GetString(1)),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        Enum.Parse<EntryOrigin>(reader.GetString(5)),
        DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}
