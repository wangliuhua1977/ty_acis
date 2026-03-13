using System.Net.Http;
using System.Windows;
using TianyiVision.Acis.Infrastructure.Localization;
using TianyiVision.Acis.Infrastructure.Theming;
using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Demo;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Dispatch;
using TianyiVision.Acis.Services.Home;
using TianyiVision.Acis.Services.Integrations;
using TianyiVision.Acis.Services.Integrations.Ctyun;
using TianyiVision.Acis.Services.Inspection;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Reports;
using TianyiVision.Acis.Services.Settings;
using TianyiVision.Acis.Services.Storage;
using TianyiVision.Acis.Services.Theming;
using TianyiVision.Acis.Services.Time;
using TianyiVision.Acis.Core.Theming;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.UI.ViewModels;

namespace TianyiVision.Acis.App;

public sealed class AppBootstrapper
{
    private readonly IClockService _clockService;
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly IDispatchResponsibilitySettingsService _dispatchResponsibilitySettingsService;
    private readonly IHomeOverlayLayoutService _homeOverlayLayoutService;
    private readonly ILocalConfigurationBootstrapService _localConfigurationBootstrapService;
    private readonly IHomeDashboardService _homeDashboardService;
    private readonly IInspectionTaskService _inspectionTaskService;
    private readonly IDispatchNotificationService _dispatchNotificationService;
    private readonly IDispatchResponsibilityService _dispatchResponsibilityService;
    private readonly INotificationSettingsService _notificationSettingsService;
    private readonly IReportDataService _reportDataService;
    private readonly ITextService _textService;
    private readonly IThemeService _themeService;

    public AppBootstrapper()
    {
        var paths = new AcisLocalDataPaths();
        var documentStore = new JsonFileDocumentStore();
        var platformIntegrationSettingsService = new FilePlatformIntegrationSettingsService(paths, documentStore);
        var notificationSettingsService = new FileNotificationSettingsService(paths, documentStore);
        var notificationSettings = notificationSettingsService.Load();
        var responsibilitySettingsService = new FileDispatchResponsibilitySettingsService(paths, documentStore);
        var responsibilitySettings = responsibilitySettingsService.Load();
        var notificationHistoryService = new FileDispatchNotificationHistoryService(paths, documentStore);
        var workOrderSnapshotService = new FileDispatchWorkOrderSnapshotService(paths, documentStore);
        var platformSettings = platformIntegrationSettingsService.Load();
        var demoDeviceCatalogService = new DemoDeviceCatalogService();
        var demoAlertQueryService = new DemoAlertQueryService();
        var demoHomeDashboardService = new DemoHomeDashboardService();
        var demoInspectionTaskService = new DemoInspectionTaskService();
        var demoDispatchNotificationService = new DemoDispatchNotificationService();
        var demoDispatchResponsibilityService = new DemoDispatchResponsibilityService(demoDispatchNotificationService);
        var demoReportDataService = new DemoReportDataService();

        _themeService = new ThemeService(new ThemeCatalogProvider());
        _textService = new TextService(new TerminologyCatalogProvider());
        _clockService = new SystemClockService();
        _notificationSettingsService = notificationSettingsService;
        _dispatchResponsibilitySettingsService = responsibilitySettingsService;
        _homeOverlayLayoutService = new FileHomeOverlayLayoutService(paths, documentStore);
        _appPreferencesService = new FileAppPreferencesService(paths, documentStore);
        _localConfigurationBootstrapService = new LocalConfigurationBootstrapService(
            _appPreferencesService,
            _homeOverlayLayoutService,
            platformIntegrationSettingsService,
            notificationSettingsService);

        var ctyunRuntime = CreateCtyunRuntime(platformSettings);
        var deviceCatalogService = BuildDeviceCatalogService(platformSettings, demoDeviceCatalogService, ctyunRuntime);
        var alertQueryService = BuildAlertQueryService(platformSettings, demoAlertQueryService, ctyunRuntime);
        var pointDetailService = BuildPointDetailService(platformSettings, demoDeviceCatalogService, deviceCatalogService, ctyunRuntime);
        var deviceWorkspaceService = new DeviceWorkspaceService(deviceCatalogService, pointDetailService);
        _dispatchResponsibilityService = BuildResponsibilityService(
            responsibilitySettings,
            responsibilitySettingsService,
            workOrderSnapshotService,
            demoDispatchResponsibilityService);
        var faultPoolService = BuildFaultPoolService(
            deviceWorkspaceService,
            alertQueryService,
            _dispatchResponsibilityService,
            demoDispatchNotificationService,
            platformSettings);
        var dispatchNotificationSender = BuildDispatchNotificationSender(notificationSettings, notificationSettingsService);
        _homeDashboardService = new ConfigDrivenHomeDashboardService(deviceWorkspaceService, faultPoolService, demoHomeDashboardService);
        _inspectionTaskService = new ConfigDrivenInspectionTaskService(deviceWorkspaceService, faultPoolService, demoInspectionTaskService);
        _dispatchNotificationService = new ConfigDrivenDispatchNotificationService(
            faultPoolService,
            dispatchNotificationSender,
            notificationHistoryService,
            workOrderSnapshotService,
            demoDispatchNotificationService,
            notificationSettings.IsAutoFallback() || notificationSettings.EnableDemoFallback);
        _reportDataService = new ConfigDrivenReportDataService(
            _dispatchNotificationService,
            _textService,
            demoReportDataService);

        LoadLocalConfiguration();
    }

