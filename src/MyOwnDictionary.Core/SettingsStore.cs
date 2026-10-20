using System.Text.Json;

namespace MyOwnDictionary.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly Lock _gate = new();
    private AppSettings _current;

    public SettingsStore(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _current = Load();
    }

    public AppSettings Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public void Save(AppSettings settings)
    {
        var normalized = settings.Normalized();
        lock (_gate)
        {
            _current = normalized;
            File.WriteAllText(_path, JsonSerializer.Serialize(normalized, JsonOptions));
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppSettings();

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)?.Normalized() ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
