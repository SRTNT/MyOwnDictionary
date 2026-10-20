using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyOwnDictionary.App.Services;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.ViewModels;

public partial class DictionaryViewModel : ViewModelBase
{
    private readonly DictionaryRepository _dictionary;
    private CancellationTokenSource? _search;

    public DictionaryViewModel(DictionaryRepository dictionary)
    {
        _dictionary = dictionary;
        SelectedLanguage = Languages[0];
    }

    public LanguageOption[] Languages { get; } =
    [
        new(Language.En, "English"),
        new(Language.Fa, "Persian")
    ];

    public ObservableCollection<DictionaryRow> Rows { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _wordText = string.Empty;
    [ObservableProperty] private string _translationText = string.Empty;
    [ObservableProperty] private string _formMessage = string.Empty;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private long? _editingId;
    [ObservableProperty] private LanguageOption _selectedLanguage;

    public override string Title => "Dictionary";
    public override string Subtitle => "Offline words stored on this device";
    public bool IsEditing => EditingId is not null;
    public bool HasFormMessage => !string.IsNullOrWhiteSpace(FormMessage);
    public FlowDirection WordFlow => SelectedLanguage.Language == Language.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public FlowDirection TranslationFlow => SelectedLanguage.Language == Language.Fa ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
    public FontFamily WordTypeface => SelectedLanguage.Language == Language.Fa ? AppFonts.Persian : AppFonts.Body;
    public FontFamily TranslationTypeface => SelectedLanguage.Language == Language.Fa ? AppFonts.Body : AppFonts.Persian;

    public override Task OnShownAsync() => ReloadAsync(CancellationToken.None);

    partial void OnEditingIdChanged(long? value) => OnPropertyChanged(nameof(IsEditing));

    partial void OnFormMessageChanged(string value) => OnPropertyChanged(nameof(HasFormMessage));

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        OnPropertyChanged(nameof(WordFlow));
        OnPropertyChanged(nameof(TranslationFlow));
        OnPropertyChanged(nameof(WordTypeface));
        OnPropertyChanged(nameof(TranslationTypeface));
    }

    partial void OnSearchTextChanged(string value)
    {
        _search?.Cancel();
        _search = new CancellationTokenSource();
        var token = _search.Token;
        _ = ReloadDebouncedAsync(token);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var result = await _dictionary.AddUserAsync(SelectedLanguage.Language, WordText, TranslationText);
        FormMessage = result switch
        {
            DictionaryWriteResult.Saved => "Saved to the offline dictionary.",
            DictionaryWriteResult.Duplicate => "This word is already in the dictionary.",
            _ => "Enter both the word and the translation."
        };
        if (result == DictionaryWriteResult.Saved)
        {
            WordText = string.Empty;
            TranslationText = string.Empty;
            EditingId = null;
            await ReloadAsync(CancellationToken.None);
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (EditingId is not long id)
            return;

        var result = await _dictionary.UpdateUserAsync(id, SelectedLanguage.Language, WordText, TranslationText);
        FormMessage = result switch
        {
            DictionaryWriteResult.Saved => "Word updated.",
            DictionaryWriteResult.Duplicate => "Another entry already uses that word.",
            DictionaryWriteResult.NotFound => "That entry is no longer in the dictionary.",
            _ => "Enter both the word and the translation."
        };
        if (result == DictionaryWriteResult.Saved)
            await ReloadAsync(CancellationToken.None);
    }

    public void BeginEdit(DictionaryRow row)
    {
        EditingId = row.Id;
        WordText = row.SourceText;
        TranslationText = row.Translation;
        SelectedLanguage = Languages.First(option => option.Language == row.SourceLanguage);
        FormMessage = "Editing the selected word.";
    }

    public async Task DeleteAsync(DictionaryRow row)
    {
        await _dictionary.DeleteAsync(row.Id);
        if (EditingId == row.Id)
        {
            EditingId = null;
            WordText = string.Empty;
            TranslationText = string.Empty;
        }

        FormMessage = "Word deleted.";
        await ReloadAsync(CancellationToken.None);
    }

    private async Task ReloadDebouncedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(200, token);
            await ReloadAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReloadAsync(CancellationToken token)
    {
        var entries = await _dictionary.SearchAsync(SearchText, 200, token);
        if (token.IsCancellationRequested)
            return;

        Rows.Clear();
        foreach (var entry in entries)
            Rows.Add(new DictionaryRow(entry, BeginEdit, DeleteAsync));
        IsEmpty = Rows.Count == 0;
    }
}

public sealed class LanguageOption
{
    public LanguageOption(Language language, string label)
    {
        Language = language;
        Label = label;
    }

    public Language Language { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

public sealed class DictionaryRow
{
    public DictionaryRow(DictionaryEntry entry, Action<DictionaryRow> edit, Func<DictionaryRow, Task> delete)
    {
        Id = entry.Id;
        SourceText = entry.SourceText;
        Translation = entry.Translation;
        SourceLanguage = entry.SourceLanguage;
        LanguageLabel = entry.SourceLanguage == Language.Fa ? "FA" : "EN";
        OriginLabel = entry.Origin switch
        {
            EntryOrigin.Google => "Google",
            EntryOrigin.User => "Saved",
            _ => "Offline"
        };
        SourceFlow = entry.SourceLanguage == Language.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        TranslationFlow = entry.SourceLanguage == Language.Fa ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        EditCommand = new RelayCommand(() => edit(this));
        DeleteCommand = new AsyncRelayCommand(() => delete(this));
    }

    public long Id { get; }
    public string SourceText { get; }
    public string Translation { get; }
    public Language SourceLanguage { get; }
    public string LanguageLabel { get; }
    public string OriginLabel { get; }
    public FlowDirection SourceFlow { get; }
    public FlowDirection TranslationFlow { get; }
    public IRelayCommand EditCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
}
