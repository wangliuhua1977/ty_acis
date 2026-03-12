namespace TianyiVision.Acis.Services.Settings;

public sealed record StoredThemePreference(
    string Id,
    string DisplayName,
    string Description,
    bool IsPreset,
    IReadOnlyDictionary<string, string> Colors,
    string CardBorderStyleKey,
    string MapStyleKey);

public sealed record StoredTerminologyPreference(
    string Id,
    string DisplayName,
    string Description,
    bool IsPreset,
    IReadOnlyDictionary<string, string> TextEntries,
    IReadOnlyDictionary<string, string> Variables);

public sealed record AppPreferencesSnapshot(
    string? ActiveThemeId,
    string? ActiveTerminologyId,
    IReadOnlyList<StoredThemePreference> Themes,
    IReadOnlyList<StoredTerminologyPreference> Terminologies);