    public void ApplyTheme(ResourceDictionary applicationResources)
    {
        ApplyTheme(applicationResources, _themeService.ActiveTheme);
    }

    public void ApplyTheme(ResourceDictionary applicationResources, ThemeDefinition theme)
    {
        var themeResources = ThemeResourceBuilder.Build(theme);
        var existingIndex = -1;

        for (var index = 0; index < applicationResources.MergedDictionaries.Count; index++)
        {
            if (applicationResources.MergedDictionaries[index].Contains(ThemeBrushKeys.WindowBackgroundBrush))
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            applicationResources.MergedDictionaries[existingIndex] = themeResources;
        }
        else
        {
            applicationResources.MergedDictionaries.Insert(0, themeResources);
        }
    }

    public ShellViewModel CreateShellViewModel(ResourceDictionary applicationResources)
    {
        var pageFactories = new Dictionary<AppSectionId, Func<PageViewModelBase>>
        {
            [AppSectionId.Home] = () => new HomePageViewModel(_textService, _homeOverlayLayoutService, _homeDashboardService),
            [AppSectionId.Inspection] = () => new InspectionPageViewModel(_textService, _inspectionTaskService),
            [AppSectionId.Dispatch] = () => new DispatchPageViewModel(_textService, _dispatchNotificationService, _dispatchResponsibilityService),
            [AppSectionId.Reports] = () => new ReportsPageViewModel(_textService, _reportDataService),
            [AppSectionId.Settings] = () => new SettingsPageViewModel(
                _textService,
                _themeService,
                _appPreferencesService,
                _dispatchResponsibilitySettingsService,
                _notificationSettingsService,
                theme => ApplyTheme(applicationResources, theme))
        };

        return new ShellViewModel(_textService, _clockService, pageFactories);
    }

