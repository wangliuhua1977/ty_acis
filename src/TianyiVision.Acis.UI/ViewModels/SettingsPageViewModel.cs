using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Core.Theming;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Settings;
using TianyiVision.Acis.Services.Theming;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel : PageViewModelBase
{
    private const string DefaultTerminologyProfileId = "telecom";

    private const string ThemeFieldWindowBackground = "window";
    private const string ThemeFieldSurfaceBackground = "surface";
    private const string ThemeFieldPrimaryAccent = "accentPrimary";
    private const string ThemeFieldSecondaryAccent = "accentSecondary";
    private const string ThemeFieldEmphasis = "emphasis";
    private const string ThemeFieldSuccess = "success";
    private const string ThemeFieldInspection = "inspection";
    private const string ThemeFieldFault = "fault";
    private const string ThemeBorderStyleSubtle = "subtle";
    private const string ThemeBorderStyleStrong = "strong";
    private const string ThemeMapStyleOcean = "ocean";
    private const string ThemeMapStyleNight = "night";
    private const string ThemeMapStyleGlacier = "glacier";

    private readonly Action<ThemeDefinition> _applyThemeToApplication;
    private readonly IAppPreferencesService _appPreferencesService;
    private readonly IDispatchResponsibilitySettingsService _dispatchResponsibilitySettingsService;
    private readonly INotificationSettingsService _notificationSettingsService;
    private readonly Dictionary<string, ThemeEditorFieldState> _themeFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TerminologyFieldState> _terminologyFields = new(StringComparer.Ordinal);
    private readonly ITextService _textService;
    private readonly IThemeService _themeService;
    private readonly AppPreferencesSnapshot _preferencesSnapshot;

    private ObservableCollection<PanelPlaceholderState> _placeholderCards = [];
    private SettingsSectionState? _selectedSection;
    private ThemeSummaryState? _selectedTheme;
    private TerminologySchemeSummaryState? _selectedTerminologyScheme;
    private string _placeholderSectionTitle = string.Empty;
    private string _placeholderSectionDescription = string.Empty;
    private int _customThemeCounter = 1;
    private int _customTerminologyCounter = 1;

    public SettingsPageViewModel(
        ITextService textService,
        IThemeService themeService,
        IAppPreferencesService appPreferencesService,
        IDispatchResponsibilitySettingsService dispatchResponsibilitySettingsService,
        INotificationSettingsService notificationSettingsService,
        Action<ThemeDefinition> applyThemeToApplication)
        : base(
            textService.Resolve(TextTokens.SettingsTitle),
            textService.Resolve(TextTokens.SettingsDescription))
    {
        _textService = textService;
        _themeService = themeService;
        _appPreferencesService = appPreferencesService;
        _dispatchResponsibilitySettingsService = dispatchResponsibilitySettingsService;
        _notificationSettingsService = notificationSettingsService;
        _applyThemeToApplication = applyThemeToApplication;
        _preferencesSnapshot = appPreferencesService.Load();

        SelectSectionCommand = new RelayCommand(parameter =>
        {
            if (parameter is SettingsSectionState section)
            {
                SelectSection(section.Key);
            }
        });

        SelectThemeCommand = new RelayCommand(parameter =>
        {
            if (parameter is ThemeSummaryState theme)
            {
                SelectTheme(theme.Id);
            }
        });
        ApplyThemeCommand = new RelayCommand(_ => ApplySelectedTheme(), _ => SelectedTheme is not null);
        CopyThemeCommand = new RelayCommand(_ => CopySelectedTheme(), _ => SelectedTheme is not null);
        NewCustomThemeCommand = new RelayCommand(_ => CreateCustomThemeFromCurrent());
        SaveThemeCommand = new RelayCommand(_ => SaveThemeChanges(), _ => SelectedTheme is not null);
        SaveThemeAsCommand = new RelayCommand(_ => SaveThemeAsNew(), _ => SelectedTheme is not null);
        RestoreThemeCommand = new RelayCommand(_ => RestoreThemePreset(), _ => SelectedTheme is not null);

        SelectTerminologyCommand = new RelayCommand(parameter =>
        {
            if (parameter is TerminologySchemeSummaryState scheme)
            {
                SelectTerminologyScheme(scheme.Id);
            }
        });
        ApplyTerminologyCommand = new RelayCommand(_ => ApplySelectedTerminology(), _ => SelectedTerminologyScheme is not null);
        CopyTerminologyCommand = new RelayCommand(_ => CopySelectedTerminology(), _ => SelectedTerminologyScheme is not null);
        NewCustomTerminologyCommand = new RelayCommand(_ => CreateCustomTerminologyFromCurrent());
        SaveTerminologyCommand = new RelayCommand(_ => SaveTerminologyChanges(), _ => SelectedTerminologyScheme is not null);
        SaveTerminologyAsCommand = new RelayCommand(_ => SaveTerminologyAsNew(), _ => SelectedTerminologyScheme is not null);
        RestoreTerminologyCommand = new RelayCommand(_ => RestoreDefaultTerminology());

        AppliedState = new SettingsAppliedState(
            textService.Resolve(TextTokens.SettingsAppliedThemeLabel),
            textService.Resolve(TextTokens.SettingsAppliedTerminologyLabel))
        {
            ActiveThemeName = themeService.ActiveTheme.DisplayName,
            ActiveTerminologyName = textService.ActiveProfile.DisplayName,
            StatusText = textService.Resolve(TextTokens.SettingsAppliedStatusHint)
        };

        SectionItems = CreateSections();
        ThemeItems = CreateThemes();
        ThemeEditor = CreateThemeEditor();
        ThemePreview = new ThemePreviewState();
        TerminologyItems = CreateTerminologySchemes();
        TerminologyGroups = CreateTerminologyGroups();
        TerminologyPreview = new TerminologyPreviewState();
        InitializeConfigSelections();

        HookThemeEditor();
        HookTerminologyEditor();

        SelectTheme(themeService.ActiveTheme.Id);
        foreach (var item in ThemeItems)
        {
            item.IsApplied = item.Id == themeService.ActiveTheme.Id;
        }

        SelectTerminologyScheme(textService.ActiveProfile.Id);
        foreach (var item in TerminologyItems)
        {
            item.IsApplied = item.Id == textService.ActiveProfile.Id;
        }

        SelectSection(SettingsSectionKey.ThemeCenter);
    }

    public ObservableCollection<SettingsSectionState> SectionItems { get; }

    public SettingsAppliedState AppliedState { get; }

    public ObservableCollection<PanelPlaceholderState> PlaceholderCards
    {
        get => _placeholderCards;
        private set => SetProperty(ref _placeholderCards, value);
    }

    public ObservableCollection<ThemeSummaryState> ThemeItems { get; }

    public ThemeEditorState ThemeEditor { get; }

    public ThemePreviewState ThemePreview { get; }

    public ObservableCollection<TerminologySchemeSummaryState> TerminologyItems { get; }

    public ObservableCollection<TerminologyGroupState> TerminologyGroups { get; }

    public TerminologyPreviewState TerminologyPreview { get; }

    public ICommand SelectSectionCommand { get; }

    public ICommand SelectThemeCommand { get; }

    public ICommand ApplyThemeCommand { get; }

    public ICommand CopyThemeCommand { get; }

    public ICommand NewCustomThemeCommand { get; }

    public ICommand SaveThemeCommand { get; }

    public ICommand SaveThemeAsCommand { get; }

    public ICommand RestoreThemeCommand { get; }

    public ICommand SelectTerminologyCommand { get; }

    public ICommand ApplyTerminologyCommand { get; }

    public ICommand CopyTerminologyCommand { get; }

    public ICommand NewCustomTerminologyCommand { get; }

    public ICommand SaveTerminologyCommand { get; }

    public ICommand SaveTerminologyAsCommand { get; }

    public ICommand RestoreTerminologyCommand { get; }

    public SettingsSectionState? SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(IsThemeCenterVisible));
                OnPropertyChanged(nameof(IsTerminologyCenterVisible));
                OnPropertyChanged(nameof(IsPlaceholderSectionVisible));
            }
        }
    }

    public ThemeSummaryState? SelectedTheme
    {
        get => _selectedTheme;
        private set => SetProperty(ref _selectedTheme, value);
    }

    public TerminologySchemeSummaryState? SelectedTerminologyScheme
    {
        get => _selectedTerminologyScheme;
        private set => SetProperty(ref _selectedTerminologyScheme, value);
    }

    public string PlaceholderSectionTitle
    {
        get => _placeholderSectionTitle;
        private set => SetProperty(ref _placeholderSectionTitle, value);
    }

    public string PlaceholderSectionDescription
    {
        get => _placeholderSectionDescription;
        private set => SetProperty(ref _placeholderSectionDescription, value);
    }

    public bool IsThemeCenterVisible => SelectedSection?.Key == SettingsSectionKey.ThemeCenter;

    public bool IsTerminologyCenterVisible => SelectedSection?.Key == SettingsSectionKey.TerminologyCenter;

    public bool IsPlaceholderSectionVisible => false;

    public SettingsSectionKey SelectedSectionKey => SelectedSection?.Key ?? SettingsSectionKey.ThemeCenter;

    public void ActivateSection(SettingsSectionKey key)
        => SelectSection(key);
}
