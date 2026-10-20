using Microsoft.Data.Sqlite;

namespace MyOwnDictionary.Core;

public sealed class AppDatabase : IAsyncDisposable, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS Entries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SourceLang TEXT NOT NULL,
            SourceText TEXT NOT NULL,
            NormalizedText TEXT NOT NULL,
            Translation TEXT NOT NULL,
            Origin TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            UNIQUE(SourceLang, NormalizedText)
        );

        CREATE TABLE IF NOT EXISTS History (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SourceLang TEXT NOT NULL,
            InputText TEXT NOT NULL,
            Translation TEXT NOT NULL,
            Origin TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_History_Lang_Created ON History(SourceLang, CreatedAt);

        CREATE TABLE IF NOT EXISTS Meta (
            "Key" TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS QuizProgress (
            SourceLang TEXT NOT NULL,
            NormalizedText TEXT NOT NULL,
            CorrectCount INTEGER NOT NULL DEFAULT 0,
            WrongCount INTEGER NOT NULL DEFAULT 0,
            LastAskedAt TEXT,
            MasteredAt TEXT,
            PRIMARY KEY (SourceLang, NormalizedText)
        );
        """;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    static AppDatabase()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public AppDatabase(string databasePath, bool useDeleteJournal = false)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = useDeleteJournal
            ? "PRAGMA journal_mode=DELETE;"
            : "PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();
    }

    public Task BackupToAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return ExecuteAsync(async (connection, token) =>
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };
            await using var destination = new SqliteConnection(builder.ToString());
            await destination.OpenAsync(token).ConfigureAwait(false);
            connection.BackupDatabase(destination);
        }, cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Schema;
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        if (await GetMetaAsync("SchemaVersion", cancellationToken).ConfigureAwait(false) is null)
            await SetMetaAsync("SchemaVersion", "1", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetMetaAsync(string key, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """SELECT Value FROM Meta WHERE "Key" = $key LIMIT 1""";
            command.Parameters.AddWithValue("$key", key);
            var result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return result as string;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task SetMetaAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Meta ("Key", Value) VALUES ($key, $value)
                ON CONFLICT("Key") DO UPDATE SET Value = excluded.Value
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<SqliteConnection, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(_connection, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ExecuteAsync(
        Func<SqliteConnection, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async (connection, token) =>
        {
            await action(connection, token).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _connection.Dispose();
        _gate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
