using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MyOwnDictionary.Core;

public sealed class HistoryRepository
{
    private readonly AppDatabase _database;

    public HistoryRepository(AppDatabase database)
    {
        _database = database;
    }

    public Task AddAsync(
        Language sourceLanguage,
        string inputText,
        string translation,
        EntryOrigin origin,
        CancellationToken cancellationToken = default)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO History (SourceLang, InputText, Translation, Origin, CreatedAt)
                VALUES ($lang, $input, $translation, $origin, $created)
                """;
            command.Parameters.AddWithValue("$lang", LanguageCodes.ToCode(sourceLanguage));
            command.Parameters.AddWithValue("$input", inputText);
            command.Parameters.AddWithValue("$translation", translation);
            command.Parameters.AddWithValue("$origin", origin.ToString());
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<HistoryItem?> LatestAsync(Language sourceLanguage, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, SourceLang, InputText, Translation, Origin, CreatedAt
                FROM History
                WHERE SourceLang = $lang
                ORDER BY Id DESC
                LIMIT 1
                """;
            command.Parameters.AddWithValue("$lang", LanguageCodes.ToCode(sourceLanguage));
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            return await reader.ReadAsync(token).ConfigureAwait(false) ? ReadItem(reader) : null;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<HistoryItem>> ListAsync(
        Language? sourceLanguage,
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        var limitValue = Math.Clamp(limit, 1, 500);
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, SourceLang, InputText, Translation, Origin, CreatedAt
                FROM History
                WHERE ($all = 1 OR SourceLang = $lang)
                  AND ($empty = 1 OR InputText LIKE $like ESCAPE '\' OR Translation LIKE $like ESCAPE '\')
                ORDER BY Id DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$all", sourceLanguage is null ? 1 : 0);
            command.Parameters.AddWithValue("$lang", sourceLanguage is null ? string.Empty : LanguageCodes.ToCode(sourceLanguage.Value));
            command.Parameters.AddWithValue("$empty", trimmed.Length == 0 ? 1 : 0);
            command.Parameters.AddWithValue("$like", Like(trimmed));
            command.Parameters.AddWithValue("$limit", limitValue);

            var rows = new List<HistoryItem>();
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                rows.Add(ReadItem(reader));

            return (IReadOnlyList<HistoryItem>)rows;
        }, cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM History WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task ClearAsync(Language sourceLanguage, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM History WHERE SourceLang = $lang";
            command.Parameters.AddWithValue("$lang", LanguageCodes.ToCode(sourceLanguage));
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    private static string Like(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    private static HistoryItem ReadItem(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        LanguageCodes.Parse(reader.GetString(1)),
        reader.GetString(2),
        reader.GetString(3),
        Enum.Parse<EntryOrigin>(reader.GetString(4)),
        DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}
