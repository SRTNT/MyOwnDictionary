using Avalonia.Media;

namespace MyOwnDictionary.App.Services;

public static class AppFonts
{
    public static FontFamily Title { get; } = new("avares://MyOwnDictionary/Assets/Fonts/Poppins-SemiBold.ttf#Poppins SemiBold");
    public static FontFamily Body { get; } = new("avares://MyOwnDictionary/Assets/Fonts/OpenSans-Regular.ttf#Open Sans");
    public static FontFamily Persian { get; } = new("avares://MyOwnDictionary/Assets/Fonts/Vazirmatn-Regular.ttf#Vazirmatn");
}
