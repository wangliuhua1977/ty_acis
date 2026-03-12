using System.Text.Json;

namespace TianyiVision.Acis.Services.Settings;

public sealed class FileAppPreferencesService : IAppPreferencesService
{
    private readonly string _filePath;

    public FileAppPreferencesService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TianyiVision.Acis");
        _filePath = Path.Combine(directory, "app-preferences.json");
    }

    public AppPreferencesSnapshot Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppPreferencesSnapshot(null, null, [], []);
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize<AppPreferencesSnapshot>(stream)
                ?? new AppPreferencesSnapshot(null, null, [], []);
        }
        catch
        {
            return new AppPreferencesSnapshot(null, null, [], []);
        }
    }

    public void Save(AppPreferencesSnapshot snapshot)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(_filePath);
        JsonSerializer.Serialize(stream, snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public void Reset()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
