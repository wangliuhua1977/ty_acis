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
        var settings = _documentStore.LoadOrCreate(
            _paths.DispatchNotificationFile,
            () => new DispatchNotificationSettings(
                "AutoFallback",
                true,
                [
                    new NotificationChannelSettings("default", "Default Dispatch Channel", string.Empty, false)
                ]));
        var normalized = Normalize(settings);
        _documentStore.Save(_paths.DispatchNotificationFile, normalized);
        return normalized;
    }

    private static DispatchNotificationSettings Normalize(DispatchNotificationSettings settings)
    {
        var sourceChannels = settings.Channels ?? Array.Empty<NotificationChannelSettings>();
        var channels = sourceChannels.Count == 0
            ? new List<NotificationChannelSettings>
            {
                new("default", "Default Dispatch Channel", string.Empty, false)
            }
            : sourceChannels
                .Select(channel => new NotificationChannelSettings(
                    string.IsNullOrWhiteSpace(channel.ChannelId) ? "default" : channel.ChannelId.Trim(),
                    string.IsNullOrWhiteSpace(channel.DisplayName) ? "Dispatch Channel" : channel.DisplayName.Trim(),
                    channel.WebhookUrl?.Trim() ?? string.Empty,
                    channel.IsEnabled))
                .ToList();

        var mode = DispatchNotificationSettingsExtensions.NormalizeMode(settings.ServiceMode);
        return new DispatchNotificationSettings(
            mode,
            settings.EnableDemoFallback || mode == DispatchNotificationSettingsExtensions.AutoFallbackMode,
            channels);
    }
}
