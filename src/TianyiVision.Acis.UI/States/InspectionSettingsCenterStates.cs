using System.Collections.ObjectModel;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionScopePlanState : ViewModelBase
{
    private bool _isSelected;

    public InspectionScopePlanState(string id, string name, string summary, bool isEnabled, bool isDefault)
    {
        Id = id;
        Name = name;
        Summary = summary;
        IsEnabled = isEnabled;
        IsDefault = isDefault;
    }

    public string Id { get; }

    public string Name { get; }

    public string Summary { get; }

    public bool IsEnabled { get; }

    public bool IsDefault { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class InspectionScopePlanEditorState : ViewModelBase
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _includedRegionsText = string.Empty;
    private string _includedDirectoriesText = string.Empty;
    private string _includedPointsText = string.Empty;
    private string _excludedPointsText = string.Empty;
    private string _focusPointsText = string.Empty;
    private bool _isEnabled = true;
    private bool _isDefault;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string IncludedRegionsText
    {
        get => _includedRegionsText;
        set => SetProperty(ref _includedRegionsText, value);
    }

    public string IncludedDirectoriesText
    {
        get => _includedDirectoriesText;
        set => SetProperty(ref _includedDirectoriesText, value);
    }

    public string IncludedPointsText
    {
        get => _includedPointsText;
        set => SetProperty(ref _includedPointsText, value);
    }

    public string ExcludedPointsText
    {
        get => _excludedPointsText;
        set => SetProperty(ref _excludedPointsText, value);
    }

    public string FocusPointsText
    {
        get => _focusPointsText;
        set => SetProperty(ref _focusPointsText, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }
}

public sealed class InspectionAlertTypeSelectionState : ViewModelBase
{
    private bool _isSelected;

    public InspectionAlertTypeSelectionState(string id, string label, string description, bool isSelected)
    {
        Id = id;
        Label = label;
        Description = description;
        _isSelected = isSelected;
    }

    public string Id { get; }

    public string Label { get; }

    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class InspectionPolicyToggleState : ViewModelBase
{
    private bool _isEnabled;

    public InspectionPolicyToggleState(string key, string label, string description, bool isEnabled)
    {
        Key = key;
        Label = label;
        Description = description;
        _isEnabled = isEnabled;
    }

    public string Key { get; }

    public string Label { get; }

    public string Description { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

public sealed class InspectionPointPolicyOverrideState : ViewModelBase
{
    private bool _isSelected;

    public InspectionPointPolicyOverrideState(string id, string pointId, string pointName, string summary)
    {
        Id = id;
        PointId = pointId;
        PointName = pointName;
        Summary = summary;
    }

    public string Id { get; }

    public string PointId { get; }

    public string PointName { get; }

    public string Summary { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class InspectionPointPolicyOverrideEditorState : ViewModelBase
{
    private string _pointId = string.Empty;
    private string _pointName = string.Empty;
    private bool _useGlobalAlertTypes = true;

    public InspectionPointPolicyOverrideEditorState(
        ObservableCollection<InspectionAlertTypeSelectionState> alertTypes,
        ObservableCollection<InspectionPolicyToggleState> policyToggles)
    {
        AlertTypes = alertTypes;
        PolicyToggles = policyToggles;
    }

    public ObservableCollection<InspectionAlertTypeSelectionState> AlertTypes { get; }

    public ObservableCollection<InspectionPolicyToggleState> PolicyToggles { get; }

    public string PointId
    {
        get => _pointId;
        set => SetProperty(ref _pointId, value);
    }

    public string PointName
    {
        get => _pointName;
        set => SetProperty(ref _pointName, value);
    }

    public bool UseGlobalAlertTypes
    {
        get => _useGlobalAlertTypes;
        set => SetProperty(ref _useGlobalAlertTypes, value);
    }
}

public sealed class InspectionDispatchOverrideState : ViewModelBase
{
    private bool _isSelected;

    public InspectionDispatchOverrideState(string id, string pointId, string pointName, string modeLabel)
    {
        Id = id;
        PointId = pointId;
        PointName = pointName;
        ModeLabel = modeLabel;
    }

    public string Id { get; }

    public string PointId { get; }

    public string PointName { get; }

    public string ModeLabel { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class InspectionDispatchOverrideEditorState : ViewModelBase
{
    private string _pointId = string.Empty;
    private string _pointName = string.Empty;
    private SettingsOptionState? _selectedMode;

    public InspectionDispatchOverrideEditorState(ObservableCollection<SettingsOptionState> modeOptions)
    {
        ModeOptions = modeOptions;
        _selectedMode = modeOptions.FirstOrDefault();
    }

    public ObservableCollection<SettingsOptionState> ModeOptions { get; }

    public string PointId
    {
        get => _pointId;
        set => SetProperty(ref _pointId, value);
    }

    public string PointName
    {
        get => _pointName;
        set => SetProperty(ref _pointName, value);
    }

    public SettingsOptionState? SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }
}

public sealed class InspectionVideoStrategyEditorState : ViewModelBase
{
    private int _playbackTimeoutSeconds;
    private int _screenshotCount;
    private int _screenshotIntervalSeconds;
    private int _playbackFailureRetryCount;
    private int _reinspectionIntervalMinutes;
    private int _evidenceRetentionDays;
    private bool _allowManualSupplementScreenshot;
    private bool _enableProtocolFallbackRetry;
    private SettingsOptionState? _selectedEvidenceRetentionMode;

    public InspectionVideoStrategyEditorState(ObservableCollection<SettingsOptionState> evidenceRetentionModes)
    {
        EvidenceRetentionModes = evidenceRetentionModes;
        _selectedEvidenceRetentionMode = evidenceRetentionModes.FirstOrDefault();
    }

    public ObservableCollection<SettingsOptionState> EvidenceRetentionModes { get; }

    public int PlaybackTimeoutSeconds
    {
        get => _playbackTimeoutSeconds;
        set => SetProperty(ref _playbackTimeoutSeconds, value);
    }

    public int ScreenshotCount
    {
        get => _screenshotCount;
        set => SetProperty(ref _screenshotCount, value);
    }

    public int ScreenshotIntervalSeconds
    {
        get => _screenshotIntervalSeconds;
        set => SetProperty(ref _screenshotIntervalSeconds, value);
    }

    public int PlaybackFailureRetryCount
    {
        get => _playbackFailureRetryCount;
        set => SetProperty(ref _playbackFailureRetryCount, value);
    }

    public int ReinspectionIntervalMinutes
    {
        get => _reinspectionIntervalMinutes;
        set => SetProperty(ref _reinspectionIntervalMinutes, value);
    }

    public int EvidenceRetentionDays
    {
        get => _evidenceRetentionDays;
        set => SetProperty(ref _evidenceRetentionDays, value);
    }

    public bool AllowManualSupplementScreenshot
    {
        get => _allowManualSupplementScreenshot;
        set => SetProperty(ref _allowManualSupplementScreenshot, value);
    }

    public bool EnableProtocolFallbackRetry
    {
        get => _enableProtocolFallbackRetry;
        set => SetProperty(ref _enableProtocolFallbackRetry, value);
    }

    public SettingsOptionState? SelectedEvidenceRetentionMode
    {
        get => _selectedEvidenceRetentionMode;
        set => SetProperty(ref _selectedEvidenceRetentionMode, value);
    }
}

public sealed class InspectionTaskExecutionEditorState : ViewModelBase
{
    private bool _enableSinglePointInspection;
    private bool _enableBatchInspection;
    private bool _reserveScheduledTasks;
    private int _reservedMaxConcurrency;
    private string _defaultTaskNamePattern = string.Empty;
    private bool _enforceGroupSerialExecution;

    public bool EnableSinglePointInspection
    {
        get => _enableSinglePointInspection;
        set => SetProperty(ref _enableSinglePointInspection, value);
    }

    public bool EnableBatchInspection
    {
        get => _enableBatchInspection;
        set => SetProperty(ref _enableBatchInspection, value);
    }

    public bool ReserveScheduledTasks
    {
        get => _reserveScheduledTasks;
        set => SetProperty(ref _reserveScheduledTasks, value);
    }

    public int ReservedMaxConcurrency
    {
        get => _reservedMaxConcurrency;
        set => SetProperty(ref _reservedMaxConcurrency, value);
    }

    public string DefaultTaskNamePattern
    {
        get => _defaultTaskNamePattern;
        set => SetProperty(ref _defaultTaskNamePattern, value);
    }

    public bool EnforceGroupSerialExecution
    {
        get => _enforceGroupSerialExecution;
        set => SetProperty(ref _enforceGroupSerialExecution, value);
    }
}