    private void LoadLocalConfiguration()
    {
        var snapshot = _localConfigurationBootstrapService.Initialize().Preferences;

        foreach (var theme in snapshot.Themes)
        {
            _themeService.SetTheme(new ThemeDefinition(
                theme.Id,
                theme.DisplayName,
                theme.Description,
                new Dictionary<string, string>(theme.Colors, StringComparer.Ordinal)));
        }

        foreach (var terminology in snapshot.Terminologies)
        {
            _textService.SetProfile(new TerminologyProfile(
                terminology.Id,
                terminology.DisplayName,
                new Dictionary<string, string>(terminology.TextEntries, StringComparer.Ordinal),
                new Dictionary<string, string>(terminology.Variables, StringComparer.Ordinal)));
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ActiveThemeId))
        {
            _themeService.SetTheme(snapshot.ActiveThemeId);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ActiveTerminologyId))
        {
            _textService.SetProfile(snapshot.ActiveTerminologyId);
        }
    }

    private static CtyunOpenPlatformClient? CreateCtyunRuntime(PlatformIntegrationSettings settings)
    {
        if (!settings.IsCtyunPreferred())
        {
            return null;
        }

        var issues = settings.GetCtyunConfigurationIssues();
        if (issues.Count > 0)
        {
            System.Diagnostics.Trace.WriteLine(string.Join(Environment.NewLine, issues));
            return null;
        }

        var httpClient = new HttpClient();
        var tokenService = new CtyunAccessTokenService(httpClient, settings);
        return new CtyunOpenPlatformClient(httpClient, settings, tokenService);
    }

    private static IDeviceCatalogService BuildDeviceCatalogService(
        PlatformIntegrationSettings settings,
        IDeviceCatalogService demoDeviceCatalogService,
        CtyunOpenPlatformClient? ctyunRuntime)
    {
        if (ctyunRuntime is null)
        {
            return demoDeviceCatalogService;
        }
        var ctyunService = new CtyunDeviceCatalogService(ctyunRuntime, new CtyunDeviceListAdapter(), settings);

        return settings.IsAutoFallback() || settings.OpenPlatform.EnableDemoFallback
            ? new FallbackDeviceCatalogService(ctyunService, demoDeviceCatalogService)
            : ctyunService;
    }

    private static IAlertQueryService BuildAlertQueryService(
        PlatformIntegrationSettings settings,
        IAlertQueryService demoAlertQueryService,
        CtyunOpenPlatformClient? ctyunRuntime)
    {
        if (ctyunRuntime is null)
        {
            return demoAlertQueryService;
        }
        var ctyunService = new CtyunAlertQueryService(ctyunRuntime, new CtyunAiAlertAdapter(), new CtyunDeviceAlertAdapter());

        return settings.IsAutoFallback() || settings.OpenPlatform.EnableDemoFallback
            ? new FallbackAlertQueryService(ctyunService, demoAlertQueryService)
            : ctyunService;
    }

    private static IDevicePointDetailService BuildPointDetailService(
        PlatformIntegrationSettings settings,
        IDeviceCatalogService demoDeviceCatalogService,
        IDeviceCatalogService activeDeviceCatalogService,
        CtyunOpenPlatformClient? ctyunRuntime)
    {
        var demoService = new DemoDevicePointDetailService(demoDeviceCatalogService);
        if (ctyunRuntime is null)
        {
            return demoService;
        }

        var ctyunService = new CtyunDevicePointDetailService(ctyunRuntime, activeDeviceCatalogService);
        return settings.IsAutoFallback() || settings.OpenPlatform.EnableDemoFallback
            ? new FallbackDevicePointDetailService(ctyunService, demoService)
            : ctyunService;
    }

    private static IFaultPoolService BuildFaultPoolService(
        IDeviceWorkspaceService deviceWorkspaceService,
        IAlertQueryService alertQueryService,
        IDispatchResponsibilityService dispatchResponsibilityService,
        IDispatchNotificationService demoDispatchNotificationService,
        PlatformIntegrationSettings settings)
    {
        var demoService = new DemoFaultPoolService(demoDispatchNotificationService);
        if (!settings.IsCtyunPreferred())
        {
            return demoService;
        }

        var ctyunService = new ConfigDrivenFaultPoolService(deviceWorkspaceService, alertQueryService, dispatchResponsibilityService);
        return settings.IsAutoFallback() || settings.OpenPlatform.EnableDemoFallback
            ? new FallbackFaultPoolService(ctyunService, demoService)
            : ctyunService;
    }

    private static IDispatchResponsibilityService BuildResponsibilityService(
        DispatchResponsibilitySettings settings,
        IDispatchResponsibilitySettingsService settingsService,
        IDispatchWorkOrderSnapshotService workOrderSnapshotService,
        IDispatchResponsibilityService demoService)
    {
        if (!settings.IsLocalFilePreferred())
        {
            return demoService;
        }

        var fileService = new FileDispatchResponsibilityService(settingsService, workOrderSnapshotService);
        return settings.IsAutoFallback() || settings.EnableDemoFallback
            ? new FallbackDispatchResponsibilityService(fileService, demoService)
            : fileService;
    }

    private static IDispatchNotificationSender? BuildDispatchNotificationSender(
        DispatchNotificationSettings settings,
        INotificationSettingsService notificationSettingsService)
    {
        if (!settings.IsEnterpriseWeChatPreferred())
        {
            return null;
        }

        return new EnterpriseWeChatDispatchNotificationSender(new HttpClient(), notificationSettingsService);
    }
}
