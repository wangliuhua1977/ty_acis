using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Settings;

namespace TianyiVision.Acis.Services.Configuration;

public sealed record OpenPlatformSettings(
    string BaseUrl,
    string AppId,
    string AppSecret,
    string RsaPrivateKey,
    string Version,
    string ApiVersion,
    string ClientType,
    string GrantType,
    string EnterpriseUser,
    string ParentUser);

public sealed record MapProviderSettings(
    string AmapWebJsApiKey,
    string AmapSecurityJsCode,
    string AmapJsApiVersion,
    string CoordinateSystem);

public sealed record PlatformIntegrationSettings(
    OpenPlatformSettings OpenPlatform,
    MapProviderSettings MapProvider);

public sealed record NotificationChannelSettings(
    string ChannelId,
    string DisplayName,
    string WebhookUrl,
    bool IsEnabled);

public sealed record DispatchNotificationSettings(
    IReadOnlyList<NotificationChannelSettings> Channels);

public sealed record LocalConfigurationSnapshot(
    AppPreferencesSnapshot Preferences,
    HomeOverlayLayoutSnapshot HomeOverlayLayout,
    PlatformIntegrationSettings PlatformIntegration,
    DispatchNotificationSettings NotificationSettings);
