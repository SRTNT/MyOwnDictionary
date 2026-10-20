using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MyOwnDictionary.App.Services;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly Language _language;
    private readonly HistoryRepository _history;
    private readonly ClipboardService _clipboard;
    private readonly IMessenger _messenger;
    private CancellationTokenSource? _search;
    private bool _confirmClear;

    public HistoryViewModel(
        Language language,
        string title,
        string subtitle,
        HistoryRepository history,
        ClipboardService clipboard,
        IMessenger messenger,
        QuizViewModel quiz)
    {
        _language = language;
        Title = title;
        Subtitle = subtitle;
        _history = history;
        _clipboard = clipboard;
        _messenger = messenger;
        Quiz = quiz;
    }

    public QuizViewModel Quiz { get; }

    public ObservableCollection<HistoryRow> Rows { get; } = [];

    public override string Title { get; }
    public override string Subtitle { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _clearLabel = "Clear all";
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _showQuiz;

    public bool IsHistoryTab => !ShowQuiz;

    public override async Task OnShownAsync()
    {
        await ReloadAsync(CancellationToken.None);
        await Quiz.RefreshAsync();
    }

    partial void OnShowQuizChanged(bool value) => OnPropertyChanged(nameof(IsHistoryTab));

    [RelayCommand]
    private void ShowHistoryTab() => ShowQuiz = false;

    [RelayCommand]
    private async Task ShowQuizTabAsync()
    {
        ShowQuiz = true;
        await Quiz.RefreshAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _search?.Cancel();
        _search = new CancellationTokenSource();
        var token = _search.Token;
        _ = ReloadDebouncedAsync(token);
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (!_confirmClear)
        {
            _confirmClear = true;
            ClearLabel = "Click again to confirm";
            try
            {
                await Task.Delay(3000);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_confirmClear)
            {
                _confirmClear = false;
                ClearLabel = "Clear all";
            }

            return;
        }

        _confirmClear = false;
        ClearLabel = "Clear all";
        await _history.ClearAsync(_language);
        Status = "History cleared.";
        await ReloadAsync(CancellationToken.None);
    }

    public async Task DeleteAsync(HistoryRow row)
    {
        await _history.DeleteAsync(row.Id);
        await ReloadAsync(CancellationToken.None);
    }

    public async Task CopyAsync(HistoryRow row)
    {
        await _clipboard.SetTextAsync(row.Translation);
        Status = "Copied";
    }

    public void Open(HistoryRow row)
    {
        _messenger.Send(new ReTranslateMessage(row.Input, _language));
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
        var items = await _history.ListAsync(_language, SearchText, 300, token);
        if (token.IsCancellationRequested)
            return;

        Rows.Clear();
        foreach (var item in items)
            Rows.Add(new HistoryRow(item, Open, CopyAsync, DeleteAsync));
        IsEmpty = Rows.Count == 0;
    }
}

public sealed class HistoryRow
{
    public HistoryRow(
        HistoryItem item,
        Action<HistoryRow> open,
        Func<HistoryRow, Task> copy,
        Func<HistoryRow, Task> delete)
    {
        Id = item.Id;
        Input = item.InputText;
        Translation = item.Translation;
        When = item.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        OriginLabel = item.Origin switch
        {
            EntryOrigin.Google => "Google",
            EntryOrigin.User => "Saved",
            _ => "Offline"
        };
        SourceFlow = item.SourceLanguage == Language.Fa ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
        TargetFlow = item.SourceLanguage == Language.Fa ? Avalonia.Media.FlowDirection.LeftToRight : Avalonia.Media.FlowDirection.RightToLeft;
        OpenCommand = new RelayCommand(() => open(this));
        CopyCommand = new AsyncRelayCommand(() => copy(this));
        DeleteCommand = new AsyncRelayCommand(() => delete(this));
    }

    public long Id { get; }
    public string Input { get; }
    public string Translation { get; }
    public string When { get; }
    public string OriginLabel { get; }
    public Avalonia.Media.FlowDirection SourceFlow { get; }
    public Avalonia.Media.FlowDirection TargetFlow { get; }
    public IRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
}
