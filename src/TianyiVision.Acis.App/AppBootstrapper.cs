using System.Windows;
using TianyiVision.Acis.Infrastructure.Localization;
using TianyiVision.Acis.Infrastructure.Theming;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Settings;
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
    private readonly ITextService _textService;
    private readonly IThemeService _themeService;

    public AppBootstrapper()
    {
        _themeService = new ThemeService(new ThemeCatalogProvider());
        _textService = new TextService(new TerminologyCatalogProvider());
        _clockService = new SystemClockService();
        _homeOverlayLayoutService = new FileHomeOverlayLayoutService();
        _appPreferencesService = new FileAppPreferencesService();

        LoadPreferences();
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
            [AppSectionId.Home] = () => new HomePageViewModel(_textService, _homeOverlayLayoutService),
            [AppSectionId.Inspection] = () => new InspectionPageViewModel(_textService),
            [AppSectionId.Dispatch] = () => new DispatchPageViewModel(_textService),
            [AppSectionId.Reports] = () => new ReportsPageViewModel(_textService),
            [AppSectionId.Settings] = () => new SettingsPageViewModel(
                _textService,
                _themeService,
                _appPreferencesService,
                theme => ApplyTheme(applicationResources, theme))
        };

        return new ShellViewModel(_textService, _clockService, pageFactories);
    }

    private void LoadPreferences()
    {
        var snapshot = _appPreferencesService.Load();

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
