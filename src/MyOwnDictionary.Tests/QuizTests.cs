using MyOwnDictionary.Core;

namespace MyOwnDictionary.Tests;

public class QuizTests
{
    [Fact]
    public async Task TenCorrectAnswers_MasterTheWord_AndAWrongAnswerDoesNotResetThem()
    {
        await using var app = await TestApp.CreateAsync();
        await app.History.AddAsync(Language.En, "book", "کتاب", EntryOrigin.User);
        var quiz = new QuizRepository(app.Database);

        for (var i = 0; i < 3; i++)
            await quiz.RecordAsync(Language.En, "book", correct: true);

        var afterWrong = await quiz.RecordAsync(Language.En, "book", correct: false);
        Assert.Equal(3, afterWrong.CorrectCount);
        Assert.Equal(1, afterWrong.WrongCount);
        Assert.False(afterWrong.Mastered);

        var tenth = new QuizScore(0, 0, false, false);
        for (var i = 0; i < 7; i++)
            tenth = await quiz.RecordAsync(Language.En, "book", correct: true);

        Assert.Equal(10, tenth.CorrectCount);
        Assert.True(tenth.Mastered);
        Assert.True(tenth.JustMastered);

        var reviewWrong = await quiz.RecordAsync(Language.En, "book", correct: false);
        Assert.Equal(10, reviewWrong.CorrectCount);
        Assert.True(reviewWrong.Mastered);
        Assert.False(reviewWrong.JustMastered);

        var learning = await quiz.ListAsync(Language.En, QuizPool.Learning);
        var mastered = await quiz.ListAsync(Language.En, QuizPool.Mastered);
        Assert.DoesNotContain(learning, word => word.NormalizedText == "book");
        Assert.Contains(mastered, word => word.NormalizedText == "book");
    }

    [Fact]
    public async Task Pool_KeepsLatestShortWord_AndDropsLongOrRepeatedText()
    {
        await using var app = await TestApp.CreateAsync();
        await app.History.AddAsync(Language.En, "book", "کتاب", EntryOrigin.User);
        await app.History.AddAsync(Language.En, "Book", "کتابی", EntryOrigin.User);
        await app.History.AddAsync(Language.En, "one two three four five six", "بلند", EntryOrigin.User);
        await app.History.AddAsync(Language.Fa, "سلام", "hello", EntryOrigin.User);

        var words = await new QuizRepository(app.Database).ListAsync(Language.En, QuizPool.Learning);

        var only = Assert.Single(words);
        Assert.Equal("book", only.NormalizedText);
        Assert.Equal("کتابی", only.Translation);
    }

    [Fact]
    public async Task Choices_AreFourUniqueAnswers_AndIncludeTheCorrectOne()
    {
        await using var app = await TestApp.CreateAsync();
        await app.History.AddAsync(Language.En, "book", "کتاب", EntryOrigin.User);
        await app.History.AddAsync(Language.En, "water", "آب", EntryOrigin.User);
        await app.Dictionary.AddUserAsync(Language.En, "cat", "گربه");
        await app.Dictionary.AddUserAsync(Language.En, "dog", "سگ");
        await app.Dictionary.AddUserAsync(Language.En, "bird", "پرنده");

        var choices = await new QuizRepository(app.Database).BuildChoicesAsync(Language.En, "کتاب");

        Assert.Equal(4, choices.Count);
        Assert.Contains("کتاب", choices);
        Assert.Equal(4, choices.Select(choice => TextNormalizer.Normalize(choice, Language.Fa)).Distinct().Count());
    }

    [Fact]
    public void TypedAnswer_AcceptsArabicLetterVariants_AndAnyMeaning()
    {
        Assert.True(QuizRepository.AnswersMatch("کتاب", "كتاب", Language.Fa));
        Assert.True(QuizRepository.AnswersMatch("چای", "چاي", Language.Fa));
        Assert.True(QuizRepository.AnswersMatch("book, volume", "Volume", Language.En));
        Assert.True(QuizRepository.AnswersMatch("کتاب؛ دفتر", "دفتر", Language.Fa));
        Assert.False(QuizRepository.AnswersMatch("کتاب", "دفتر", Language.Fa));
    }

    [Fact]
    public async Task Backup_CopiesHistory_AndExistingDestinationIsKept()
    {
        await using var app = await TestApp.CreateAsync();
        await app.History.AddAsync(Language.En, "book", "کتاب", EntryOrigin.User);
        app.Settings.Save(app.Settings.Current with { DataFolder = app.DirectoryPath });

        var cloud = Path.Combine(app.DirectoryPath, "cloud");
        var copied = await DataFolderService.SwitchAsync(app.Database, app.Settings, cloud);

        Assert.True(copied.Restart);
        Assert.Null(copied.Error);
        await using (var cloudDb = new AppDatabase(Path.Combine(cloud, "dictionary.db")))
        {
            await cloudDb.InitializeAsync();
            var history = await new HistoryRepository(cloudDb).ListAsync(Language.En, null, 10);
            Assert.Equal("کتاب", Assert.Single(history).Translation);
        }

        var existing = Path.Combine(app.DirectoryPath, "existing");
        Directory.CreateDirectory(existing);
        await using (var existingDb = new AppDatabase(Path.Combine(existing, "dictionary.db")))
        {
            await existingDb.InitializeAsync();
            await new HistoryRepository(existingDb).AddAsync(Language.En, "water", "آب", EntryOrigin.User);
        }

        var kept = await DataFolderService.SwitchAsync(app.Database, app.Settings, existing);

        Assert.True(kept.Restart);
        await using var check = new AppDatabase(Path.Combine(existing, "dictionary.db"));
        await check.InitializeAsync();
        var rows = await new HistoryRepository(check).ListAsync(Language.En, null, 10);
        Assert.Equal("آب", Assert.Single(rows).Translation);
    }

    [Fact]
    public async Task CustomFolder_UsesDeleteJournal()
    {
        await using var app = await TestApp.CreateAsync();
        await using var synced = new AppDatabase(Path.Combine(app.DirectoryPath, "sync", "dictionary.db"), useDeleteJournal: true);
        var mode = await synced.ExecuteAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            return (string)(await command.ExecuteScalarAsync(token))!;
        });

        Assert.Equal("delete", mode, ignoreCase: true);
    }
}
