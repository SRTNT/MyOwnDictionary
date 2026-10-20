using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using MyOwnDictionary.App.ViewModels;
using MyOwnDictionary.App.Views;

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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (StartupError is not null || Services is null)
            {
                desktop.MainWindow = new Window
                {
                    Title = "MyOwnDictionary",
                    Width = 720,
                    Height = 420,
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = StartupError ?? "The dictionary could not start.",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(24)
                        }
                    }
                };
            }
            else
            {
                var main = Services.GetRequiredService<MainViewModel>();
                main.ApplyInitialTheme();
                desktop.MainWindow = new MainWindow { DataContext = main };
                _ = main.ShowInitialPageAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
