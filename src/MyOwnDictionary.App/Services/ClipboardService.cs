using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MyOwnDictionary.App.Services;

public sealed class ClipboardService
{
    public async Task SetTextAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            return;

        var clipboard = lifetime.MainWindow?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }
}
