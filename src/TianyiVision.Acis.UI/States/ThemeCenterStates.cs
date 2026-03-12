using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Theming;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class ThemeSummaryState : ViewModelBase
{
    private bool _isSelected;
    private bool _isApplied;

    public ThemeSummaryState(
        string id,
        string displayName,
        string description,
        string kindLabel,
        bool isPreset,
        ThemeDefinition savedDefinition,
        ThemeDefinition? presetDefinition,
        string cardBorderStyleKey,
        string presetCardBorderStyleKey,
        string mapStyleKey,
        string presetMapStyleKey)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        KindLabel = kindLabel;
        IsPreset = isPreset;
        SavedDefinition = savedDefinition;
        PresetDefinition = presetDefinition;
        CardBorderStyleKey = cardBorderStyleKey;
        PresetCardBorderStyleKey = presetCardBorderStyleKey;
        MapStyleKey = mapStyleKey;
        PresetMapStyleKey = presetMapStyleKey;
    }

    public string Id { get; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public string KindLabel { get; set; }

    public bool IsPreset { get; }

    public ThemeDefinition SavedDefinition { get; set; }

    public ThemeDefinition? PresetDefinition { get; }

    public string CardBorderStyleKey { get; set; }

    public string PresetCardBorderStyleKey { get; }

    public string MapStyleKey { get; set; }

    public string PresetMapStyleKey { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsApplied
    {
        get => _isApplied;
        set => SetProperty(ref _isApplied, value);
    }
}

public sealed class ThemeEditorFieldState : ViewModelBase
{
    private string _value;

    public ThemeEditorFieldState(string key, string label, string description, string value)
    {
        Key = key;
        Label = label;
        Description = description;
        _value = value;
    }

    public string Key { get; }

    public string Label { get; }

    public string Description { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

public sealed class ThemeEditorState : ViewModelBase
{
    private SettingsOptionState? _selectedCardBorderOption;
    private SettingsOptionState? _selectedMapStyleOption;

    public ThemeEditorState(
        ObservableCollection<ThemeEditorFieldState> fields,
        ObservableCollection<SettingsOptionState> cardBorderOptions,
        ObservableCollection<SettingsOptionState> mapStyleOptions)
    {
        Fields = fields;
        CardBorderOptions = cardBorderOptions;
        MapStyleOptions = mapStyleOptions;
        _selectedCardBorderOption = cardBorderOptions.FirstOrDefault();
        _selectedMapStyleOption = mapStyleOptions.FirstOrDefault();
    }

    public ObservableCollection<ThemeEditorFieldState> Fields { get; }

    public ObservableCollection<SettingsOptionState> CardBorderOptions { get; }

    public ObservableCollection<SettingsOptionState> MapStyleOptions { get; }

    public SettingsOptionState? SelectedCardBorderOption
    {
        get => _selectedCardBorderOption;
        set => SetProperty(ref _selectedCardBorderOption, value);
    }

    public SettingsOptionState? SelectedMapStyleOption
    {
        get => _selectedMapStyleOption;
        set => SetProperty(ref _selectedMapStyleOption, value);
    }
}

public sealed class ThemePreviewChipState
{
    public ThemePreviewChipState(string label, string colorValue)
    {
        Label = label;
        ColorValue = colorValue;
    }

    public string Label { get; }

    public string ColorValue { get; }
}

public sealed class ThemePreviewState : ViewModelBase
{
    private string _themeName = string.Empty;
    private string _topBarColor = "#000000";
    private string _navigationColor = "#000000";
    private string _mapStageColor = "#000000";
    private string _cardColor = "#000000";
    private string _buttonColor = "#000000";
    private string _borderColor = "#000000";
    private string _cardBorderStyleLabel = string.Empty;
    private string _mapStyleLabel = string.Empty;

    public ObservableCollection<ThemePreviewChipState> StatusChips { get; } = [];

    public string ThemeName
    {
        get => _themeName;
        set => SetProperty(ref _themeName, value);
    }

    public string TopBarColor
    {
        get => _topBarColor;
        set => SetProperty(ref _topBarColor, value);
    }

    public string NavigationColor
    {
        get => _navigationColor;
        set => SetProperty(ref _navigationColor, value);
    }

    public string MapStageColor
    {
        get => _mapStageColor;
        set => SetProperty(ref _mapStageColor, value);
    }

    public string CardColor
    {
        get => _cardColor;
        set => SetProperty(ref _cardColor, value);
    }

    public string ButtonColor
    {
        get => _buttonColor;
        set => SetProperty(ref _buttonColor, value);
    }

    public string BorderColor
    {
        get => _borderColor;
        set => SetProperty(ref _borderColor, value);
    }

    public string CardBorderStyleLabel
    {
        get => _cardBorderStyleLabel;
        set => SetProperty(ref _cardBorderStyleLabel, value);
    }

    public string MapStyleLabel
    {
        get => _mapStyleLabel;
        set => SetProperty(ref _mapStyleLabel, value);
    }
}
