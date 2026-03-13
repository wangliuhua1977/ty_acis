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
                    new NotificationChannelSettings("default", "Default Dispatch Channel", string.Empty, false, true)
                ]));
        var normalized = Normalize(settings);
        _documentStore.Save(_paths.DispatchNotificationFile, normalized);
        return normalized;
    }

    public void Save(DispatchNotificationSettings settings)
    {
        _documentStore.Save(_paths.DispatchNotificationFile, Normalize(settings));
    }

    private static DispatchNotificationSettings Normalize(DispatchNotificationSettings settings)
    {
        var sourceChannels = settings.Channels ?? Array.Empty<NotificationChannelSettings>();
        var channels = sourceChannels.Count == 0
            ? new List<NotificationChannelSettings>
            {
                new("default", "Default Dispatch Channel", string.Empty, false, true)
            }
            : sourceChannels
                .Select(channel => new NotificationChannelSettings(
                    string.IsNullOrWhiteSpace(channel.ChannelId) ? "default" : channel.ChannelId.Trim(),
                    string.IsNullOrWhiteSpace(channel.DisplayName) ? "Dispatch Channel" : channel.DisplayName.Trim(),
                    channel.WebhookUrl?.Trim() ?? string.Empty,
                    channel.IsEnabled,
                    channel.IsDefault))
                .GroupBy(channel => channel.ChannelId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

        var defaultChannelId = channels.FirstOrDefault(channel => channel.IsDefault)?.ChannelId
            ?? channels.FirstOrDefault(channel => channel.IsEnabled)?.ChannelId
            ?? channels[0].ChannelId;
        var normalizedChannels = channels
            .Select(channel => channel with
            {
                IsDefault = string.Equals(channel.ChannelId, defaultChannelId, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        var mode = DispatchNotificationSettingsExtensions.NormalizeMode(settings.ServiceMode);
        return new DispatchNotificationSettings(
            mode,
            settings.EnableDemoFallback || mode == DispatchNotificationSettingsExtensions.AutoFallbackMode,
            normalizedChannels);
    }
}
