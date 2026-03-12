using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Core.Theming;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel
{
    private ObservableCollection<ThemeSummaryState> CreateThemes()
    {
        var items = new ObservableCollection<ThemeSummaryState>();

        foreach (var theme in _themeService.GetAvailableThemes())
        {
            var borderKey = theme.Id switch
            {
                "polar-night-fusion" => ThemeBorderStyleStrong,
                _ => ThemeBorderStyleSubtle
            };

            var mapStyleKey = theme.Id switch
            {
                "polar-night-fusion" => ThemeMapStyleNight,
                "glacier-silver-gold" => ThemeMapStyleGlacier,
                _ => ThemeMapStyleOcean
            };

            items.Add(new ThemeSummaryState(
                theme.Id,
                theme.DisplayName,
                theme.Description,
                _textService.Resolve(TextTokens.SettingsThemePresetTag),
                true,
                CloneThemeDefinition(theme, theme.Id, theme.DisplayName, theme.Description),
                CloneThemeDefinition(theme, theme.Id, theme.DisplayName, theme.Description),
                borderKey,
                borderKey,
                mapStyleKey,
                mapStyleKey));
        }

        _customThemeCounter = items.Count + 1;
        return items;
    }

    private ThemeEditorState CreateThemeEditor()
    {
        var active = _themeService.ActiveTheme;
        var fields = new ObservableCollection<ThemeEditorFieldState>
        {
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldWindowBackground, _textService.Resolve(TextTokens.SettingsThemeFieldWindowBackground), _textService.Resolve(TextTokens.SettingsThemeFieldWindowBackgroundDescription), active.GetColor(ThemeColorTokens.WindowBackground))),
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldSurfaceBackground, _textService.Resolve(TextTokens.SettingsThemeFieldSurfaceBackground), _textService.Resolve(TextTokens.SettingsThemeFieldSurfaceBackgroundDescription), active.GetColor(ThemeColorTokens.SurfacePrimary))),
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldPrimaryAccent, _textService.Resolve(TextTokens.SettingsThemeFieldPrimaryAccent), _textService.Resolve(TextTokens.SettingsThemeFieldPrimaryAccentDescription), active.GetColor(ThemeColorTokens.AccentPrimary))),
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldSecondaryAccent, _textService.Resolve(TextTokens.SettingsThemeFieldSecondaryAccent), _textService.Resolve(TextTokens.SettingsThemeFieldSecondaryAccentDescription), active.GetColor(ThemeColorTokens.AccentSecondary))),
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldEmphasis, _textService.Resolve(TextTokens.SettingsThemeFieldEmphasis), _textService.Resolve(TextTokens.SettingsThemeFieldEmphasisDescription), active.GetColor(ThemeColorTokens.BorderStrong))),
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldSuccess, _textService.Resolve(TextTokens.SettingsThemeFieldSuccess), _textService.Resolve(TextTokens.SettingsThemeFieldSuccessDescription), active.GetColor(ThemeColorTokens.Success))),
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldInspection, _textService.Resolve(TextTokens.SettingsThemeFieldInspection), _textService.Resolve(TextTokens.SettingsThemeFieldInspectionDescription), active.GetColor(ThemeColorTokens.InspectionActive))),
            RegisterThemeField(new ThemeEditorFieldState(ThemeFieldFault, _textService.Resolve(TextTokens.SettingsThemeFieldFault), _textService.Resolve(TextTokens.SettingsThemeFieldFaultDescription), active.GetColor(ThemeColorTokens.FaultBlink)))
        };

        return new ThemeEditorState(
            fields,
            new ObservableCollection<SettingsOptionState>
            {
                new(ThemeBorderStyleSubtle, _textService.Resolve(TextTokens.SettingsThemeBorderSubtle)),
                new(ThemeBorderStyleStrong, _textService.Resolve(TextTokens.SettingsThemeBorderStrong))
            },
            new ObservableCollection<SettingsOptionState>
            {
                new(ThemeMapStyleOcean, _textService.Resolve(TextTokens.SettingsThemeMapOcean)),
                new(ThemeMapStyleNight, _textService.Resolve(TextTokens.SettingsThemeMapNight)),
                new(ThemeMapStyleGlacier, _textService.Resolve(TextTokens.SettingsThemeMapGlacier))
            });
    }

    private ThemeEditorFieldState RegisterThemeField(ThemeEditorFieldState field)
    {
        _themeFields[field.Key] = field;
        return field;
    }

    private void HookThemeEditor()
    {
        foreach (var field in ThemeEditor.Fields)
        {
            field.PropertyChanged += HandleThemeFieldChanged;
        }

        ThemeEditor.PropertyChanged += HandleThemeEditorChanged;
    }

    private void HandleThemeFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ThemeEditorFieldState.Value))
        {
            UpdateThemePreview();
        }
    }

    private void HandleThemeEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ThemeEditorState.SelectedCardBorderOption)
            || e.PropertyName == nameof(ThemeEditorState.SelectedMapStyleOption))
        {
            UpdateThemePreview();
        }
    }

    private void SelectSection(SettingsSectionKey key)
    {
        SelectedSection = SectionItems.First(item => item.Key == key);

        foreach (var item in SectionItems)
        {
            item.IsSelected = item.Key == key;
        }

        if (IsPlaceholderSectionVisible)
        {
            PlaceholderSectionTitle = SelectedSection!.Title;
            PlaceholderSectionDescription = SelectedSection.Description;
            PlaceholderCards =
            [
                new PanelPlaceholderState(
                    _textService.Resolve(TextTokens.SettingsPlaceholderScopeTitle),
                    _textService.Resolve(TextTokens.SettingsPlaceholderScopeDescription),
                    _textService.Resolve(TextTokens.SettingsPlaceholderScopeAccent)),
                new PanelPlaceholderState(
                    _textService.Resolve(TextTokens.SettingsPlaceholderNextTitle),
                    _textService.Resolve(TextTokens.SettingsPlaceholderNextDescription),
                    _textService.Resolve(TextTokens.SettingsPlaceholderNextAccent))
            ];
        }
    }

    private void SelectTheme(string themeId)
    {
        SelectedTheme = ThemeItems.FirstOrDefault(item => item.Id == themeId) ?? ThemeItems.First();

        foreach (var item in ThemeItems)
        {
            item.IsSelected = item == SelectedTheme;
        }

        LoadThemeEditor(SelectedTheme);
        UpdateThemePreview();
    }

    private void LoadThemeEditor(ThemeSummaryState theme)
    {
        var definition = theme.SavedDefinition;
        _themeFields[ThemeFieldWindowBackground].Value = definition.GetColor(ThemeColorTokens.WindowBackground);
        _themeFields[ThemeFieldSurfaceBackground].Value = definition.GetColor(ThemeColorTokens.SurfacePrimary);
        _themeFields[ThemeFieldPrimaryAccent].Value = definition.GetColor(ThemeColorTokens.AccentPrimary);
        _themeFields[ThemeFieldSecondaryAccent].Value = definition.GetColor(ThemeColorTokens.AccentSecondary);
        _themeFields[ThemeFieldEmphasis].Value = definition.GetColor(ThemeColorTokens.BorderStrong);
        _themeFields[ThemeFieldSuccess].Value = definition.GetColor(ThemeColorTokens.Success);
        _themeFields[ThemeFieldInspection].Value = definition.GetColor(ThemeColorTokens.InspectionActive);
        _themeFields[ThemeFieldFault].Value = definition.GetColor(ThemeColorTokens.FaultBlink);
        ThemeEditor.SelectedCardBorderOption = ThemeEditor.CardBorderOptions.FirstOrDefault(option => option.Key == theme.CardBorderStyleKey);
        ThemeEditor.SelectedMapStyleOption = ThemeEditor.MapStyleOptions.FirstOrDefault(option => option.Key == theme.MapStyleKey);
    }

    private void UpdateThemePreview()
    {
        if (SelectedTheme is null)
        {
            return;
        }

        var fallback = SelectedTheme.SavedDefinition;
        ThemePreview.ThemeName = SelectedTheme.DisplayName;
        ThemePreview.TopBarColor = ResolveThemeFieldValue(ThemeFieldWindowBackground, fallback.GetColor(ThemeColorTokens.WindowBackground));
        ThemePreview.NavigationColor = ResolveThemeFieldValue(ThemeFieldWindowBackground, fallback.GetColor(ThemeColorTokens.SidebarBackground));
        ThemePreview.MapStageColor = ResolveThemeFieldValue(ThemeFieldSurfaceBackground, fallback.GetColor(ThemeColorTokens.WorkbenchBackground));
        ThemePreview.CardColor = ResolveThemeFieldValue(ThemeFieldSurfaceBackground, fallback.GetColor(ThemeColorTokens.SurfacePrimary));
        ThemePreview.ButtonColor = ResolveThemeFieldValue(ThemeFieldPrimaryAccent, fallback.GetColor(ThemeColorTokens.AccentPrimary));
        ThemePreview.BorderColor = ResolveThemeFieldValue(ThemeFieldEmphasis, fallback.GetColor(ThemeColorTokens.BorderStrong));
        ThemePreview.CardBorderStyleLabel = ThemeEditor.SelectedCardBorderOption?.Label ?? string.Empty;
        ThemePreview.MapStyleLabel = ThemeEditor.SelectedMapStyleOption?.Label ?? string.Empty;

        ThemePreview.StatusChips.Clear();
        ThemePreview.StatusChips.Add(new ThemePreviewChipState(_textService.Resolve(TextTokens.SettingsThemePreviewStatusNormal), ResolveThemeFieldValue(ThemeFieldSuccess, fallback.GetColor(ThemeColorTokens.Success))));
        ThemePreview.StatusChips.Add(new ThemePreviewChipState(_textService.Resolve(TextTokens.SettingsThemePreviewStatusInspection), ResolveThemeFieldValue(ThemeFieldInspection, fallback.GetColor(ThemeColorTokens.InspectionActive))));
        ThemePreview.StatusChips.Add(new ThemePreviewChipState(_textService.Resolve(TextTokens.SettingsThemePreviewStatusFault), ResolveThemeFieldValue(ThemeFieldFault, fallback.GetColor(ThemeColorTokens.FaultBlink))));
    }

    private void ApplySelectedTheme()
    {
        if (SelectedTheme is null)
        {
            return;
        }

        var appliedDefinition = BuildThemeDefinition(SelectedTheme);
        SelectedTheme.SavedDefinition = appliedDefinition;
        SelectedTheme.CardBorderStyleKey = ThemeEditor.SelectedCardBorderOption?.Key ?? ThemeBorderStyleSubtle;
        SelectedTheme.MapStyleKey = ThemeEditor.SelectedMapStyleOption?.Key ?? ThemeMapStyleOcean;

        _themeService.SetTheme(appliedDefinition);
        _applyThemeToApplication(appliedDefinition);

        foreach (var item in ThemeItems)
        {
            item.IsApplied = item == SelectedTheme;
        }

        AppliedState.ActiveThemeName = SelectedTheme.DisplayName;
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsThemeAppliedFeedbackPattern), SelectedTheme.DisplayName);
    }

    private void CopySelectedTheme()
    {
        if (SelectedTheme is null)
        {
            return;
        }

        var copiedTheme = CreateThemeClone(
            $"theme-custom-{_customThemeCounter}",
            string.Format(_textService.Resolve(TextTokens.SettingsThemeCopyNamePattern), _customThemeCounter),
            _textService.Resolve(TextTokens.SettingsThemeCopyDescription),
            BuildThemeDefinition(SelectedTheme));

        ThemeItems.Add(copiedTheme);
        _customThemeCounter++;
        SelectTheme(copiedTheme.Id);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsThemeCopyFeedbackPattern), copiedTheme.DisplayName);
    }

    private void CreateCustomThemeFromCurrent()
    {
        var themeName = string.Format(_textService.Resolve(TextTokens.SettingsThemeNewNamePattern), _customThemeCounter);
        var customTheme = CreateThemeClone(
            $"theme-custom-{_customThemeCounter}",
            themeName,
            _textService.Resolve(TextTokens.SettingsThemeNewDescription),
            CloneThemeDefinition(SelectedTheme?.SavedDefinition ?? _themeService.ActiveTheme, $"theme-custom-{_customThemeCounter}", themeName, _textService.Resolve(TextTokens.SettingsThemeNewDescription)));

        ThemeItems.Add(customTheme);
        _customThemeCounter++;
        SelectTheme(customTheme.Id);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsThemeNewFeedbackPattern), customTheme.DisplayName);
    }

    private void SaveThemeChanges()
    {
        if (SelectedTheme is null)
        {
            return;
        }

        SelectedTheme.SavedDefinition = BuildThemeDefinition(SelectedTheme);
        SelectedTheme.CardBorderStyleKey = ThemeEditor.SelectedCardBorderOption?.Key ?? ThemeBorderStyleSubtle;
        SelectedTheme.MapStyleKey = ThemeEditor.SelectedMapStyleOption?.Key ?? ThemeMapStyleOcean;
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsThemeSaveFeedbackPattern), SelectedTheme.DisplayName);
    }

    private void SaveThemeAsNew()
    {
        if (SelectedTheme is null)
        {
            return;
        }

        var themeName = string.Format(_textService.Resolve(TextTokens.SettingsThemeSaveAsNamePattern), _customThemeCounter);
        var newTheme = CreateThemeClone(
            $"theme-custom-{_customThemeCounter}",
            themeName,
            _textService.Resolve(TextTokens.SettingsThemeSaveAsDescription),
            BuildThemeDefinition(SelectedTheme));

        ThemeItems.Add(newTheme);
        _customThemeCounter++;
        SelectTheme(newTheme.Id);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsThemeSaveAsFeedbackPattern), newTheme.DisplayName);
    }

    private void RestoreThemePreset()
    {
        if (SelectedTheme is null)
        {
            return;
        }

        if (SelectedTheme.PresetDefinition is not null)
        {
            SelectedTheme.SavedDefinition = CloneThemeDefinition(SelectedTheme.PresetDefinition, SelectedTheme.Id, SelectedTheme.DisplayName, SelectedTheme.Description);
            SelectedTheme.CardBorderStyleKey = SelectedTheme.PresetCardBorderStyleKey;
            SelectedTheme.MapStyleKey = SelectedTheme.PresetMapStyleKey;
        }

        LoadThemeEditor(SelectedTheme);
        UpdateThemePreview();
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsThemeRestoreFeedbackPattern), SelectedTheme.DisplayName);
    }

    private ThemeSummaryState CreateThemeClone(string id, string name, string description, ThemeDefinition definition)
    {
        return new ThemeSummaryState(
            id,
            name,
            description,
            _textService.Resolve(TextTokens.SettingsThemeCustomTag),
            false,
            CloneThemeDefinition(definition, id, name, description),
            null,
            ThemeEditor.SelectedCardBorderOption?.Key ?? ThemeBorderStyleSubtle,
            ThemeBorderStyleSubtle,
            ThemeEditor.SelectedMapStyleOption?.Key ?? ThemeMapStyleOcean,
            ThemeMapStyleOcean);
    }

    private ThemeDefinition BuildThemeDefinition(ThemeSummaryState theme)
    {
        var colors = new Dictionary<string, string>(theme.SavedDefinition.Colors, StringComparer.Ordinal)
        {
            [ThemeColorTokens.WindowBackground] = ResolveThemeFieldValue(ThemeFieldWindowBackground, theme.SavedDefinition.GetColor(ThemeColorTokens.WindowBackground)),
            [ThemeColorTokens.SidebarBackground] = ResolveThemeFieldValue(ThemeFieldWindowBackground, theme.SavedDefinition.GetColor(ThemeColorTokens.SidebarBackground)),
            [ThemeColorTokens.ShellGradientStart] = ResolveThemeFieldValue(ThemeFieldWindowBackground, theme.SavedDefinition.GetColor(ThemeColorTokens.ShellGradientStart)),
            [ThemeColorTokens.SurfacePrimary] = ResolveThemeFieldValue(ThemeFieldSurfaceBackground, theme.SavedDefinition.GetColor(ThemeColorTokens.SurfacePrimary)),
            [ThemeColorTokens.SurfaceSecondary] = ResolveThemeFieldValue(ThemeFieldSurfaceBackground, theme.SavedDefinition.GetColor(ThemeColorTokens.SurfaceSecondary)),
            [ThemeColorTokens.WorkbenchBackground] = ResolveThemeFieldValue(ThemeFieldSurfaceBackground, theme.SavedDefinition.GetColor(ThemeColorTokens.WorkbenchBackground)),
            [ThemeColorTokens.AccentPrimary] = ResolveThemeFieldValue(ThemeFieldPrimaryAccent, theme.SavedDefinition.GetColor(ThemeColorTokens.AccentPrimary)),
            [ThemeColorTokens.AccentSecondary] = ResolveThemeFieldValue(ThemeFieldSecondaryAccent, theme.SavedDefinition.GetColor(ThemeColorTokens.AccentSecondary)),
            [ThemeColorTokens.BorderStrong] = ResolveThemeFieldValue(ThemeFieldEmphasis, theme.SavedDefinition.GetColor(ThemeColorTokens.BorderStrong)),
            [ThemeColorTokens.BorderPrimary] = ResolveThemeFieldValue(ThemeFieldEmphasis, theme.SavedDefinition.GetColor(ThemeColorTokens.BorderPrimary)),
            [ThemeColorTokens.Success] = ResolveThemeFieldValue(ThemeFieldSuccess, theme.SavedDefinition.GetColor(ThemeColorTokens.Success)),
            [ThemeColorTokens.InspectionActive] = ResolveThemeFieldValue(ThemeFieldInspection, theme.SavedDefinition.GetColor(ThemeColorTokens.InspectionActive)),
            [ThemeColorTokens.Danger] = ResolveThemeFieldValue(ThemeFieldFault, theme.SavedDefinition.GetColor(ThemeColorTokens.Danger)),
            [ThemeColorTokens.FaultBlink] = ResolveThemeFieldValue(ThemeFieldFault, theme.SavedDefinition.GetColor(ThemeColorTokens.FaultBlink))
        };

        return new ThemeDefinition(theme.Id, theme.DisplayName, theme.Description, colors);
    }

    private string ResolveThemeFieldValue(string fieldKey, string fallback)
    {
        var candidate = _themeFields[fieldKey].Value;
        return IsValidColor(candidate) ? candidate : fallback;
    }

    private static bool IsValidColor(string value)
    {
        try
        {
            _ = ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ThemeDefinition CloneThemeDefinition(ThemeDefinition source, string id, string name, string description)
    {
        return new ThemeDefinition(id, name, description, new Dictionary<string, string>(source.Colors, StringComparer.Ordinal));
    }
}
