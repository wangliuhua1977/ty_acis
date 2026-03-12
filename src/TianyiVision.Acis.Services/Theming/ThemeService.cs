using TianyiVision.Acis.Core.Contracts;
using TianyiVision.Acis.Core.Theming;

namespace TianyiVision.Acis.Services.Theming;

public sealed class ThemeService : IThemeService
{
    private readonly IReadOnlyList<ThemeDefinition> _themes;

    public ThemeService(IThemeCatalogProvider provider)
    {
        _themes = provider.GetThemes();
        ActiveTheme = _themes.First();
    }

    public ThemeDefinition ActiveTheme { get; private set; }

    public IReadOnlyList<ThemeDefinition> GetAvailableThemes() => _themes;

    public void SetTheme(string themeId)
    {
        ActiveTheme = _themes.FirstOrDefault(theme => theme.Id == themeId) ?? ActiveTheme;
    }
}
