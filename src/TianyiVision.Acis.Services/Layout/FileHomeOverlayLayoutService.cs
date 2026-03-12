using System.Text.Json;

namespace TianyiVision.Acis.Services.Layout;

public sealed class FileHomeOverlayLayoutService : IHomeOverlayLayoutService
{
    private readonly string _filePath;

    public FileHomeOverlayLayoutService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TianyiVision.Acis");
        _filePath = Path.Combine(directory, "home-overlay-layout.json");
    }

    public HomeOverlayLayoutSnapshot Load()
    {
        if (!File.Exists(_filePath))
        {
            return new HomeOverlayLayoutSnapshot([]);
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize<HomeOverlayLayoutSnapshot>(stream)
                ?? new HomeOverlayLayoutSnapshot([]);
        }
        catch
        {
            return new HomeOverlayLayoutSnapshot([]);
        }
    }

    public void Save(HomeOverlayLayoutSnapshot snapshot)
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
