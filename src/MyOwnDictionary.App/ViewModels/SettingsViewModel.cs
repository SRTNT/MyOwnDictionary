using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MyOwnDictionary.App.Services;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _settings;
    private readonly AppDatabase _database;
    private readonly IMessenger _messenger;
    private bool _suppress;
    private bool _moving;

    public SettingsViewModel(SettingsStore settings, AppDatabase database, IMessenger messenger)
    {
        _settings = settings;
        _database = database;
        _messenger = messenger;
        var current = settings.Current;
        _suppress = true;
        IsDarkTheme = current.IsDarkTheme;
        OnlineFallbackEnabled = current.OnlineFallbackEnabled;
        TimeoutSeconds = current.TimeoutSeconds;
        _suppress = false;
        OneDriveFolder = AppPaths.OneDriveDataFolder();
        GoogleDriveFolder = AppPaths.GoogleDriveDataFolder();
        messenger.Register<SettingsViewModel, PreferencesChanged>(this, (recipient, message) => recipient.Load(message.Settings));
    }

    public override string Title => "Settings";
    public override string Subtitle => "Theme, online fallback, and where data is stored";
    public string StoragePath => Path.GetDirectoryName(AppPaths.DatabaseFile(_settings.Current)) ?? AppPaths.Root;
    public string? OneDriveFolder { get; }
    public string? GoogleDriveFolder { get; }
    public bool HasOneDrive => OneDriveFolder is not null;
    public bool HasGoogleDrive => GoogleDriveFolder is not null;
    public string OneDriveStatus => HasOneDrive ? OneDriveFolder! : "OneDrive was not found on this PC.";
    public string GoogleDriveStatus => HasGoogleDrive ? GoogleDriveFolder! : "Google Drive was not found on this PC.";
    public bool IsCustomFolder => !AppPaths.IsDefaultFolder(_settings.Current.DataFolder);

    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private bool _onlineFallbackEnabled;
    [ObservableProperty] private decimal? _timeoutSeconds = AppSettings.DefaultTimeoutSeconds;
    [ObservableProperty] private string _folderMessage = "";

    partial void OnIsDarkThemeChanged(bool value) => Save();

    partial void OnOnlineFallbackEnabledChanged(bool value) => Save();

    partial void OnTimeoutSecondsChanged(decimal? value)
    {
        if (_suppress || value is null)
            return;

        var clamped = Math.Clamp((int)value.Value, AppSettings.MinimumTimeoutSeconds, AppSettings.MaximumTimeoutSeconds);
        if (clamped != (int)value.Value)
        {
            TimeoutSeconds = clamped;
            return;
        }

        Save();
    }

    [RelayCommand(CanExecute = nameof(CanUseOneDrive))]
    private Task UseOneDriveAsync() => MoveAsync(OneDriveFolder);

    [RelayCommand(CanExecute = nameof(CanUseGoogleDrive))]
    private Task UseGoogleDriveAsync() => MoveAsync(GoogleDriveFolder);

    [RelayCommand(CanExecute = nameof(CanUseLocal))]
    private Task UseLocalAsync() => MoveAsync(null);

    private bool CanUseOneDrive() => HasOneDrive;

    private bool CanUseGoogleDrive() => HasGoogleDrive;

    private bool CanUseLocal() => IsCustomFolder;

    public Task UseFolderAsync(string folder) => MoveAsync(folder);

    private async Task MoveAsync(string? folder)
    {
        if (_moving)
            return;

        _moving = true;
        FolderMessage = "Switching the data folder...";
        var result = await DataFolderService.SwitchAsync(_database, _settings, folder);
        if (result.Error is not null)
        {
            FolderMessage = result.Error;
            _moving = false;
            return;
        }

        if (!result.Restart)
        {
            FolderMessage = "This folder is already in use.";
            _moving = false;
            return;
        }

        FolderMessage = "Reopening with the new folder...";
        AppRestart.Now();
    }

    private void Save()
    {
        if (_suppress)
            return;

        var next = (_settings.Current with
        {
            IsDarkTheme = IsDarkTheme,
            OnlineFallbackEnabled = OnlineFallbackEnabled,
            TimeoutSeconds = (int)(TimeoutSeconds ?? AppSettings.DefaultTimeoutSeconds)
        }).Normalized();
        _settings.Save(next);
        _messenger.Send(new PreferencesChanged(next));
    }

    private void Load(AppSettings settings)
    {
        _suppress = true;
        IsDarkTheme = settings.IsDarkTheme;
        OnlineFallbackEnabled = settings.OnlineFallbackEnabled;
        TimeoutSeconds = settings.TimeoutSeconds;
        _suppress = false;
        OnPropertyChanged(nameof(StoragePath));
        OnPropertyChanged(nameof(IsCustomFolder));
        UseLocalCommand.NotifyCanExecuteChanged();
    }
}
