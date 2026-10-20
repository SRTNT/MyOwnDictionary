using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly IMessenger _messenger;
    private bool _suppress;

    public MainViewModel(
        TranslateViewModel translate,
        DictionaryViewModel dictionary,
        HistoryViewModel historyEnglish,
        HistoryViewModel historyPersian,
        SettingsViewModel settingsPage,
        SettingsStore settings,
        IMessenger messenger)
    {
        TranslatePage = translate;
        DictionaryPage = dictionary;
        HistoryEnglishPage = historyEnglish;
        HistoryPersianPage = historyPersian;
        SettingsPage = settingsPage;
        _settings = settings;
        _messenger = messenger;
        _suppress = true;
        IsDarkTheme = settings.Current.IsDarkTheme;
        _suppress = false;
        CurrentPage = translate;
        messenger.Register<MainViewModel, ReTranslateMessage>(this, (recipient, _) => recipient.ShowTranslate());
        messenger.Register<MainViewModel, PreferencesChanged>(this, (recipient, message) => recipient.LoadTheme(message.Settings));
    }

    public TranslateViewModel TranslatePage { get; }
    public DictionaryViewModel DictionaryPage { get; }
    public HistoryViewModel HistoryEnglishPage { get; }
    public HistoryViewModel HistoryPersianPage { get; }
    public SettingsViewModel SettingsPage { get; }

    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isDarkTheme;

    public string PageTitle => CurrentPage.Title;
    public string PageSubtitle => CurrentPage.Subtitle;
    public string TodayLabel => DateTime.Now.ToString("dddd, MMM d");
    public string ThemeIcon => IsDarkTheme ? "\uE706" : "\uE708";
    public bool IsTranslateSelected => ReferenceEquals(CurrentPage, TranslatePage);
    public bool IsDictionarySelected => ReferenceEquals(CurrentPage, DictionaryPage);
    public bool IsHistoryEnglishSelected => ReferenceEquals(CurrentPage, HistoryEnglishPage);
    public bool IsHistoryPersianSelected => ReferenceEquals(CurrentPage, HistoryPersianPage);
    public bool IsSettingsSelected => ReferenceEquals(CurrentPage, SettingsPage);

    public void ApplyInitialTheme() => ApplyVariant(IsDarkTheme);

    public Task ShowInitialPageAsync() => TranslatePage.OnShownAsync();

    partial void OnCurrentPageChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(IsTranslateSelected));
        OnPropertyChanged(nameof(IsDictionarySelected));
        OnPropertyChanged(nameof(IsHistoryEnglishSelected));
        OnPropertyChanged(nameof(IsHistoryPersianSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        OnPropertyChanged(nameof(ThemeIcon));
        if (_suppress)
            return;

        var next = _settings.Current with { IsDarkTheme = value };
        _settings.Save(next);
        ApplyVariant(value);
        _messenger.Send(new PreferencesChanged(next));
    }

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    [RelayCommand]
    private Task ShowTranslate() => NavigateAsync(TranslatePage);

    [RelayCommand]
    private Task ShowDictionary() => NavigateAsync(DictionaryPage);

    [RelayCommand]
    private Task ShowHistoryEnglish() => NavigateAsync(HistoryEnglishPage);

    [RelayCommand]
    private Task ShowHistoryPersian() => NavigateAsync(HistoryPersianPage);

    [RelayCommand]
    private Task ShowSettings() => NavigateAsync(SettingsPage);

    private async Task NavigateAsync(ViewModelBase page)
    {
        CurrentPage = page;
        await page.OnShownAsync();
    }

    private void LoadTheme(AppSettings settings)
    {
        _suppress = true;
        IsDarkTheme = settings.IsDarkTheme;
        _suppress = false;
        ApplyVariant(settings.IsDarkTheme);
    }

    private static void ApplyVariant(bool isDark)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
