using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Configuration;

public sealed class FileNotificationSettingsService : INotificationSettingsService
{
    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FileNotificationSettingsService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public DispatchNotificationSettings Load()
    {
        return _documentStore.LoadOrCreate(
            _paths.DispatchNotificationFile,
            () => new DispatchNotificationSettings(
            [
                new NotificationChannelSettings("default", "默认派单通道", string.Empty, false)
            ]));
    }
}
