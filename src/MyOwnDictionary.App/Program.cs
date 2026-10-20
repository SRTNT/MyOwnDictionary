using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using MyOwnDictionary.App.Services;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.IsPhoneLayout = args.Any(arg => arg is "--phone" or "-phone" or "/phone");
        try
        {
            App.Services = AppHost.Build();
            var database = App.Services.GetRequiredService<AppDatabase>();
            database.InitializeAsync().GetAwaiter().GetResult();
            SeedImporter.EnsureSeededAsync(database).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            App.StartupError = ex.ToString();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
