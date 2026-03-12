using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Theming;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class SettingsPageViewModel : PageViewModelBase
{
    public SettingsPageViewModel(ITextService textService, IThemeService themeService)
        : base(
            textService.Resolve(TextTokens.SettingsTitle),
            textService.Resolve(TextTokens.SettingsDescription))
    {
        Cards =
        [
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.SettingsThemeCenterTitle),
                textService.Resolve(TextTokens.SettingsThemeCenterDescription),
                "主题令牌已预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.SettingsTerminologyCenterTitle),
                textService.Resolve(TextTokens.SettingsTerminologyCenterDescription),
                "术语变量已预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.SettingsConfigTitle),
                textService.Resolve(TextTokens.SettingsConfigDescription),
                "本地配置读取"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.SettingsStructureTitle),
                textService.Resolve(TextTokens.SettingsStructureDescription),
                "五层结构就绪")
        ];

        ActiveThemeLabel = textService.Resolve(TextTokens.SettingsActiveTheme);
        ActiveThemeName = themeService.ActiveTheme.DisplayName;
        ActiveTerminologyLabel = textService.Resolve(TextTokens.SettingsActiveTerminology);
        ActiveTerminologyName = textService.ActiveProfile.DisplayName;
        AvailableThemesLabel = textService.Resolve(TextTokens.SettingsAvailableThemes);
        AvailableTerminologiesLabel = textService.Resolve(TextTokens.SettingsAvailableTerminologies);

        ThemeNames = new ObservableCollection<string>(themeService.GetAvailableThemes().Select(theme => theme.DisplayName));
        TerminologyNames = new ObservableCollection<string>(textService.GetAvailableProfiles().Select(profile => profile.DisplayName));
    }

    public ObservableCollection<PanelPlaceholderState> Cards { get; }

    public string ActiveThemeLabel { get; }

    public string ActiveThemeName { get; }

    public string ActiveTerminologyLabel { get; }

    public string ActiveTerminologyName { get; }

    public string AvailableThemesLabel { get; }

    public ObservableCollection<string> ThemeNames { get; }

    public string AvailableTerminologiesLabel { get; }

    public ObservableCollection<string> TerminologyNames { get; }
}
