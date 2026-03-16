using System.Net.Http;
using System.Windows;
using TianyiVision.Acis.Infrastructure.Localization;
using TianyiVision.Acis.Infrastructure.Theming;
using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Demo;
using TianyiVision.Acis.Services.Diagnostics;
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
    private static readonly string[] LegacyMapTechnicalMarkers =
    [
        "真实地图",
        "地图 SDK",
        "SDK",
        "假数据",
        "模拟底图",
        "地图模式",
        "已加载",
        "开发态"
    ];

    private static readonly string[] MapUiTokensToSanitize =
    [
        TextTokens.HomeMapStageDescription,
        TextTokens.HomeMapStageHint,
        TextTokens.InspectionDescription,
        TextTokens.InspectionWorkbenchDescription,
        TextTokens.InspectionWorkbenchHint,
        TextTokens.InspectionMapModeReal,
        TextTokens.InspectionMapModeFallback
    ];

    private readonly IClockService _clockService;
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly IDispatchResponsibilitySettingsService _dispatchResponsibilitySettingsService;
    private readonly IHomeOverlayLayoutService _homeOverlayLayoutService;
    private readonly ILocalConfigurationBootstrapService _localConfigurationBootstrapService;
    private readonly IHomeDashboardService _homeDashboardService;
    private readonly IInspectionTaskService _inspectionTaskService;
    private readonly IInspectionSettingsService _inspectionSettingsService;
    private readonly IDispatchNotificationService _dispatchNotificationService;
    private readonly IDispatchResponsibilityService _dispatchResponsibilityService;
    private readonly INotificationSettingsService _notificationSettingsService;
    private readonly IReportDataService _reportDataService;
    private readonly PointSelectionContext _pointSelectionContext;
    private readonly ITextService _textService;
    private readonly IThemeService _themeService;
    private readonly MapProviderSettings _mapProviderSettings;

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
        var inspectionSettingsService = new FileInspectionSettingsService(paths, documentStore);
        var inspectionTaskHistoryStore = new FileInspectionTaskHistoryStore(paths, documentStore);
        var inspectionPointCheckExecutor = new ReservedInspectionPointCheckExecutor();
        var platformSettings = platformIntegrationSettingsService.Load();
        var ctyunConfigurationIssues = platformSettings.GetCtyunConfigurationIssues();
        var demoDeviceCatalogService = new DemoDeviceCatalogService();
        var demoAlertQueryService = new DemoAlertQueryService();
        var demoHomeDashboardService = new DemoHomeDashboardService();
        var demoDispatchNotificationService = new DemoDispatchNotificationService();
        var demoDispatchResponsibilityService = new DemoDispatchResponsibilityService(demoDispatchNotificationService);
        var demoReportDataService = new DemoReportDataService();
        _mapProviderSettings = platformSettings.MapProvider;

        MapPointSourceDiagnostics.WriteLines("Configuration", [
            $"diagnosticLogFile = {MapPointSourceDiagnostics.LogFilePath}",
            $"serviceMode = {PlatformIntegrationSettingsExtensions.NormalizeMode(platformSettings.OpenPlatform.ServiceMode)}",
            $"enableDemoFallback = {platformSettings.OpenPlatform.EnableDemoFallback}",
            "mapCoordinateConversionPath = amap-js-convertFrom(baidu)",
            $"ctyunConfiguration = {(ctyunConfigurationIssues.Count == 0 ? "complete" : "missing")}",
            $"ctyunConfigurationIssues = {(ctyunConfigurationIssues.Count == 0 ? "none" : string.Join("; ", ctyunConfigurationIssues.Select(ExtractConfigurationIssueField)))}"
        ]);

        _themeService = new ThemeService(new ThemeCatalogProvider());
        _textService = new TextService(new TerminologyCatalogProvider());
        _clockService = new SystemClockService();
        _pointSelectionContext = new PointSelectionContext();
        _notificationSettingsService = notificationSettingsService;
        _dispatchResponsibilitySettingsService = responsibilitySettingsService;
        _inspectionSettingsService = inspectionSettingsService;
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
        var pointWorkspaceService = new ConfigDrivenPointWorkspaceService(deviceWorkspaceService, faultPoolService);
        var dispatchNotificationSender = BuildDispatchNotificationSender(notificationSettings, notificationSettingsService);
        _homeDashboardService = new ConfigDrivenHomeDashboardService(pointWorkspaceService, demoHomeDashboardService);
        _inspectionTaskService = new ConfigDrivenInspectionTaskService(
            pointWorkspaceService,
            inspectionSettingsService,
            inspectionTaskHistoryStore,
            inspectionPointCheckExecutor);
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

        MapPointSourceDiagnostics.WriteLines("ServiceSelection", [
            $"homeMapPointDataService = {_homeDashboardService.GetType().Name} -> {pointWorkspaceService.GetType().Name} -> {deviceWorkspaceService.GetType().Name} -> {deviceCatalogService.GetType().Name}",
            $"inspectionMapPointDataService = {_inspectionTaskService.GetType().Name} -> {pointWorkspaceService.GetType().Name} -> {deviceWorkspaceService.GetType().Name} -> {deviceCatalogService.GetType().Name}",
            $"pointDetailService = {pointDetailService.GetType().Name}",
            $"ctyunRuntime = {(ctyunRuntime is null ? "disabled" : "enabled")}",
            $"demoFallbackBranchEnabled = {platformSettings.IsAutoFallback() || platformSettings.OpenPlatform.EnableDemoFallback}",
            $"deviceCatalogFallbackBranch = {DescribeFallbackBranch(deviceCatalogService)}",
            $"pointDetailFallbackBranch = {DescribeFallbackBranch(pointDetailService)}"
        ]);

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
            [AppSectionId.Home] = () => new HomePageViewModel(
                _textService,
                _homeOverlayLayoutService,
                _homeDashboardService,
                _pointSelectionContext,
                _mapProviderSettings),
            [AppSectionId.Inspection] = () => new InspectionPageViewModel(
                _textService,
                _inspectionTaskService,
                _pointSelectionContext,
                _mapProviderSettings),
            [AppSectionId.Dispatch] = () => new DispatchPageViewModel(_textService, _dispatchNotificationService, _dispatchResponsibilityService),
            [AppSectionId.Reports] = () => new ReportsPageViewModel(_textService, _reportDataService),
            [AppSectionId.Settings] = () => new SettingsPageViewModel(
                _textService,
                _themeService,
                _appPreferencesService,
                _inspectionSettingsService,
                _dispatchResponsibilitySettingsService,
                _notificationSettingsService,
                theme => ApplyTheme(applicationResources, theme))
        };

        return new ShellViewModel(_textService, _clockService, _homeDashboardService, pageFactories);
    }

    private void LoadLocalConfiguration()
    {
        var snapshot = _localConfigurationBootstrapService.Initialize().Preferences;
        var bundledTerminologies = _textService.GetAvailableProfiles()
            .ToDictionary(profile => profile.Id, profile => profile, StringComparer.Ordinal);
        snapshot = SanitizeLegacyMapTerminology(snapshot, bundledTerminologies, out var hasSanitizedTerminology);

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
            bundledTerminologies.TryGetValue(terminology.Id, out var bundledProfile);
            var mergedTextEntries = bundledProfile is null
                ? new Dictionary<string, string>(terminology.TextEntries, StringComparer.Ordinal)
                : new Dictionary<string, string>(bundledProfile.TextEntries, StringComparer.Ordinal);
            var mergedVariables = bundledProfile is null
                ? new Dictionary<string, string>(terminology.Variables, StringComparer.Ordinal)
                : new Dictionary<string, string>(bundledProfile.Variables, StringComparer.Ordinal);

            foreach (var pair in terminology.TextEntries)
            {
                mergedTextEntries[pair.Key] = pair.Value;
            }

            foreach (var pair in terminology.Variables)
            {
                mergedVariables[pair.Key] = pair.Value;
            }

            _textService.SetProfile(new TerminologyProfile(
                terminology.Id,
                terminology.DisplayName,
                mergedTextEntries,
                mergedVariables));
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ActiveThemeId))
        {
            _themeService.SetTheme(snapshot.ActiveThemeId);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ActiveTerminologyId))
        {
            _textService.SetProfile(snapshot.ActiveTerminologyId);
        }

        if (hasSanitizedTerminology)
        {
            _appPreferencesService.Save(snapshot);
        }
    }

    private static CtyunOpenPlatformClient? CreateCtyunRuntime(PlatformIntegrationSettings settings)
    {
        if (!settings.IsCtyunPreferred())
        {
            MapPointSourceDiagnostics.Write(
                "ServiceSelection",
                $"CTYun runtime disabled because serviceMode = {PlatformIntegrationSettingsExtensions.NormalizeMode(settings.OpenPlatform.ServiceMode)}.");
            return null;
        }

        var issues = settings.GetCtyunConfigurationIssues();
        if (issues.Count > 0)
        {
            System.Diagnostics.Trace.WriteLine(string.Join(Environment.NewLine, issues));
            MapPointSourceDiagnostics.Write(
                "Configuration",
                $"CTYun runtime disabled because configuration is missing: {string.Join("; ", issues.Select(ExtractConfigurationIssueField))}");
            return null;
        }

        var httpClient = new HttpClient();
        var tokenService = new CtyunAccessTokenService(httpClient, settings);
        MapPointSourceDiagnostics.Write("ServiceSelection", "CTYun runtime enabled.");
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
        return ctyunService;
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

    private static AppPreferencesSnapshot SanitizeLegacyMapTerminology(
        AppPreferencesSnapshot snapshot,
        IReadOnlyDictionary<string, TerminologyProfile> bundledTerminologies,
        out bool hasChanges)
    {
        hasChanges = false;
        if (snapshot.Terminologies.Count == 0)
        {
            return snapshot;
        }

        bundledTerminologies.TryGetValue("telecom", out var fallbackProfile);
        var sanitizedTerminologies = new List<StoredTerminologyPreference>(snapshot.Terminologies.Count);

        foreach (var terminology in snapshot.Terminologies)
        {
            bundledTerminologies.TryGetValue(terminology.Id, out var bundledProfile);
            bundledProfile ??= fallbackProfile;

            if (bundledProfile is null)
            {
                sanitizedTerminologies.Add(terminology);
                continue;
            }

            var textEntries = new Dictionary<string, string>(terminology.TextEntries, StringComparer.Ordinal);
            var terminologyChanged = false;

            foreach (var token in MapUiTokensToSanitize)
            {
                if (!textEntries.TryGetValue(token, out var currentValue)
                    || !ContainsLegacyMapTechnicalMarker(currentValue))
                {
                    continue;
                }

                var bundledValue = bundledProfile.Resolve(token);
                if (string.Equals(currentValue, bundledValue, StringComparison.Ordinal))
                {
                    continue;
                }

                textEntries[token] = bundledValue;
                terminologyChanged = true;
            }

            if (!terminologyChanged)
            {
                sanitizedTerminologies.Add(terminology);
                continue;
            }

            hasChanges = true;
            sanitizedTerminologies.Add(terminology with
            {
                TextEntries = textEntries
            });
        }

        return hasChanges
            ? snapshot with { Terminologies = sanitizedTerminologies }
            : snapshot;
    }

    private static bool ContainsLegacyMapTechnicalMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return LegacyMapTechnicalMarkers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeFallbackBranch(object service)
    {
        return service.GetType().Name.StartsWith("Fallback", StringComparison.Ordinal)
            ? "enabled"
            : "disabled";
    }

    private static string ExtractConfigurationIssueField(string issue)
    {
        const string marker = "缺少 ";
        var normalized = issue?.Trim() ?? string.Empty;
        var index = normalized.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return normalized.TrimEnd('。');
        }

        return normalized[(index + marker.Length)..].Trim().TrimEnd('。');
    }
}
