using MyOwnDictionary.Core;

namespace MyOwnDictionary.Tests;

public sealed class FakeTranslationProvider : ITranslationProvider
{
    public int Calls { get; private set; }
    public List<string> Inputs { get; } = [];
    public Func<string, Language, Language, string>? Handler { get; set; }
    public Exception? Failure { get; set; }

    public Task<string> TranslateAsync(string text, Language source, Language target, CancellationToken cancellationToken)
    {
        Calls++;
        Inputs.Add(text);
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
            throw Failure;

        return Task.FromResult(Handler?.Invoke(text, source, target) ?? $"{LanguageCodes.ToCode(target)}:{text}");
    }
}

public sealed class TestApp : IAsyncDisposable
{
    public string DirectoryPath { get; }
    public AppDatabase Database { get; }
    public SettingsStore Settings { get; }
    public DictionaryRepository Dictionary { get; }
    public HistoryRepository History { get; }
    public FakeTranslationProvider Provider { get; }
    public TranslationService Translator { get; }

    private TestApp(string directoryPath)
    {
        DirectoryPath = directoryPath;
        Database = new AppDatabase(Path.Combine(directoryPath, "dictionary.db"));
        Settings = new SettingsStore(Path.Combine(directoryPath, "settings.json"));
        Dictionary = new DictionaryRepository(Database);
        History = new HistoryRepository(Database);
        Provider = new FakeTranslationProvider();
        Translator = new TranslationService(Dictionary, History, Provider, Settings);
    }

    public static async Task<TestApp> CreateAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mydict-tests", Guid.NewGuid().ToString("N"));
        var app = new TestApp(directory);
        await app.Database.InitializeAsync();
        return app;
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        try
        {
            Directory.Delete(DirectoryPath, true);
        }
        catch (IOException)
        {
        }
    }
}
