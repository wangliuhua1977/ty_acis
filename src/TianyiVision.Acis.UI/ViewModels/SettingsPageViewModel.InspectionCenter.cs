using System.Collections.ObjectModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Inspection;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel
{
    private InspectionSettingsSnapshot _inspectionSettingsSnapshot = new([], new([], new(true, true, true, true, true), []), new(InspectionSettingsValueKeys.DispatchModeManualConfirm, []), new(12, 3, 4, 2, 10, InspectionSettingsValueKeys.EvidenceRetentionKeepDays, 30, true, true), new(true, true, true, 4, "AIXC-{yyyyMMdd}-{mode}-{pointId}", true));
    private string? _selectedScopePlanId;
    private string? _selectedPointPolicyOverrideId;
    private string? _selectedDispatchOverrideId;
    private SettingsOptionState? _selectedGlobalDispatchMode;
    private InspectionPointPolicyOverrideEditorState? _pointPolicyOverrideEditor;
    private InspectionDispatchOverrideEditorState? _dispatchOverrideEditor;
    private InspectionVideoStrategyEditorState? _videoStrategyEditor;
    private InspectionTaskExecutionEditorState? _taskExecutionEditor;

    public ObservableCollection<InspectionScopePlanState> ScopePlans { get; } = [];

    public ObservableCollection<InspectionAlertTypeSelectionState> GlobalAlertTypes { get; } = [];

    public ObservableCollection<InspectionPolicyToggleState> GlobalPolicyToggles { get; } = [];

    public ObservableCollection<InspectionPointPolicyOverrideState> PointPolicyOverrides { get; } = [];

    public ObservableCollection<InspectionDispatchOverrideState> DispatchOverrides { get; } = [];

    public ObservableCollection<SettingsOptionState> GlobalDispatchModeOptions { get; } = [];

    public InspectionScopePlanEditorState ScopePlanEditor { get; } = new();

    public SettingsOptionState? SelectedGlobalDispatchMode
    {
        get => _selectedGlobalDispatchMode;
        set => SetProperty(ref _selectedGlobalDispatchMode, value);
    }

    public InspectionPointPolicyOverrideEditorState? PointPolicyOverrideEditor
    {
        get => _pointPolicyOverrideEditor;
        private set => SetProperty(ref _pointPolicyOverrideEditor, value);
    }

    public InspectionDispatchOverrideEditorState? DispatchOverrideEditor
    {
        get => _dispatchOverrideEditor;
        private set => SetProperty(ref _dispatchOverrideEditor, value);
    }

    public InspectionVideoStrategyEditorState? VideoStrategyEditor
    {
        get => _videoStrategyEditor;
        private set => SetProperty(ref _videoStrategyEditor, value);
    }

    public InspectionTaskExecutionEditorState? TaskExecutionEditor
    {
        get => _taskExecutionEditor;
        private set => SetProperty(ref _taskExecutionEditor, value);
    }

    public ICommand SelectScopePlanCommand => new RelayCommand(parameter =>
    {
        if (parameter is InspectionScopePlanState item)
        {
            SelectScopePlan(item.Id);
        }
    });

    public ICommand NewScopePlanCommand => new RelayCommand(_ => BeginNewScopePlan());

    public ICommand SaveScopePlanCommand => new RelayCommand(_ => SaveScopePlan());

    public ICommand DeleteScopePlanCommand => new RelayCommand(_ => DeleteScopePlan(), _ => !string.IsNullOrWhiteSpace(_selectedScopePlanId));

    public ICommand SaveAlertStrategyCommand => new RelayCommand(_ => SaveAlertStrategy());

    public ICommand SelectPointPolicyOverrideCommand => new RelayCommand(parameter =>
    {
        if (parameter is InspectionPointPolicyOverrideState item)
        {
            SelectPointPolicyOverride(item.Id);
        }
    });

    public ICommand NewPointPolicyOverrideCommand => new RelayCommand(_ => BeginNewPointPolicyOverride());

    public ICommand SavePointPolicyOverrideCommand => new RelayCommand(_ => SavePointPolicyOverride());

    public ICommand DeletePointPolicyOverrideCommand => new RelayCommand(_ => DeletePointPolicyOverride(), _ => !string.IsNullOrWhiteSpace(_selectedPointPolicyOverrideId));

    public ICommand SaveDispatchStrategyCommand => new RelayCommand(_ => SaveDispatchStrategy());

    public ICommand SelectDispatchOverrideCommand => new RelayCommand(parameter =>
    {
        if (parameter is InspectionDispatchOverrideState item)
        {
            SelectDispatchOverride(item.Id);
        }
    });

    public ICommand NewDispatchOverrideCommand => new RelayCommand(_ => BeginNewDispatchOverride());

    public ICommand SaveDispatchOverrideCommand => new RelayCommand(_ => SaveDispatchOverride());

    public ICommand DeleteDispatchOverrideCommand => new RelayCommand(_ => DeleteDispatchOverride(), _ => !string.IsNullOrWhiteSpace(_selectedDispatchOverrideId));

    public ICommand SaveVideoStrategyCommand => new RelayCommand(_ => SaveVideoStrategy());

    public ICommand SaveTaskExecutionCommand => new RelayCommand(_ => SaveTaskExecution());

    public ICommand ReloadInspectionSettingsCommand => new RelayCommand(_ => ReloadInspectionSettings());

    public bool IsInspectionScopePlansVisible => SelectedSection?.Key == SettingsSectionKey.InspectionScopePlans;

    public bool IsInspectionAlertStrategyVisible => SelectedSection?.Key == SettingsSectionKey.InspectionAlertStrategy;

    public bool IsInspectionDispatchStrategyVisible => SelectedSection?.Key == SettingsSectionKey.InspectionDispatchStrategy;

    public bool IsInspectionVideoStrategyVisible => SelectedSection?.Key == SettingsSectionKey.InspectionVideoStrategy;

    public bool IsInspectionTaskExecutionVisible => SelectedSection?.Key == SettingsSectionKey.InspectionTaskExecution;

    public string InspectionNewText => _textService.Resolve(TextTokens.SettingsActionNew);

    public string InspectionSaveText => _textService.Resolve(TextTokens.SettingsActionSave);

    public string InspectionDeleteText => _textService.Resolve(TextTokens.SettingsActionDelete);

    public string InspectionReloadText => _textService.Resolve(TextTokens.SettingsActionReload);

    public string ScopePlansPageTitle => _textService.Resolve(TextTokens.SettingsInspectionScopePlansPageTitle);

    public string ScopePlansPageDescription => _textService.Resolve(TextTokens.SettingsInspectionScopePlansPageDescription);

    public string AlertStrategyPageTitle => _textService.Resolve(TextTokens.SettingsInspectionAlertStrategyPageTitle);

    public string AlertStrategyPageDescription => _textService.Resolve(TextTokens.SettingsInspectionAlertStrategyPageDescription);

    public string DispatchStrategyPageTitle => _textService.Resolve(TextTokens.SettingsInspectionDispatchStrategyPageTitle);

    public string DispatchStrategyPageDescription => _textService.Resolve(TextTokens.SettingsInspectionDispatchStrategyPageDescription);

    public string VideoCenterPageTitle => _textService.Resolve(TextTokens.SettingsInspectionVideoStrategyPageTitle);

    public string VideoCenterPageDescription => _textService.Resolve(TextTokens.SettingsInspectionVideoStrategyPageDescription);

    public string TaskExecutionPageTitle => _textService.Resolve(TextTokens.SettingsInspectionTaskExecutionPageTitle);

    public string TaskExecutionPageDescription => _textService.Resolve(TextTokens.SettingsInspectionTaskExecutionPageDescription);

    public string ScopeNameLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeNameLabel);

    public string ScopeDescriptionLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeDescriptionLabel);

    public string ScopeIncludedRegionsLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeIncludedRegionsLabel);

    public string ScopeIncludedDirectoriesLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeIncludedDirectoriesLabel);

    public string ScopeIncludedPointsLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeIncludedPointsLabel);

    public string ScopeExcludedPointsLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeExcludedPointsLabel);

    public string ScopeFocusPointsLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeFocusPointsLabel);

    public string ScopeEnabledLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeEnabledLabel);

    public string ScopeDefaultLabel => _textService.Resolve(TextTokens.SettingsInspectionScopeDefaultLabel);

    public string AlertTypesTitle => _textService.Resolve(TextTokens.SettingsInspectionAlertTypesTitle);

    public string GlobalPolicyTitle => _textService.Resolve(TextTokens.SettingsInspectionAlertGlobalPolicyTitle);

    public string AlertOverrideListTitle => _textService.Resolve(TextTokens.SettingsInspectionAlertOverrideListTitle);

    public string AlertOverrideEditorTitle => _textService.Resolve(TextTokens.SettingsInspectionAlertOverrideEditorTitle);

    public string InspectionPointIdFieldLabel => _textService.Resolve(TextTokens.SettingsInspectionPointIdLabel);

    public string InspectionPointNameFieldLabel => _textService.Resolve(TextTokens.SettingsInspectionPointNameLabel);

    public string UseGlobalAlertTypesLabel => _textService.Resolve(TextTokens.SettingsInspectionUseGlobalAlertTypesLabel);

    public string DispatchGlobalModeLabel => _textService.Resolve(TextTokens.SettingsInspectionDispatchGlobalModeLabel);

    public string DispatchOverrideListTitle => _textService.Resolve(TextTokens.SettingsInspectionDispatchOverrideListTitle);

    public string DispatchOverrideEditorTitle => _textService.Resolve(TextTokens.SettingsInspectionDispatchOverrideEditorTitle);

    public string VideoPlaybackTimeoutLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoPlaybackTimeoutLabel);

    public string VideoScreenshotCountLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoScreenshotCountLabel);

    public string VideoScreenshotIntervalLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoScreenshotIntervalLabel);

    public string VideoRetryCountLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoRetryCountLabel);

    public string VideoReinspectionIntervalLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoReinspectionIntervalLabel);

    public string VideoEvidenceRetentionModeLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoEvidenceRetentionModeLabel);

    public string VideoEvidenceRetentionDaysLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoEvidenceRetentionDaysLabel);

    public string VideoAllowManualSupplementLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoAllowManualSupplementLabel);

    public string VideoEnableProtocolFallbackLabel => _textService.Resolve(TextTokens.SettingsInspectionVideoEnableProtocolFallbackLabel);

    public string TaskEnableSingleLabel => _textService.Resolve(TextTokens.SettingsInspectionTaskEnableSingleLabel);

    public string TaskEnableBatchLabel => _textService.Resolve(TextTokens.SettingsInspectionTaskEnableBatchLabel);

    public string TaskReserveScheduledLabel => _textService.Resolve(TextTokens.SettingsInspectionTaskReserveScheduledLabel);

    public string TaskReservedMaxConcurrencyLabel => _textService.Resolve(TextTokens.SettingsInspectionTaskReservedMaxConcurrencyLabel);

    public string TaskNamePatternLabel => _textService.Resolve(TextTokens.SettingsInspectionTaskNamePatternLabel);

    public string TaskSerialExecutionLabel => _textService.Resolve(TextTokens.SettingsInspectionTaskSerialExecutionLabel);

    public string InspectionSettingsFeedback => AppliedState.StatusText;

    public void InitializeInspectionSettings()
    {
        GlobalDispatchModeOptions.Clear();
        foreach (var option in CreateDispatchModeOptions())
        {
            GlobalDispatchModeOptions.Add(option);
        }

        VideoStrategyEditor = new InspectionVideoStrategyEditorState(CreateEvidenceRetentionModeOptions());
        TaskExecutionEditor = new InspectionTaskExecutionEditorState();
        LoadInspectionSettings();
    }

    private void ReloadInspectionSettings()
    {
        LoadInspectionSettings();
        AppliedState.StatusText = _textService.Resolve(TextTokens.SettingsInspectionReloadFeedback);
    }

    private void LoadInspectionSettings()
    {
        _inspectionSettingsSnapshot = _inspectionSettingsService.Load();
        LoadScopePlans();
        LoadAlertStrategy();
        LoadDispatchStrategy();
        LoadVideoStrategy();
        LoadTaskExecution();
    }

    private void LoadScopePlans()
    {
        ScopePlans.Clear();
        foreach (var plan in _inspectionSettingsSnapshot.ScopePlans)
        {
            ScopePlans.Add(new InspectionScopePlanState(
                plan.PlanId,
                plan.PlanName,
                $"{plan.IncludedRegions.Count} 个区域 / {plan.IncludedDirectories.Count} 个目录 / {plan.IncludedPointIds.Count} 个点位",
                plan.IsEnabled,
                plan.IsDefault));
        }

        SelectScopePlan(_selectedScopePlanId ?? _inspectionSettingsSnapshot.ScopePlans.FirstOrDefault()?.PlanId);
    }

    private void SelectScopePlan(string? planId)
    {
        var selected = _inspectionSettingsSnapshot.ScopePlans.FirstOrDefault(plan => plan.PlanId == planId)
            ?? _inspectionSettingsSnapshot.ScopePlans.FirstOrDefault();
        _selectedScopePlanId = selected?.PlanId;

        foreach (var item in ScopePlans)
        {
            item.IsSelected = item.Id == _selectedScopePlanId;
        }

        if (selected is null)
        {
            BeginNewScopePlan();
            return;
        }

        ScopePlanEditor.Name = selected.PlanName;
        ScopePlanEditor.Description = selected.Description;
        ScopePlanEditor.IncludedRegionsText = JoinLines(selected.IncludedRegions);
        ScopePlanEditor.IncludedDirectoriesText = JoinLines(selected.IncludedDirectories);
        ScopePlanEditor.IncludedPointsText = JoinLines(selected.IncludedPointIds);
        ScopePlanEditor.ExcludedPointsText = JoinLines(selected.ExcludedPointIds);
        ScopePlanEditor.FocusPointsText = JoinLines(selected.FocusPointIds);
        ScopePlanEditor.IsEnabled = selected.IsEnabled;
        ScopePlanEditor.IsDefault = selected.IsDefault;
    }

    private void BeginNewScopePlan()
    {
        _selectedScopePlanId = null;
        foreach (var item in ScopePlans)
        {
            item.IsSelected = false;
        }

        ScopePlanEditor.Name = string.Empty;
        ScopePlanEditor.Description = string.Empty;
        ScopePlanEditor.IncludedRegionsText = string.Empty;
        ScopePlanEditor.IncludedDirectoriesText = string.Empty;
        ScopePlanEditor.IncludedPointsText = string.Empty;
        ScopePlanEditor.ExcludedPointsText = string.Empty;
        ScopePlanEditor.FocusPointsText = string.Empty;
        ScopePlanEditor.IsEnabled = true;
        ScopePlanEditor.IsDefault = ScopePlans.Count == 0;
    }

    private void SaveScopePlan()
    {
        var planId = string.IsNullOrWhiteSpace(_selectedScopePlanId) ? $"scope-{Guid.NewGuid():N}" : _selectedScopePlanId;
        var plans = _inspectionSettingsSnapshot.ScopePlans
            .Where(plan => !string.Equals(plan.PlanId, planId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        plans.Add(new InspectionScopePlanSettings(
            planId!,
            string.IsNullOrWhiteSpace(ScopePlanEditor.Name) ? "未命名方案" : ScopePlanEditor.Name.Trim(),
            ScopePlanEditor.Description.Trim(),
            ScopePlanEditor.IsEnabled,
            ScopePlanEditor.IsDefault,
            SplitLines(ScopePlanEditor.IncludedRegionsText),
            SplitLines(ScopePlanEditor.IncludedDirectoriesText),
            SplitLines(ScopePlanEditor.IncludedPointsText),
            SplitLines(ScopePlanEditor.ExcludedPointsText),
            SplitLines(ScopePlanEditor.FocusPointsText)));

        SaveInspectionSettings(_inspectionSettingsSnapshot with { ScopePlans = plans }, planId: planId);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), ScopePlansPageTitle);
    }

    private void DeleteScopePlan()
    {
        if (string.IsNullOrWhiteSpace(_selectedScopePlanId))
        {
            return;
        }

        var plans = _inspectionSettingsSnapshot.ScopePlans
            .Where(plan => !string.Equals(plan.PlanId, _selectedScopePlanId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        SaveInspectionSettings(_inspectionSettingsSnapshot with { ScopePlans = plans }, planId: plans.FirstOrDefault()?.PlanId);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), ScopePlansPageTitle);
    }

    private void LoadAlertStrategy()
    {
        ResetAlertTypes(GlobalAlertTypes, _inspectionSettingsSnapshot.AlertStrategy.EnabledAlertTypeIds);
        ResetPolicyToggles(GlobalPolicyToggles, _inspectionSettingsSnapshot.AlertStrategy.GlobalPolicy);

        PointPolicyOverrides.Clear();
        foreach (var item in _inspectionSettingsSnapshot.AlertStrategy.PointOverrides)
        {
            PointPolicyOverrides.Add(new InspectionPointPolicyOverrideState(
                item.OverrideId,
                item.PointId,
                item.PointName,
                item.UseGlobalAlertTypes ? "沿用全局告警类型" : $"{item.EnabledAlertTypeIds.Count} 个点位覆盖告警"));
        }

        SelectPointPolicyOverride(_selectedPointPolicyOverrideId ?? _inspectionSettingsSnapshot.AlertStrategy.PointOverrides.FirstOrDefault()?.OverrideId);
    }

    private void SelectPointPolicyOverride(string? overrideId)
    {
        var selected = _inspectionSettingsSnapshot.AlertStrategy.PointOverrides.FirstOrDefault(item => item.OverrideId == overrideId)
            ?? _inspectionSettingsSnapshot.AlertStrategy.PointOverrides.FirstOrDefault();
        _selectedPointPolicyOverrideId = selected?.OverrideId;

        foreach (var item in PointPolicyOverrides)
        {
            item.IsSelected = item.Id == _selectedPointPolicyOverrideId;
        }

        PointPolicyOverrideEditor = CreatePointPolicyOverrideEditor(selected);
    }

    private void BeginNewPointPolicyOverride()
    {
        _selectedPointPolicyOverrideId = null;
        foreach (var item in PointPolicyOverrides)
        {
            item.IsSelected = false;
        }

        PointPolicyOverrideEditor = CreatePointPolicyOverrideEditor(null);
    }

    private void SaveAlertStrategy()
    {
        var alertStrategy = _inspectionSettingsSnapshot.AlertStrategy with
        {
            EnabledAlertTypeIds = GetSelectedAlertTypeIds(GlobalAlertTypes),
            GlobalPolicy = BuildPolicy(GlobalPolicyToggles)
        };

        SaveInspectionSettings(_inspectionSettingsSnapshot with { AlertStrategy = alertStrategy });
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), AlertStrategyPageTitle);
    }

    private void SavePointPolicyOverride()
    {
        if (PointPolicyOverrideEditor is null)
        {
            return;
        }

        var overrideId = string.IsNullOrWhiteSpace(_selectedPointPolicyOverrideId) ? $"policy-{Guid.NewGuid():N}" : _selectedPointPolicyOverrideId;
        var overrides = _inspectionSettingsSnapshot.AlertStrategy.PointOverrides
            .Where(item => !string.Equals(item.OverrideId, overrideId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        overrides.Add(new InspectionPointPolicyOverrideSettings(
            overrideId!,
            PointPolicyOverrideEditor.PointId.Trim(),
            PointPolicyOverrideEditor.PointName.Trim(),
            PointPolicyOverrideEditor.UseGlobalAlertTypes,
            PointPolicyOverrideEditor.UseGlobalAlertTypes ? Array.Empty<string>() : GetSelectedAlertTypeIds(PointPolicyOverrideEditor.AlertTypes),
            BuildPolicy(PointPolicyOverrideEditor.PolicyToggles)));

        var alertStrategy = _inspectionSettingsSnapshot.AlertStrategy with { PointOverrides = overrides };
        SaveInspectionSettings(_inspectionSettingsSnapshot with { AlertStrategy = alertStrategy }, policyOverrideId: overrideId);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), AlertStrategyPageTitle);
    }

    private void DeletePointPolicyOverride()
    {
        if (string.IsNullOrWhiteSpace(_selectedPointPolicyOverrideId))
        {
            return;
        }

        var overrides = _inspectionSettingsSnapshot.AlertStrategy.PointOverrides
            .Where(item => !string.Equals(item.OverrideId, _selectedPointPolicyOverrideId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var alertStrategy = _inspectionSettingsSnapshot.AlertStrategy with { PointOverrides = overrides };
        SaveInspectionSettings(_inspectionSettingsSnapshot with { AlertStrategy = alertStrategy }, policyOverrideId: overrides.FirstOrDefault()?.OverrideId);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), AlertStrategyPageTitle);
    }

    private void LoadDispatchStrategy()
    {
        SelectedGlobalDispatchMode = GlobalDispatchModeOptions.FirstOrDefault(option => option.Key == _inspectionSettingsSnapshot.DispatchStrategy.GlobalDispatchMode)
            ?? GlobalDispatchModeOptions.FirstOrDefault();

        DispatchOverrides.Clear();
        foreach (var item in _inspectionSettingsSnapshot.DispatchStrategy.PointOverrides)
        {
            DispatchOverrides.Add(new InspectionDispatchOverrideState(
                item.OverrideId,
                item.PointId,
                item.PointName,
                ResolveDispatchModeLabel(item.DispatchMode)));
        }

        SelectDispatchOverride(_selectedDispatchOverrideId ?? _inspectionSettingsSnapshot.DispatchStrategy.PointOverrides.FirstOrDefault()?.OverrideId);
    }

    private void SelectDispatchOverride(string? overrideId)
    {
        var selected = _inspectionSettingsSnapshot.DispatchStrategy.PointOverrides.FirstOrDefault(item => item.OverrideId == overrideId)
            ?? _inspectionSettingsSnapshot.DispatchStrategy.PointOverrides.FirstOrDefault();
        _selectedDispatchOverrideId = selected?.OverrideId;

        foreach (var item in DispatchOverrides)
        {
            item.IsSelected = item.Id == _selectedDispatchOverrideId;
        }

        DispatchOverrideEditor = CreateDispatchOverrideEditor(selected);
    }

    private void BeginNewDispatchOverride()
    {
        _selectedDispatchOverrideId = null;
        foreach (var item in DispatchOverrides)
        {
            item.IsSelected = false;
        }

        DispatchOverrideEditor = CreateDispatchOverrideEditor(null);
    }

    private void SaveDispatchStrategy()
    {
        var dispatchStrategy = _inspectionSettingsSnapshot.DispatchStrategy with
        {
            GlobalDispatchMode = SelectedGlobalDispatchMode?.Key ?? _inspectionSettingsSnapshot.DispatchStrategy.GlobalDispatchMode
        };

        SaveInspectionSettings(_inspectionSettingsSnapshot with { DispatchStrategy = dispatchStrategy });
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), DispatchStrategyPageTitle);
    }

    private void SaveDispatchOverride()
    {
        if (DispatchOverrideEditor is null)
        {
            return;
        }

        var overrideId = string.IsNullOrWhiteSpace(_selectedDispatchOverrideId) ? $"dispatch-{Guid.NewGuid():N}" : _selectedDispatchOverrideId;
        var overrides = _inspectionSettingsSnapshot.DispatchStrategy.PointOverrides
            .Where(item => !string.Equals(item.OverrideId, overrideId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        overrides.Add(new InspectionDispatchOverrideSettings(
            overrideId!,
            DispatchOverrideEditor.PointId.Trim(),
            DispatchOverrideEditor.PointName.Trim(),
            DispatchOverrideEditor.SelectedMode?.Key ?? InspectionSettingsValueKeys.DispatchModeManualConfirm));

        var dispatchStrategy = _inspectionSettingsSnapshot.DispatchStrategy with { PointOverrides = overrides };
        SaveInspectionSettings(_inspectionSettingsSnapshot with { DispatchStrategy = dispatchStrategy }, dispatchOverrideId: overrideId);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), DispatchStrategyPageTitle);
    }

    private void DeleteDispatchOverride()
    {
        if (string.IsNullOrWhiteSpace(_selectedDispatchOverrideId))
        {
            return;
        }

        var overrides = _inspectionSettingsSnapshot.DispatchStrategy.PointOverrides
            .Where(item => !string.Equals(item.OverrideId, _selectedDispatchOverrideId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var dispatchStrategy = _inspectionSettingsSnapshot.DispatchStrategy with { PointOverrides = overrides };
        SaveInspectionSettings(_inspectionSettingsSnapshot with { DispatchStrategy = dispatchStrategy }, dispatchOverrideId: overrides.FirstOrDefault()?.OverrideId);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), DispatchStrategyPageTitle);
    }

    private void LoadVideoStrategy()
    {
        if (VideoStrategyEditor is null)
        {
            return;
        }

        var settings = _inspectionSettingsSnapshot.VideoInspection;
        VideoStrategyEditor.PlaybackTimeoutSeconds = settings.PlaybackTimeoutSeconds;
        VideoStrategyEditor.ScreenshotCount = settings.ScreenshotCount;
        VideoStrategyEditor.ScreenshotIntervalSeconds = settings.ScreenshotIntervalSeconds;
        VideoStrategyEditor.PlaybackFailureRetryCount = settings.PlaybackFailureRetryCount;
        VideoStrategyEditor.ReinspectionIntervalMinutes = settings.ReinspectionIntervalMinutes;
        VideoStrategyEditor.EvidenceRetentionDays = settings.EvidenceRetentionDays;
        VideoStrategyEditor.AllowManualSupplementScreenshot = settings.AllowManualSupplementScreenshot;
        VideoStrategyEditor.EnableProtocolFallbackRetry = settings.EnableProtocolFallbackRetry;
        VideoStrategyEditor.SelectedEvidenceRetentionMode = VideoStrategyEditor.EvidenceRetentionModes.FirstOrDefault(option => option.Key == settings.EvidenceRetentionMode);
    }

    private void SaveVideoStrategy()
    {
        if (VideoStrategyEditor is null)
        {
            return;
        }

        var videoStrategy = new InspectionVideoInspectionSettings(
            VideoStrategyEditor.PlaybackTimeoutSeconds,
            VideoStrategyEditor.ScreenshotCount,
            VideoStrategyEditor.ScreenshotIntervalSeconds,
            VideoStrategyEditor.PlaybackFailureRetryCount,
            VideoStrategyEditor.ReinspectionIntervalMinutes,
            VideoStrategyEditor.SelectedEvidenceRetentionMode?.Key ?? InspectionSettingsValueKeys.EvidenceRetentionKeepDays,
            VideoStrategyEditor.EvidenceRetentionDays,
            VideoStrategyEditor.AllowManualSupplementScreenshot,
            VideoStrategyEditor.EnableProtocolFallbackRetry);

        SaveInspectionSettings(_inspectionSettingsSnapshot with { VideoInspection = videoStrategy });
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), VideoCenterPageTitle);
    }

    private void LoadTaskExecution()
    {
        if (TaskExecutionEditor is null)
        {
            return;
        }

        var settings = _inspectionSettingsSnapshot.TaskExecution;
        TaskExecutionEditor.EnableSinglePointInspection = settings.EnableSinglePointInspection;
        TaskExecutionEditor.EnableBatchInspection = settings.EnableBatchInspection;
        TaskExecutionEditor.ReserveScheduledTasks = settings.ReserveScheduledTasks;
        TaskExecutionEditor.ReservedMaxConcurrency = settings.ReservedMaxConcurrency;
        TaskExecutionEditor.DefaultTaskNamePattern = settings.DefaultTaskNamePattern;
        TaskExecutionEditor.EnforceGroupSerialExecution = settings.EnforceGroupSerialExecution;
    }

    private void SaveTaskExecution()
    {
        if (TaskExecutionEditor is null)
        {
            return;
        }

        var taskExecution = new InspectionTaskExecutionSettings(
            TaskExecutionEditor.EnableSinglePointInspection,
            TaskExecutionEditor.EnableBatchInspection,
            TaskExecutionEditor.ReserveScheduledTasks,
            TaskExecutionEditor.ReservedMaxConcurrency,
            TaskExecutionEditor.DefaultTaskNamePattern,
            true);

        SaveInspectionSettings(_inspectionSettingsSnapshot with { TaskExecution = taskExecution });
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsInspectionSaveFeedbackPattern), TaskExecutionPageTitle);
    }

    private void SaveInspectionSettings(
        InspectionSettingsSnapshot snapshot,
        string? planId = null,
        string? policyOverrideId = null,
        string? dispatchOverrideId = null)
    {
        _inspectionSettingsService.Save(snapshot);
        _selectedScopePlanId = planId ?? _selectedScopePlanId;
        _selectedPointPolicyOverrideId = policyOverrideId ?? _selectedPointPolicyOverrideId;
        _selectedDispatchOverrideId = dispatchOverrideId ?? _selectedDispatchOverrideId;
        LoadInspectionSettings();
    }

    private InspectionPointPolicyOverrideEditorState CreatePointPolicyOverrideEditor(InspectionPointPolicyOverrideSettings? settings)
    {
        var editor = new InspectionPointPolicyOverrideEditorState(
            CreateAlertTypeSelections(settings?.UseGlobalAlertTypes == false
                ? settings.EnabledAlertTypeIds
                : _inspectionSettingsSnapshot.AlertStrategy.EnabledAlertTypeIds),
            CreatePolicyToggles(settings?.Policy));
        editor.PointId = settings?.PointId ?? string.Empty;
        editor.PointName = settings?.PointName ?? string.Empty;
        editor.UseGlobalAlertTypes = settings?.UseGlobalAlertTypes ?? true;
        return editor;
    }

    private InspectionDispatchOverrideEditorState CreateDispatchOverrideEditor(InspectionDispatchOverrideSettings? settings)
    {
        var editor = new InspectionDispatchOverrideEditorState(CreateDispatchModeOptions());
        editor.PointId = settings?.PointId ?? string.Empty;
        editor.PointName = settings?.PointName ?? string.Empty;
        editor.SelectedMode = editor.ModeOptions.FirstOrDefault(option => option.Key == (settings?.DispatchMode ?? _inspectionSettingsSnapshot.DispatchStrategy.GlobalDispatchMode));
        return editor;
    }

    private void ResetAlertTypes(ObservableCollection<InspectionAlertTypeSelectionState> target, IReadOnlyList<string> enabledIds)
    {
        target.Clear();
        foreach (var item in CreateAlertTypeSelections(enabledIds))
        {
            target.Add(item);
        }
    }

    private void ResetPolicyToggles(ObservableCollection<InspectionPolicyToggleState> target, InspectionPointPolicySettings settings)
    {
        target.Clear();
        foreach (var item in CreatePolicyToggles(settings))
        {
            target.Add(item);
        }
    }

    private ObservableCollection<InspectionAlertTypeSelectionState> CreateAlertTypeSelections(IReadOnlyList<string> enabledIds)
    {
        return
        [
            new InspectionAlertTypeSelectionState(InspectionSettingsValueKeys.AlertTypeImageAbnormal, _textService.Resolve(TextTokens.SettingsInspectionAlertTypeImageAbnormal), _textService.Resolve(TextTokens.SettingsInspectionAlertTypeImageAbnormalDescription), enabledIds.Contains(InspectionSettingsValueKeys.AlertTypeImageAbnormal, StringComparer.OrdinalIgnoreCase)),
            new InspectionAlertTypeSelectionState(InspectionSettingsValueKeys.AlertTypeRegionIntrusion, _textService.Resolve(TextTokens.SettingsInspectionAlertTypeRegionIntrusion), _textService.Resolve(TextTokens.SettingsInspectionAlertTypeRegionIntrusionDescription), enabledIds.Contains(InspectionSettingsValueKeys.AlertTypeRegionIntrusion, StringComparer.OrdinalIgnoreCase)),
            new InspectionAlertTypeSelectionState(InspectionSettingsValueKeys.AlertTypeFire, _textService.Resolve(TextTokens.SettingsInspectionAlertTypeFire), _textService.Resolve(TextTokens.SettingsInspectionAlertTypeFireDescription), enabledIds.Contains(InspectionSettingsValueKeys.AlertTypeFire, StringComparer.OrdinalIgnoreCase)),
            new InspectionAlertTypeSelectionState(InspectionSettingsValueKeys.AlertTypePassengerFlow, _textService.Resolve(TextTokens.SettingsInspectionAlertTypePassengerFlow), _textService.Resolve(TextTokens.SettingsInspectionAlertTypePassengerFlowDescription), enabledIds.Contains(InspectionSettingsValueKeys.AlertTypePassengerFlow, StringComparer.OrdinalIgnoreCase)),
            new InspectionAlertTypeSelectionState(InspectionSettingsValueKeys.AlertTypeFaceOrPlate, _textService.Resolve(TextTokens.SettingsInspectionAlertTypeFaceOrPlate), _textService.Resolve(TextTokens.SettingsInspectionAlertTypeFaceOrPlateDescription), enabledIds.Contains(InspectionSettingsValueKeys.AlertTypeFaceOrPlate, StringComparer.OrdinalIgnoreCase))
        ];
    }

    private ObservableCollection<InspectionPolicyToggleState> CreatePolicyToggles(InspectionPointPolicySettings? settings)
    {
        settings ??= _inspectionSettingsSnapshot.AlertStrategy.GlobalPolicy;
        return
        [
            new InspectionPolicyToggleState("online", _textService.Resolve(TextTokens.SettingsInspectionPolicyOnlineLabel), _textService.Resolve(TextTokens.SettingsInspectionPolicyOnlineDescription), settings.RequireOnlineStatusCheck),
            new InspectionPolicyToggleState("playback", _textService.Resolve(TextTokens.SettingsInspectionPolicyPlaybackLabel), _textService.Resolve(TextTokens.SettingsInspectionPolicyPlaybackDescription), settings.RequirePlaybackCheck),
            new InspectionPolicyToggleState("interfaceAi", _textService.Resolve(TextTokens.SettingsInspectionPolicyInterfaceAiLabel), _textService.Resolve(TextTokens.SettingsInspectionPolicyInterfaceAiDescription), settings.EnableInterfaceAiDecision),
            new InspectionPolicyToggleState("localAnalysis", _textService.Resolve(TextTokens.SettingsInspectionPolicyLocalAnalysisLabel), _textService.Resolve(TextTokens.SettingsInspectionPolicyLocalAnalysisDescription), settings.EnableLocalScreenshotAnalysis),
            new InspectionPolicyToggleState("reviewWall", _textService.Resolve(TextTokens.SettingsInspectionPolicyReviewWallLabel), _textService.Resolve(TextTokens.SettingsInspectionPolicyReviewWallDescription), settings.RouteAbnormalPointsToReviewWall)
        ];
    }

    private ObservableCollection<SettingsOptionState> CreateDispatchModeOptions()
    {
        return
        [
            new SettingsOptionState(InspectionSettingsValueKeys.DispatchModeAuto, _textService.Resolve(TextTokens.SettingsInspectionDispatchModeAuto)),
            new SettingsOptionState(InspectionSettingsValueKeys.DispatchModeManualConfirm, _textService.Resolve(TextTokens.SettingsInspectionDispatchModeManualConfirm))
        ];
    }

    private ObservableCollection<SettingsOptionState> CreateEvidenceRetentionModeOptions()
    {
        return
        [
            new SettingsOptionState(InspectionSettingsValueKeys.EvidenceRetentionKeepDays, _textService.Resolve(TextTokens.SettingsInspectionVideoEvidenceKeepDays)),
            new SettingsOptionState(InspectionSettingsValueKeys.EvidenceRetentionKeepLatestTask, _textService.Resolve(TextTokens.SettingsInspectionVideoEvidenceKeepLatest)),
            new SettingsOptionState(InspectionSettingsValueKeys.EvidenceRetentionManualCleanup, _textService.Resolve(TextTokens.SettingsInspectionVideoEvidenceManualCleanup))
        ];
    }

    private static InspectionPointPolicySettings BuildPolicy(IEnumerable<InspectionPolicyToggleState> toggles)
    {
        var map = toggles.ToDictionary(item => item.Key, item => item.IsEnabled, StringComparer.OrdinalIgnoreCase);
        return new InspectionPointPolicySettings(
            map.GetValueOrDefault("online", true),
            map.GetValueOrDefault("playback"),
            map.GetValueOrDefault("interfaceAi"),
            map.GetValueOrDefault("localAnalysis"),
            map.GetValueOrDefault("reviewWall"));
    }

    private static IReadOnlyList<string> GetSelectedAlertTypeIds(IEnumerable<InspectionAlertTypeSelectionState> items)
    {
        return items
            .Where(item => item.IsSelected)
            .Select(item => item.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveDispatchModeLabel(string dispatchMode)
    {
        return string.Equals(dispatchMode, InspectionSettingsValueKeys.DispatchModeAuto, StringComparison.OrdinalIgnoreCase)
            ? _textService.Resolve(TextTokens.SettingsInspectionDispatchModeAuto)
            : _textService.Resolve(TextTokens.SettingsInspectionDispatchModeManualConfirm);
    }

    private static IReadOnlyList<string> SplitLines(string? text)
    {
        return (text ?? string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string JoinLines(IEnumerable<string> items)
        => string.Join(Environment.NewLine, items);
}
