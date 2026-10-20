using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MyOwnDictionary.App.Services;

public static class AppRestart
{
    public static void Now()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            var start = new ProcessStartInfo(path) { UseShellExecute = true };
            if (App.IsPhoneLayout)
                start.ArgumentList.Add("--phone");

            Process.Start(start);
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
