using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Layout;

public sealed class FileHomeOverlayLayoutService : IHomeOverlayLayoutService
{
    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;
    private readonly string _legacyFilePath;

    public FileHomeOverlayLayoutService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
        _legacyFilePath = Path.Combine(paths.RootDirectory, "home-overlay-layout.json");
    }

    public HomeOverlayLayoutSnapshot Load()
    {
        if (!File.Exists(_paths.HomeOverlayLayoutFile) && File.Exists(_legacyFilePath))
        {
            TryMigrateLegacyLayout();
        }

        return _documentStore.LoadOrCreate(_paths.HomeOverlayLayoutFile, () => new HomeOverlayLayoutSnapshot([]));
    }

    public void Save(HomeOverlayLayoutSnapshot snapshot)
    {
        _documentStore.Save(_paths.HomeOverlayLayoutFile, snapshot);
    }

    public void Reset()
    {
        _documentStore.DeleteIfExists(_paths.HomeOverlayLayoutFile);
    }

    private void TryMigrateLegacyLayout()
    {
        try
        {
            using var stream = File.OpenRead(_legacyFilePath);
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<HomeOverlayLayoutSnapshot>(stream);
            if (snapshot is not null)
            {
                _documentStore.Save(_paths.HomeOverlayLayoutFile, snapshot);
                _documentStore.DeleteIfExists(_legacyFilePath);
            }
        }
        catch
        {
            // Ignore legacy migration failure and fall back to the new default document.
        }
    }
}
