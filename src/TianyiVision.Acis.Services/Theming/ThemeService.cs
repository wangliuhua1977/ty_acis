using TianyiVision.Acis.Core.Contracts;
using TianyiVision.Acis.Core.Theming;

namespace TianyiVision.Acis.Services.Theming;

public sealed class ThemeService : IThemeService
{
    private readonly List<ThemeDefinition> _themes;

    public event EventHandler? ThemeChanged;

    public ThemeService(IThemeCatalogProvider provider)
    {
        _themes = provider.GetThemes().ToList();
        ActiveTheme = _themes.First();
    }

    public ThemeDefinition ActiveTheme { get; private set; }

    public IReadOnlyList<ThemeDefinition> GetAvailableThemes() => _themes;

    public void SetTheme(string themeId)
    {
        var nextTheme = _themes.FirstOrDefault(theme => theme.Id == themeId);
        if (nextTheme is null || nextTheme.Id == ActiveTheme.Id)
        {
            return;
        }

        ActiveTheme = nextTheme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetTheme(ThemeDefinition theme)
    {
        var existingIndex = _themes.FindIndex(item => item.Id == theme.Id);
        if (existingIndex >= 0)
        {
            _themes[existingIndex] = theme;
        }
        else
        {
            _themes.Add(theme);
        }

        var hasChanged = ActiveTheme.Id != theme.Id
            || !ReferenceEquals(ActiveTheme, theme);
        ActiveTheme = theme;

        if (hasChanged)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
