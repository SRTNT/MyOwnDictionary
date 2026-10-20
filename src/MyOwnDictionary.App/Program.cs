using Avalonia;

namespace MyOwnDictionary.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.IsPhoneLayout = args.Any(arg => arg is "--phone" or "-phone" or "/phone");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
