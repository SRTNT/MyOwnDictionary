using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MyOwnDictionary.App.Services;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.ViewModels;

public partial class TranslateViewModel : ViewModelBase
{
    private readonly TranslationService _translator;
    private readonly DictionaryRepository _dictionary;
    private readonly HistoryRepository _history;
    private readonly ClipboardService _clipboard;
    private readonly SettingsStore _settings;
    private readonly IMessenger _messenger;
    private CancellationTokenSource? _run;
    private int _requestId;

    public TranslateViewModel(
        TranslationService translation,
        DictionaryRepository dictionary,
        HistoryRepository history,
        ClipboardService clipboard,
        SettingsStore settings,
        IMessenger messenger)
    {
        _translator = translation;
        _dictionary = dictionary;
        _history = history;
        _clipboard = clipboard;
        _settings = settings;
        _messenger = messenger;
        messenger.Register<TranslateViewModel, ReTranslateMessage>(this, (recipient, message) =>
        {
            recipient.SourceOverride = message.SourceLanguage;
            recipient.SourceText = message.Text;
        });
        messenger.Register<TranslateViewModel, PreferencesChanged>(this, (recipient, _) => recipient.RefreshOfflineBanner());
    }

    public ObservableCollection<RecentRow> Recent { get; } = [];

    [ObservableProperty] private string _sourceText = string.Empty;
    [ObservableProperty] private string _translation = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _showOfflineChip;
    [ObservableProperty] private bool _showGoogleChip;
    [ObservableProperty] private bool _showSavedChip;
    [ObservableProperty] private string _originText = string.Empty;
    [ObservableProperty] private bool _isOfflineOnly;
    [ObservableProperty] private bool _isRecentEmpty = true;
    [ObservableProperty] private string _totalWords = "0";
    [ObservableProperty] private string _googleCached = "0";
    [ObservableProperty] private string _englishHistory = "0";
    [ObservableProperty] private string _persianHistory = "0";
    [ObservableProperty] private Language? _sourceOverride;

    public override string Title => "Dashboard";
    public override string Subtitle => "Translate between Persian and English";
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool ShowChip => ShowOfflineChip || ShowGoogleChip || ShowSavedChip;

    public string DirectionLabel => CurrentSource == Language.Fa ? "Persian → English" : "English → Persian";
    public FlowDirection SourceFlow => CurrentSource == Language.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public FlowDirection TargetFlow => CurrentSource == Language.Fa ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
    public FontFamily SourceTypeface => CurrentSource == Language.Fa ? AppFonts.Persian : AppFonts.Body;
    public FontFamily TargetTypeface => CurrentSource == Language.Fa ? AppFonts.Body : AppFonts.Persian;

    private Language CurrentSource =>
        SourceOverride ?? (string.IsNullOrWhiteSpace(SourceText) ? Language.En : LanguageDetector.Detect(SourceText));

