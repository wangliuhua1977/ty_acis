using TianyiVision.Acis.Services.Settings;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel
{
    private void PersistPreferences(string? activeThemeId = null, string? activeTerminologyId = null)
    {
        var snapshot = new AppPreferencesSnapshot(
            activeThemeId ?? _themeService.ActiveTheme.Id,
            activeTerminologyId ?? _textService.ActiveProfile.Id,
            ThemeItems.Select(item => new StoredThemePreference(
                item.Id,
                item.DisplayName,
                item.Description,
                item.IsPreset,
                new Dictionary<string, string>(item.SavedDefinition.Colors, StringComparer.Ordinal),
                item.CardBorderStyleKey,
                item.MapStyleKey)).ToArray(),
            TerminologyItems.Select(item => new StoredTerminologyPreference(
                item.Id,
                item.DisplayName,
                item.Description,
                item.IsPreset,
                new Dictionary<string, string>(item.SavedProfile.TextEntries, StringComparer.Ordinal),
                new Dictionary<string, string>(item.SavedProfile.Variables, StringComparer.Ordinal))).ToArray());

        _appPreferencesService.Save(snapshot);
    }

    private StoredThemePreference? FindThemePreference(string id)
        => _preferencesSnapshot.Themes.FirstOrDefault(item => item.Id == id);

    private StoredTerminologyPreference? FindTerminologyPreference(string id)
        => _preferencesSnapshot.Terminologies.FirstOrDefault(item => item.Id == id);
}
