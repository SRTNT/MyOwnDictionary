using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace MyOwnDictionary.App.Services;

public sealed class ClipboardService
{
    public async Task SetTextAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var top = Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            ISingleViewApplicationLifetime { MainView: { } view } => TopLevel.GetTopLevel(view),
            _ => null
        };

        if (top?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }
}
