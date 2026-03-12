using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Settings;

namespace TianyiVision.Acis.Services.Configuration;

public sealed class LocalConfigurationBootstrapService : ILocalConfigurationBootstrapService
{
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly IHomeOverlayLayoutService _homeOverlayLayoutService;
    private readonly IPlatformIntegrationSettingsService _platformIntegrationSettingsService;
    private readonly INotificationSettingsService _notificationSettingsService;

    public LocalConfigurationBootstrapService(
        IAppPreferencesService appPreferencesService,
        IHomeOverlayLayoutService homeOverlayLayoutService,
        IPlatformIntegrationSettingsService platformIntegrationSettingsService,
        INotificationSettingsService notificationSettingsService)
    {
        _appPreferencesService = appPreferencesService;
        _homeOverlayLayoutService = homeOverlayLayoutService;
        _platformIntegrationSettingsService = platformIntegrationSettingsService;
        _notificationSettingsService = notificationSettingsService;
    }

    public LocalConfigurationSnapshot Initialize()
    {
        return new LocalConfigurationSnapshot(
            _appPreferencesService.Load(),
            _homeOverlayLayoutService.Load(),
            _platformIntegrationSettingsService.Load(),
            _notificationSettingsService.Load());
    }
}
