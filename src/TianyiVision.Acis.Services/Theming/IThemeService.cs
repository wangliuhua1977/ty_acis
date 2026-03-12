using TianyiVision.Acis.Core.Theming;

namespace TianyiVision.Acis.Services.Theming;

public interface IThemeService
{
    ThemeDefinition ActiveTheme { get; }

    IReadOnlyList<ThemeDefinition> GetAvailableThemes();

    void SetTheme(string themeId);
}
