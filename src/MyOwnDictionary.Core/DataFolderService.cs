using Microsoft.Data.Sqlite;

namespace MyOwnDictionary.Core;

public readonly record struct DataFolderSwitch(bool Restart, string? Error);

public static class DataFolderService
{
    public static async Task<DataFolderSwitch> SwitchAsync(
        AppDatabase database,
        SettingsStore settings,
        string? folder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetFolder = AppPaths.IsDefaultFolder(folder) ? AppPaths.Root : Path.GetFullPath(folder!.Trim());
            Directory.CreateDirectory(targetFolder);
            var targetDb = Path.Combine(targetFolder, AppPaths.DatabaseFileName);
            var currentDb = AppPaths.DatabaseFile(settings.Current);
            if (!File.Exists(targetDb) && !AppPaths.SamePath(currentDb, targetDb) && File.Exists(currentDb))
                await database.BackupToAsync(targetDb, cancellationToken).ConfigureAwait(false);

            var stored = AppPaths.IsDefaultFolder(targetFolder) ? "" : targetFolder;
            var changed = !AppPaths.SamePath(currentDb, targetDb)
                || !string.Equals(settings.Current.DataFolder.Trim(), stored, StringComparison.OrdinalIgnoreCase);
            if (changed)
                settings.Save(settings.Current with { DataFolder = stored });

            return new DataFolderSwitch(changed, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            return new DataFolderSwitch(false, ex.Message);
        }
    }
}
