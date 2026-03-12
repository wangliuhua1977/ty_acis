using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel
{
    public string AppliedStatusTitle => _textService.Resolve(TextTokens.SettingsAppliedStatusTitle);

    public string SectionListTitle => _textService.Resolve(TextTokens.SettingsSectionListTitle);

    public string ThemeListTitle => _textService.Resolve(TextTokens.SettingsThemeListTitle);

    public string ThemeListDescription => _textService.Resolve(TextTokens.SettingsThemeListDescription);

    public string ThemePreviewTitle => _textService.Resolve(TextTokens.SettingsThemePreviewTitle);

    public string ThemePreviewDescription => _textService.Resolve(TextTokens.SettingsThemePreviewDescription);

    public string ThemeActionsTitle => _textService.Resolve(TextTokens.SettingsThemeActionsTitle);

    public string ThemeEditorTitle => _textService.Resolve(TextTokens.SettingsThemeEditorTitle);

    public string ThemeEditorDescription => _textService.Resolve(TextTokens.SettingsThemeEditorDescription);

    public string ThemeApplyText => _textService.Resolve(TextTokens.SettingsThemeActionApply);

    public string ThemeCopyText => _textService.Resolve(TextTokens.SettingsThemeActionCopy);

    public string ThemeNewText => _textService.Resolve(TextTokens.SettingsThemeActionNew);

    public string ThemeSaveText => _textService.Resolve(TextTokens.SettingsThemeActionSave);

    public string ThemeSaveAsText => _textService.Resolve(TextTokens.SettingsThemeActionSaveAs);

    public string ThemeRestoreText => _textService.Resolve(TextTokens.SettingsThemeActionRestorePreset);

    public string ThemeAppliedTag => _textService.Resolve(TextTokens.SettingsThemeAppliedTag);

    public string ThemePreviewTopBarLabel => _textService.Resolve(TextTokens.SettingsThemePreviewTopBarLabel);

    public string ThemePreviewNavigationLabel => _textService.Resolve(TextTokens.SettingsThemePreviewNavigationLabel);

    public string ThemePreviewMapLabel => _textService.Resolve(TextTokens.SettingsThemePreviewMapLabel);

    public string ThemePreviewCardLabel => _textService.Resolve(TextTokens.SettingsThemePreviewCardLabel);

    public string ThemePreviewButtonLabel => _textService.Resolve(TextTokens.SettingsThemePreviewButtonLabel);

    public string ThemePreviewStatusLabel => _textService.Resolve(TextTokens.SettingsThemePreviewStatusLabel);

    public string ThemePreviewCardBorderLabel => _textService.Resolve(TextTokens.SettingsThemePreviewCardBorderLabel);

    public string ThemePreviewMapStyleLabel => _textService.Resolve(TextTokens.SettingsThemePreviewMapStyleLabel);

    public string ThemeCenterFeedback => AppliedState.StatusText;

    public string TerminologyListTitle => _textService.Resolve(TextTokens.SettingsTerminologyListTitle);

    public string TerminologyListDescription => _textService.Resolve(TextTokens.SettingsTerminologyListDescription);

    public string TerminologyPreviewTitle => _textService.Resolve(TextTokens.SettingsTerminologyPreviewTitle);

    public string TerminologyPreviewDescription => _textService.Resolve(TextTokens.SettingsTerminologyPreviewDescription);

    public string TerminologyActionsTitle => _textService.Resolve(TextTokens.SettingsTerminologyActionsTitle);

    public string TerminologyEditorTitle => _textService.Resolve(TextTokens.SettingsTerminologyEditorTitle);

    public string TerminologyEditorDescription => _textService.Resolve(TextTokens.SettingsTerminologyEditorDescription);

    public string TerminologyApplyText => _textService.Resolve(TextTokens.SettingsTerminologyActionApply);

    public string TerminologyCopyText => _textService.Resolve(TextTokens.SettingsTerminologyActionCopy);

    public string TerminologyNewText => _textService.Resolve(TextTokens.SettingsTerminologyActionNew);

    public string TerminologySaveText => _textService.Resolve(TextTokens.SettingsTerminologyActionSave);

    public string TerminologySaveAsText => _textService.Resolve(TextTokens.SettingsTerminologyActionSaveAs);

    public string TerminologyRestoreText => _textService.Resolve(TextTokens.SettingsTerminologyActionRestoreDefault);

    public string TerminologyAppliedTag => _textService.Resolve(TextTokens.SettingsTerminologyAppliedTag);

    public string TerminologyCenterFeedback => AppliedState.StatusText;

    private ObservableCollection<SettingsSectionState> CreateSections()
    {
        return
        [
            new SettingsSectionState(
                SettingsSectionKey.InspectionGroups,
                _textService.Resolve(TextTokens.SettingsSectionInspectionGroupsTitle),
                _textService.Resolve(TextTokens.SettingsSectionInspectionGroupsDescription),
                SelectSectionCommand),
            new SettingsSectionState(
                SettingsSectionKey.PointManagement,
                _textService.Resolve(TextTokens.SettingsSectionPointsTitle),
                _textService.Resolve(TextTokens.SettingsSectionPointsDescription),
                SelectSectionCommand),
            new SettingsSectionState(
                SettingsSectionKey.ResponsibilityMapping,
                _textService.Resolve(TextTokens.SettingsSectionResponsibilityTitle),
                _textService.Resolve(TextTokens.SettingsSectionResponsibilityDescription),
                SelectSectionCommand),
            new SettingsSectionState(
                SettingsSectionKey.VideoProtocolStrategy,
                _textService.Resolve(TextTokens.SettingsSectionVideoStrategyTitle),
                _textService.Resolve(TextTokens.SettingsSectionVideoStrategyDescription),
                SelectSectionCommand),
            new SettingsSectionState(
                SettingsSectionKey.ThemeCenter,
                _textService.Resolve(TextTokens.SettingsSectionThemeCenterTitle),
                _textService.Resolve(TextTokens.SettingsSectionThemeCenterDescription),
                SelectSectionCommand),
            new SettingsSectionState(
                SettingsSectionKey.TerminologyCenter,
                _textService.Resolve(TextTokens.SettingsSectionTerminologyCenterTitle),
                _textService.Resolve(TextTokens.SettingsSectionTerminologyCenterDescription),
                SelectSectionCommand)
        ];
    }
}
