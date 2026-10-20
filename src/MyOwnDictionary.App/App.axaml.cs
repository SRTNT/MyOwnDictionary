using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using MyOwnDictionary.App.Services;
using MyOwnDictionary.App.ViewModels;
using MyOwnDictionary.App.Views;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App;

public partial class App : Application
{
    public static IServiceProvider? Services { get; set; }
    public static string? StartupError { get; set; }
    public static bool IsPhoneLayout { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        EnsureServices();
        var main = StartupError is null ? Services?.GetRequiredService<MainViewModel>() : null;
        main?.ApplyInitialTheme();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = main is null
                ? new Window { Title = "MyOwnDictionary", Width = 720, Height = 420, Content = CreateErrorView() }
                : new MainWindow { DataContext = main };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = main is null ? CreateErrorView() : new MainView { DataContext = main };
        }

        if (main is not null)
            _ = main.ShowInitialPageAsync();

        base.OnFrameworkInitializationCompleted();
    }

    private static void EnsureServices()
    {
        if (Services is not null || StartupError is not null)
            return;

        try
        {
            Services = AppHost.Build();
            var database = Services.GetRequiredService<AppDatabase>();
            database.InitializeAsync().GetAwaiter().GetResult();
            SeedImporter.EnsureSeededAsync(database).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StartupError = ex.ToString();
        }
    }

    private static Control CreateErrorView() => new ScrollViewer
    {
        Content = new TextBlock
        {
            Text = StartupError ?? "The dictionary could not start.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24)
        }
    };
}
