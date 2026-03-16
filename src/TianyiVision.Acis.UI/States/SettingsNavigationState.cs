using System.Windows.Input;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public enum SettingsSectionKey
{
    InspectionScopePlans,
    InspectionAlertStrategy,
    InspectionDispatchStrategy,
    InspectionVideoStrategy,
    InspectionTaskExecution,
    InspectionGroups,
    PointManagement,
    ResponsibilityMapping,
    NotificationChannels,
    VideoProtocolStrategy,
    ThemeCenter,
    TerminologyCenter
}

public sealed class SettingsSectionState : ViewModelBase
{
    private bool _isSelected;

    public SettingsSectionState(
        SettingsSectionKey key,
        string title,
        string description,
        ICommand selectCommand)
    {
        Key = key;
        Title = title;
        Description = description;
        SelectCommand = selectCommand;
    }

    public SettingsSectionKey Key { get; }

    public string Title { get; }

    public string Description { get; }

    public ICommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class SettingsOptionState
{
    public SettingsOptionState(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }
}

public sealed class SettingsAppliedState : ViewModelBase
{
    private string _activeThemeName = string.Empty;
    private string _activeTerminologyName = string.Empty;
    private string _statusText = string.Empty;

    public SettingsAppliedState(string themeLabel, string terminologyLabel)
    {
        ThemeLabel = themeLabel;
        TerminologyLabel = terminologyLabel;
    }

    public string ThemeLabel { get; }

    public string TerminologyLabel { get; }

    public string ActiveThemeName
    {
        get => _activeThemeName;
        set => SetProperty(ref _activeThemeName, value);
    }

    public string ActiveTerminologyName
    {
        get => _activeTerminologyName;
        set => SetProperty(ref _activeTerminologyName, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}
