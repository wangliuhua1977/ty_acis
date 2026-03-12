using System.Windows;
using TianyiVision.Acis.Infrastructure.Localization;
using TianyiVision.Acis.Infrastructure.Theming;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Demo;
using TianyiVision.Acis.Services.Dispatch;
using TianyiVision.Acis.Services.Home;
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
    private readonly IHomeOverlayLayoutService _homeOverlayLayoutService;
    private readonly ILocalConfigurationBootstrapService _localConfigurationBootstrapService;
    private readonly IHomeDashboardService _homeDashboardService;
    private readonly IInspectionTaskService _inspectionTaskService;
    private readonly IDispatchNotificationService _dispatchNotificationService;
    private readonly IReportDataService _reportDataService;
    private readonly ITextService _textService;
    private readonly IThemeService _themeService;

    public AppBootstrapper()
    {
        var paths = new AcisLocalDataPaths();
        var documentStore = new JsonFileDocumentStore();

        _themeService = new ThemeService(new ThemeCatalogProvider());
        _textService = new TextService(new TerminologyCatalogProvider());
        _clockService = new SystemClockService();
        _homeOverlayLayoutService = new FileHomeOverlayLayoutService(paths, documentStore);
        _appPreferencesService = new FileAppPreferencesService(paths, documentStore);
        _localConfigurationBootstrapService = new LocalConfigurationBootstrapService(
            _appPreferencesService,
            _homeOverlayLayoutService,
            new FilePlatformIntegrationSettingsService(paths, documentStore),
            new FileNotificationSettingsService(paths, documentStore));
        _homeDashboardService = new DemoHomeDashboardService();
        _inspectionTaskService = new DemoInspectionTaskService();
        _dispatchNotificationService = new DemoDispatchNotificationService();
        _reportDataService = new DemoReportDataService();

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
            [AppSectionId.Dispatch] = () => new DispatchPageViewModel(_textService, _dispatchNotificationService),
            [AppSectionId.Reports] = () => new ReportsPageViewModel(_textService, _reportDataService),
            [AppSectionId.Settings] = () => new SettingsPageViewModel(
                _textService,
                _themeService,
                _appPreferencesService,
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
}
