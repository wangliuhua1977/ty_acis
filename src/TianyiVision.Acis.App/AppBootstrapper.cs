using System.Windows;
using TianyiVision.Acis.Infrastructure.Localization;
using TianyiVision.Acis.Infrastructure.Theming;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Theming;
using TianyiVision.Acis.Services.Time;
using TianyiVision.Acis.UI.ViewModels;

namespace TianyiVision.Acis.App;

public sealed class AppBootstrapper
{
    private readonly IClockService _clockService;
    private readonly IHomeOverlayLayoutService _homeOverlayLayoutService;
    private readonly ITextService _textService;
    private readonly IThemeService _themeService;

    public AppBootstrapper()
    {
        _themeService = new ThemeService(new ThemeCatalogProvider());
        _textService = new TextService(new TerminologyCatalogProvider());
        _clockService = new SystemClockService();
        _homeOverlayLayoutService = new FileHomeOverlayLayoutService();
    }

    public void ApplyTheme(ResourceDictionary applicationResources)
    {
        applicationResources.MergedDictionaries.Insert(0, ThemeResourceBuilder.Build(_themeService.ActiveTheme));
    }

    public ShellViewModel CreateShellViewModel()
    {
        var homePage = new HomePageViewModel(_textService, _homeOverlayLayoutService);
        var inspectionPage = new InspectionPageViewModel(_textService);
        var dispatchPage = new DispatchPageViewModel(_textService);
        var reportsPage = new ReportsPageViewModel(_textService);
        var settingsPage = new SettingsPageViewModel(_textService, _themeService);

        return new ShellViewModel(
            _textService,
            _clockService,
            homePage,
            inspectionPage,
            dispatchPage,
            reportsPage,
            settingsPage);
    }
}
