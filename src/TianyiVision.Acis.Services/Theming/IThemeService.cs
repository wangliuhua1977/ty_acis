using TianyiVision.Acis.Core.Theming;

namespace TianyiVision.Acis.Services.Theming;

public interface IThemeService
{
    event EventHandler? ThemeChanged;

    ThemeDefinition ActiveTheme { get; }

    IReadOnlyList<ThemeDefinition> GetAvailableThemes();

    void SetTheme(string themeId);

    void SetTheme(ThemeDefinition theme);
}
