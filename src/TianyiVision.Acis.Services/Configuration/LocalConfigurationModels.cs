using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Settings;

namespace TianyiVision.Acis.Services.Configuration;

public sealed record OpenPlatformSettings(
    string ServiceMode,
    bool EnableDemoFallback,
    string BaseUrl,
    string AppId,
    string AppKey,
    string AppSecret,
    string RsaPrivateKey,
    string Version,
    string ApiVersion,
    string ClientType,
    string GrantType,
    string EnterpriseUser,
    string ParentUser,
    OpenPlatformTokenSettings Token,
    OpenPlatformDeviceApiSettings DeviceApi,
    OpenPlatformAlarmApiSettings AlarmApi);

public sealed record OpenPlatformTokenSettings(
    string AccessTokenPath,
    string RefreshGrantType,
    int ReuseBeforeExpirySeconds);

public sealed record OpenPlatformDeviceApiSettings(
    string DeviceListPath,
    string DeviceDetailPath,
    int PageSize,
    long InitialLastId,
    int HasChildDevices,
    int DetailEnrichmentLimit);

public sealed record OpenPlatformAlarmApiSettings(
    string AiAlertListPath,
    string DeviceAlertListPath,
    int PageNo,
    int PageSize,
    int AiAlertSource,
    int DeviceAlertSource,
    string AiAlertTypeList,
    string DeviceAlertTypeList,
    int LookbackHours);

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