    public override Task OnShownAsync()
    {
        RefreshOfflineBanner();
        return RefreshSummariesAsync();
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnShowOfflineChipChanged(bool value) => OnPropertyChanged(nameof(ShowChip));

    partial void OnShowGoogleChipChanged(bool value) => OnPropertyChanged(nameof(ShowChip));

    partial void OnShowSavedChipChanged(bool value) => OnPropertyChanged(nameof(ShowChip));

    partial void OnSourceOverrideChanged(Language? value) => NotifyLanguage();

    partial void OnSourceTextChanged(string value)
    {
        if (SourceOverride is { } forced
            && !string.IsNullOrWhiteSpace(value)
            && LanguageDetector.Detect(value) != forced)
        {
            SourceOverride = null;
        }

        NotifyLanguage();
        Schedule();
    }

    [RelayCommand]
    private async Task TranslateNowAsync()
    {
        _run?.Cancel();
        _run = new CancellationTokenSource();
        await TranslateCoreAsync(_run.Token);
    }

    [RelayCommand]
    private void Swap()
    {
        var nextSource = LanguageDetector.Opposite(CurrentSource);
        var nextText = string.IsNullOrWhiteSpace(Translation) ? SourceText : Translation;
        SourceOverride = nextSource;
        SourceText = nextText;
    }

    [RelayCommand]
    private async Task CopyTranslationAsync()
    {
        await _clipboard.SetTextAsync(Translation);
        Status = string.IsNullOrEmpty(Translation) ? string.Empty : "Copied";
    }

    [RelayCommand]
    private async Task CopySourceAsync()
    {
        await _clipboard.SetTextAsync(SourceText);
        Status = string.IsNullOrEmpty(SourceText) ? string.Empty : "Copied";
    }

    private void Schedule()
    {
        _run?.Cancel();
        _run = new CancellationTokenSource();
        var token = _run.Token;
        _ = DebounceAsync(token);
    }

    private async Task DebounceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(550, token);
            await TranslateCoreAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TranslateCoreAsync(CancellationToken token)
    {
        var text = SourceText;
        if (string.IsNullOrWhiteSpace(text))
        {
            Translation = string.Empty;
            Error = null;
            Status = string.Empty;
            ShowOfflineChip = false;
            ShowGoogleChip = false;
            ShowSavedChip = false;
            OriginText = string.Empty;
            IsBusy = false;
            return;
        }

        var requestId = Interlocked.Increment(ref _requestId);
        IsBusy = true;
        Error = null;
        try
        {
            var result = await _translator.TranslateAsync(text, SourceOverride, recordHistory: true, token);
            if (requestId != _requestId || token.IsCancellationRequested)
                return;

            Translation = result.Translation;
            Error = result.Error;
            ShowOfflineChip = result.Origin == EntryOrigin.Seed;
            ShowGoogleChip = result.Origin == EntryOrigin.Google;
            ShowSavedChip = result.Origin == EntryOrigin.User;
            OriginText = result.Origin switch
            {
                EntryOrigin.Seed => "Offline",
                EntryOrigin.Google => "Google",
                EntryOrigin.User => "Saved",
                _ => string.Empty
            };
            Status = result.Success ? string.Empty : string.Empty;
            if (result.Success)
                await RefreshSummariesAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (requestId == _requestId)
                IsBusy = false;
        }
    }

    private async Task RefreshSummariesAsync()
    {
        var counts = await _dictionary.GetStatsAsync();
        var recent = await _history.ListAsync(null, null, 6);
        TotalWords = counts.TotalEntries.ToString("N0", CultureInfo.CurrentCulture);
        GoogleCached = counts.GoogleCached.ToString("N0", CultureInfo.CurrentCulture);
        EnglishHistory = counts.EnglishHistory.ToString("N0", CultureInfo.CurrentCulture);
        PersianHistory = counts.PersianHistory.ToString("N0", CultureInfo.CurrentCulture);
        Recent.Clear();
        foreach (var item in recent)
            Recent.Add(new RecentRow(item, OpenRecent));
        IsRecentEmpty = Recent.Count == 0;
    }

    private void OpenRecent(RecentRow row)
    {
        SourceOverride = row.SourceLanguage;
        SourceText = row.Input;
    }

    private void RefreshOfflineBanner() => IsOfflineOnly = !_settings.Current.OnlineFallbackEnabled;

    private void NotifyLanguage()
    {
        OnPropertyChanged(nameof(DirectionLabel));
        OnPropertyChanged(nameof(SourceFlow));
        OnPropertyChanged(nameof(TargetFlow));
        OnPropertyChanged(nameof(SourceTypeface));
        OnPropertyChanged(nameof(TargetTypeface));
    }
}

public sealed class RecentRow
{
    public RecentRow(HistoryItem item, Action<RecentRow> open)
    {
        Input = item.InputText;
        Translation = item.Translation;
        SourceLanguage = item.SourceLanguage;
        Direction = item.SourceLanguage == Core.Language.Fa ? "FA → EN" : "EN → FA";
        SourceFlow = item.SourceLanguage == Core.Language.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        TargetFlow = item.SourceLanguage == Core.Language.Fa ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        OpenCommand = new RelayCommand(() => open(this));
    }

    public string Input { get; }
    public string Translation { get; }
    public Language SourceLanguage { get; }
    public string Direction { get; }
    public FlowDirection SourceFlow { get; }
    public FlowDirection TargetFlow { get; }
    public IRelayCommand OpenCommand { get; }
}
