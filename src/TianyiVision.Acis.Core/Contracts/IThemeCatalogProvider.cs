using TianyiVision.Acis.Core.Theming;

namespace TianyiVision.Acis.Core.Contracts;

public interface IThemeCatalogProvider
{
    IReadOnlyList<ThemeDefinition> GetThemes();
}
