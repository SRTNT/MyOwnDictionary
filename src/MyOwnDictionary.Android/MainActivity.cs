using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using AvaloniaApp = MyOwnDictionary.App.App;

namespace MyOwnDictionary.Droid;

[Activity(
    Label = "MyOwnDictionary",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<AvaloniaApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AvaloniaApp.IsPhoneLayout = true;
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
