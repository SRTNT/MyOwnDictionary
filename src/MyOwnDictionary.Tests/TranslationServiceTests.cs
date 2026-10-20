using MyOwnDictionary.Core;

namespace MyOwnDictionary.Tests;

public class TranslationServiceTests
{
    [Fact]
    public async Task OfflineHit_DoesNotCallProvider_AndRecordsHistory()
    {
        await using var app = await TestApp.CreateAsync();
        await app.Dictionary.AddUserAsync(Language.En, "book", "کتاب");

        var result = await app.Translator.TranslateAsync("  Book  ", null, recordHistory: true, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("کتاب", result.Translation);
        Assert.Equal(EntryOrigin.User, result.Origin);
        Assert.Equal(Language.En, result.SourceLanguage);
        Assert.Equal(0, app.Provider.Calls);
        var history = await app.History.ListAsync(Language.En, null, 20);
        Assert.Single(history);
        Assert.Empty(await app.History.ListAsync(Language.Fa, null, 20));
    }

    [Fact]
    public async Task MissingShortPhrase_UsesProvider_Caches_AndRecordsHistory()
    {
        await using var app = await TestApp.CreateAsync();
        app.Provider.Handler = (_, _, _) => "ناشناخته";

        var result = await app.Translator.TranslateAsync("widget", null, true, CancellationToken.None);

        Assert.Equal(EntryOrigin.Google, result.Origin);
        Assert.Equal("ناشناخته", result.Translation);
        Assert.Equal(1, app.Provider.Calls);
        var cached = await app.Dictionary.FindAsync(Language.En, "widget");
        Assert.NotNull(cached);
        Assert.Equal(EntryOrigin.Google, cached.Origin);

        var again = await app.Translator.TranslateAsync("widget", null, true, CancellationToken.None);
        Assert.Equal(EntryOrigin.Google, again.Origin);
        Assert.Equal(1, app.Provider.Calls);
        Assert.Single(await app.History.ListAsync(Language.En, null, 20));
    }

    [Fact]
    public async Task LongText_IsNotCached()
    {
        await using var app = await TestApp.CreateAsync();
        app.Provider.Handler = (text, _, _) => "ترجمه " + text;
        var input = "one two three four five six";

        var result = await app.Translator.TranslateAsync(input, Language.En, true, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(await app.Dictionary.FindAsync(Language.En, input));
        Assert.Single(await app.History.ListAsync(Language.En, null, 10));
    }

    [Fact]
    public async Task OfflineMode_DoesNotCallProvider()
    {
        await using var app = await TestApp.CreateAsync();
        app.Settings.Save(app.Settings.Current with { OnlineFallbackEnabled = false });

        var result = await app.Translator.TranslateAsync("missing word", null, true, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, app.Provider.Calls);
        Assert.Contains("turned off", result.Error);
        Assert.Empty(await app.History.ListAsync(Language.En, null, 10));
    }

    [Fact]
    public async Task ProviderFailure_ReturnsFriendlyError()
    {
        await using var app = await TestApp.CreateAsync();
        app.Provider.Failure = new TranslationUnavailableException("down");

        var result = await app.Translator.TranslateAsync("missing", null, true, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("No offline entry, and Google Translate is unavailable.", result.Error);
        Assert.Empty(await app.History.ListAsync(Language.En, null, 10));
    }

    [Fact]
    public async Task PersianInput_IsStoredInPersianHistory()
    {
        await using var app = await TestApp.CreateAsync();
        app.Provider.Handler = (_, _, _) => "book";

        var result = await app.Translator.TranslateAsync("کتابخانه", null, true, CancellationToken.None);

        Assert.Equal(Language.Fa, result.SourceLanguage);
        Assert.Equal(Language.En, result.TargetLanguage);
        Assert.Single(await app.History.ListAsync(Language.Fa, null, 10));
        Assert.Empty(await app.History.ListAsync(Language.En, null, 10));
    }

    [Fact]
    public async Task ClearHistory_AffectsOnlyThatLanguage()
    {
        await using var app = await TestApp.CreateAsync();
        await app.History.AddAsync(Language.En, "book", "کتاب", EntryOrigin.Seed);
        await app.History.AddAsync(Language.Fa, "کتاب", "book", EntryOrigin.Seed);

        await app.History.ClearAsync(Language.En);

        Assert.Empty(await app.History.ListAsync(Language.En, null, 10));
        Assert.Single(await app.History.ListAsync(Language.Fa, null, 10));
    }

    [Fact]
    public async Task RepeatedTranslation_DoesNotDuplicateLatestHistory()
    {
        await using var app = await TestApp.CreateAsync();
        await app.Dictionary.AddUserAsync(Language.En, "water", "آب");

        await app.Translator.TranslateAsync("water", null, true, CancellationToken.None);
        await app.Translator.TranslateAsync("water", null, true, CancellationToken.None);

        Assert.Single(await app.History.ListAsync(Language.En, null, 10));
    }
}

public class DictionaryRepositoryTests
{
    [Fact]
    public async Task UserAdd_Search_Update_AndDuplicate()
    {
        await using var app = await TestApp.CreateAsync();
        Assert.Equal(DictionaryWriteResult.Saved, await app.Dictionary.AddUserAsync(Language.En, "Hello", "سلام"));
        Assert.Equal(DictionaryWriteResult.Duplicate, await app.Dictionary.AddUserAsync(Language.En, "hello", "درود"));

        var found = await app.Dictionary.SearchAsync("hel", 20);
        Assert.Single(found);
        Assert.Equal(DictionaryWriteResult.Saved, await app.Dictionary.UpdateUserAsync(found[0].Id, Language.En, "hello", "درود"));
        Assert.Equal("درود", (await app.Dictionary.FindAsync(Language.En, "hello"))!.Translation);
    }

    [Fact]
    public async Task SeedImport_InsertsBothDirections()
    {
        await using var app = await TestApp.CreateAsync();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("book,کتاب\nwater,آب\n"));
        var inserted = await SeedImporter.ImportAsync(app.Database, stream);

        Assert.Equal(4, inserted);
        Assert.Equal("کتاب", (await app.Dictionary.FindAsync(Language.En, "book"))!.Translation);
        Assert.Equal("book", (await app.Dictionary.FindAsync(Language.Fa, "کتاب"))!.Translation);
        Assert.Equal("1", await app.Database.GetMetaAsync("SchemaVersion"));
    }

    [Fact]
    public void EmbeddedSeed_HasAFewThousandPairs()
    {
        using var stream = SeedImporter.OpenSeed();
        var pairs = CsvPairReader.Read(stream);
        Assert.True(pairs.Count >= 2000, $"Seed has {pairs.Count} pairs.");
        Assert.DoesNotContain(pairs, pair => pair.English.EndsWith(" verb", StringComparison.Ordinal) || pair.English.EndsWith(" noun", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmbeddedSeed_ResolvesCommonWordsOffline()
    {
        await using var app = await TestApp.CreateAsync();
        await SeedImporter.EnsureSeededAsync(app.Database);

        Assert.Equal("کتاب", (await app.Dictionary.FindAsync(Language.En, "book"))!.Translation);
        Assert.Equal("آب", (await app.Dictionary.FindAsync(Language.En, "water"))!.Translation);
        Assert.Equal("سلام", (await app.Dictionary.FindAsync(Language.En, "hello"))!.Translation);
        Assert.Equal("book", (await app.Dictionary.FindAsync(Language.Fa, "کتاب"))!.Translation);

        await SeedImporter.EnsureSeededAsync(app.Database);
        var stats = await app.Dictionary.GetStatsAsync();
        Assert.True(stats.TotalEntries >= 2000);
    }
}
