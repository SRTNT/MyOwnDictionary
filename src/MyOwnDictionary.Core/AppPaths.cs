namespace MyOwnDictionary.Core;

public static class AppPaths
{
    public const string DatabaseFileName = "dictionary.db";

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyOwnDictionary");

    public static string Settings { get; } = Path.Combine(Root, "settings.json");

    public static bool IsDefaultFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return true;

        return SamePath(folder, Root);
    }

    public static string DatabaseFile(AppSettings settings)
    {
        var folder = IsDefaultFolder(settings.DataFolder) ? Root : Path.GetFullPath(settings.DataFolder.Trim());
        return Path.Combine(folder, DatabaseFileName);
    }

    public static string? OneDriveDataFolder()
    {
        foreach (var name in new[] { "OneDriveConsumer", "OneDrive" })
        {
            var root = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
                return Path.Combine(root, "MyOwnDictionary");
        }

        return null;
    }

    public static string? GoogleDriveDataFolder()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            @"G:\My Drive",
            Path.Combine(profile, "Google Drive"),
            Path.Combine(profile, "Google Drive", "My Drive")
        };

        foreach (var root in roots)
        {
            if (Directory.Exists(root))
                return Path.Combine(root, "MyOwnDictionary");
        }

        return null;
    }

    public static bool SamePath(string left, string right)
    {
        var a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
