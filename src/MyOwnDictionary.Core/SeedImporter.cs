using System.Reflection;

namespace MyOwnDictionary.Core;

public static class SeedImporter
{
    public const string SeededMetaKey = "Seeded";
    public const string SeedVersionKey = "SeedVersion";
    public const string CurrentSeedVersion = "2";
    public const string ResourceName = "MyOwnDictionary.Core.SeedData.en_fa.csv";

    public static async Task EnsureSeededAsync(AppDatabase database, CancellationToken cancellationToken = default)
    {
        if (await database.GetMetaAsync(SeedVersionKey, cancellationToken).ConfigureAwait(false) == CurrentSeedVersion)
            return;

        await database.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Entries WHERE Origin = 'Seed'";
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        await using var stream = OpenSeed();
        await ImportAsync(database, stream, cancellationToken).ConfigureAwait(false);
        await database.SetMetaAsync(SeedVersionKey, CurrentSeedVersion, cancellationToken).ConfigureAwait(false);
        await database.SetMetaAsync(SeededMetaKey, "1", cancellationToken).ConfigureAwait(false);
    }

    public static Stream OpenSeed()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded dictionary seed '{ResourceName}'.");
    }

    public static async Task<int> ImportAsync(AppDatabase database, Stream csv, CancellationToken cancellationToken = default)
    {
        var pairs = CsvPairReader.Read(csv);
        var repository = new DictionaryRepository(database);
        return await repository.ImportSeedAsync(pairs, cancellationToken).ConfigureAwait(false);
    }
}
