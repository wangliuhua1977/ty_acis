namespace TianyiVision.Acis.Services.Settings;

internal sealed record AppearanceSettingsDocument(
    string? ActiveThemeId,
    string? ActiveTerminologyId);

internal sealed record ThemeCatalogDocument(
    IReadOnlyList<StoredThemePreference> Themes);

internal sealed record TerminologyCatalogDocument(
    IReadOnlyList<StoredTerminologyPreference> Terminologies);
