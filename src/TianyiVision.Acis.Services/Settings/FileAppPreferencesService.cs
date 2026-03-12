using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Settings;

public sealed class FileAppPreferencesService : IAppPreferencesService
{
    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;
    private readonly string _legacyFilePath;

    public FileAppPreferencesService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
        _legacyFilePath = Path.Combine(paths.RootDirectory, "app-preferences.json");
    }

    public AppPreferencesSnapshot Load()
    {
        if (!File.Exists(_paths.AppearanceFile) && File.Exists(_legacyFilePath))
        {
            TryMigrateLegacyPreferences();
        }

        var appearance = _documentStore.LoadOrCreate(
            _paths.AppearanceFile,
            () => new AppearanceSettingsDocument(null, null));
        var themeCatalog = _documentStore.LoadOrCreate(
            _paths.ThemeCatalogFile,
            () => new ThemeCatalogDocument([]));
        var terminologyCatalog = _documentStore.LoadOrCreate(
            _paths.TerminologyCatalogFile,
            () => new TerminologyCatalogDocument([]));

        return new AppPreferencesSnapshot(
            appearance.ActiveThemeId,
            appearance.ActiveTerminologyId,
            themeCatalog.Themes,
            terminologyCatalog.Terminologies);
    }

    public void Save(AppPreferencesSnapshot snapshot)
    {
        _documentStore.Save(
            _paths.AppearanceFile,
            new AppearanceSettingsDocument(snapshot.ActiveThemeId, snapshot.ActiveTerminologyId));
        _documentStore.Save(
            _paths.ThemeCatalogFile,
            new ThemeCatalogDocument(snapshot.Themes));
        _documentStore.Save(
            _paths.TerminologyCatalogFile,
            new TerminologyCatalogDocument(snapshot.Terminologies));
    }

    public void Reset()
    {
        _documentStore.DeleteIfExists(_paths.AppearanceFile);
        _documentStore.DeleteIfExists(_paths.ThemeCatalogFile);
        _documentStore.DeleteIfExists(_paths.TerminologyCatalogFile);
    }

    private void TryMigrateLegacyPreferences()
    {
        try
        {
            using var stream = File.OpenRead(_legacyFilePath);
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<AppPreferencesSnapshot>(stream);
            if (snapshot is not null)
            {
                Save(snapshot);
                _documentStore.DeleteIfExists(_legacyFilePath);
            }
        }
        catch
        {
            // Ignore legacy migration failure and fall back to the new default documents.
        }
    }
}
