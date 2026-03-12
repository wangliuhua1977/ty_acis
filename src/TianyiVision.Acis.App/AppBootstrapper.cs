using System.Windows;
using TianyiVision.Acis.Infrastructure.Localization;
using TianyiVision.Acis.Infrastructure.Theming;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Theming;
using TianyiVision.Acis.Services.Time;
using TianyiVision.Acis.Core.Theming;
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
        var homePage = new HomePageViewModel(_textService, _homeOverlayLayoutService);
        var inspectionPage = new InspectionPageViewModel(_textService);
        var dispatchPage = new DispatchPageViewModel(_textService);
        var reportsPage = new ReportsPageViewModel(_textService);
        var settingsPage = new SettingsPageViewModel(
            _textService,
            _themeService,
            theme => ApplyTheme(applicationResources, theme));

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
