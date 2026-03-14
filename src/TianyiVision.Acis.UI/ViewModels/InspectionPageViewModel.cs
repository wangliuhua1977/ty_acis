using System.Collections.ObjectModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Inspection;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class InspectionPageViewModel : PageViewModelBase
{
    private readonly ITextService _textService;
    private readonly Dictionary<string, GroupWorkspaceState> _workspaceByGroupId;
    private readonly RelayCommand _executeInspectionCommand;
    private readonly RelayCommand _openDispatchWorkspaceCommand;
    private readonly RelayCommand _openReportsCenterCommand;
    private RelayCommand _openReviewWallCommand = null!;
    private RelayCommand _confirmReviewCompletedCommand = null!;
    private RelayCommand _markSelectedReviewCommand = null!;
    private InspectionGroupSummaryState? _selectedGroup;
    private InspectionStrategySummaryState? _strategySummary;
    private InspectionTaskExecutionState? _executionState;
    private InspectionRunSummaryState? _runSummary;
    private ObservableCollection<InspectionPointState> _points = [];
    private ObservableCollection<MapPointState> _mapPoints = [];
    private ObservableCollection<MapPointState> _unmappedPoints = [];
    private ObservableCollection<RecentFaultSummaryState> _recentFaults = [];
    private InspectionPointDetailState? _selectedPointDetail;
    private InspectionPointState? _selectedPoint;
    private string _toggleGroupActionText = string.Empty;
    private string _selectedMapPointId = string.Empty;

    public InspectionPageViewModel(
        ITextService textService,
        IInspectionTaskService inspectionTaskService,
        MapProviderSettings mapProvider)
        : base(
            textService.Resolve(TextTokens.InspectionTitle),
            textService.Resolve(TextTokens.InspectionDescription))
    {
        _textService = textService;
        MapProvider = mapProvider;

        GroupSectionTitle = textService.Resolve(TextTokens.InspectionGroupTitle);
        GroupSectionDescription = textService.Resolve(TextTokens.InspectionGroupDescription);
        CurrentGroupLabel = textService.Resolve(TextTokens.InspectionGroupCurrentLabel);
        AvailableGroupsLabel = textService.Resolve(TextTokens.InspectionGroupAvailableLabel);

        StrategySectionTitle = textService.Resolve(TextTokens.InspectionStrategyTitle);
        StrategySectionDescription = textService.Resolve(TextTokens.InspectionStrategyDescription);
        StrategyFirstRunLabel = textService.Resolve(TextTokens.InspectionStrategyFirstRunLabel);
        StrategyDailyRunsLabel = textService.Resolve(TextTokens.InspectionStrategyDailyRunsLabel);
        StrategyIntervalLabel = textService.Resolve(TextTokens.InspectionStrategyIntervalLabel);
        StrategyResultModeLabel = textService.Resolve(TextTokens.InspectionStrategyResultModeLabel);
        StrategyDispatchModeLabel = textService.Resolve(TextTokens.InspectionStrategyDispatchModeLabel);

        ExecutionSectionTitle = textService.Resolve(TextTokens.InspectionExecutionTitle);
        ExecutionSectionDescription = textService.Resolve(TextTokens.InspectionExecutionDescription);
        ExecutionExecutedTodayLabel = textService.Resolve(TextTokens.InspectionExecutionExecutedTodayLabel);
        ExecutionTaskStatusLabel = textService.Resolve(TextTokens.InspectionExecutionTaskStatusLabel);
        ExecutionNextRunLabel = textService.Resolve(TextTokens.InspectionExecutionNextRunLabel);
        ExecutionProgressLabel = textService.Resolve(TextTokens.InspectionExecutionProgressLabel);
        ExecutionSimulationLabel = textService.Resolve(TextTokens.InspectionExecutionSimulationLabel);

        RecentFaultsSectionTitle = textService.Resolve(TextTokens.InspectionRecentFaultsTitle);
        RecentFaultsSectionDescription = textService.Resolve(TextTokens.InspectionRecentFaultsDescription);
        RecentFaultTimeLabel = textService.Resolve(TextTokens.InspectionRecentFaultTimeLabel);

        WorkbenchTitle = textService.Resolve(TextTokens.InspectionWorkbenchTitle);
        WorkbenchDescription = textService.Resolve(TextTokens.InspectionWorkbenchDescription);
        WorkbenchMapBadge = textService.Resolve(TextTokens.InspectionWorkbenchMapBadge);
        WorkbenchHint = textService.Resolve(TextTokens.InspectionWorkbenchHint);
        RunSummaryStartedAtLabel = textService.Resolve(TextTokens.InspectionRunSummaryStartedAtLabel);
        RunSummaryTotalPointsLabel = textService.Resolve(TextTokens.InspectionRunSummaryTotalPointsLabel);
        RunSummaryInspectedPointsLabel = textService.Resolve(TextTokens.InspectionRunSummaryInspectedPointsLabel);
        RunSummaryNormalLabel = textService.Resolve(TextTokens.InspectionRunSummaryNormalLabel);
        RunSummaryFaultLabel = textService.Resolve(TextTokens.InspectionRunSummaryFaultLabel);
        RunSummaryCurrentPointLabel = textService.Resolve(TextTokens.InspectionRunSummaryCurrentPointLabel);

        LegendPendingText = textService.Resolve(TextTokens.InspectionLegendPending);
        LegendActiveText = textService.Resolve(TextTokens.InspectionLegendActive);
        LegendNormalText = textService.Resolve(TextTokens.InspectionLegendNormal);
        LegendFaultText = textService.Resolve(TextTokens.InspectionLegendFault);
        LegendSilentText = textService.Resolve(TextTokens.InspectionLegendSilent);
        LegendPausedText = textService.Resolve(TextTokens.InspectionLegendPaused);

        DetailsSectionTitle = textService.Resolve(TextTokens.InspectionDetailsTitle);
        DetailsSectionDescription = textService.Resolve(TextTokens.InspectionDetailsDescription);
        DetailBasicTitle = textService.Resolve(TextTokens.InspectionDetailBasicTitle);
        DetailPointNameLabel = textService.Resolve(TextTokens.InspectionDetailPointNameLabel);
        DetailUnitLabel = textService.Resolve(TextTokens.InspectionDetailUnitLabel);
        DetailCurrentHandlingUnitLabel = textService.Resolve(TextTokens.InspectionDetailCurrentHandlingUnitLabel);
        DetailCurrentStatusLabel = textService.Resolve(TextTokens.InspectionDetailCurrentStatusLabel);
        DetailCoordinateLabel = textService.Resolve(TextTokens.InspectionDetailCoordinateLabel);
        DetailStatusTitle = textService.Resolve(TextTokens.InspectionDetailStatusTitle);
        DetailOnlineLabel = textService.Resolve(TextTokens.InspectionDetailOnlineLabel);
        DetailPlaybackLabel = textService.Resolve(TextTokens.InspectionDetailPlaybackLabel);
        DetailImageLabel = textService.Resolve(TextTokens.InspectionDetailImageLabel);
        DetailFaultTypeLabel = textService.Resolve(TextTokens.InspectionDetailFaultTypeLabel);
        DetailFaultNoteLabel = textService.Resolve(TextTokens.InspectionDetailFaultNoteLabel);
        PreviewTitle = textService.Resolve(TextTokens.InspectionPreviewTitle);
        PreviewDescription = textService.Resolve(TextTokens.InspectionPreviewDescription);
        PreviewPlayableBadge = textService.Resolve(TextTokens.InspectionPreviewPlayableBadge);
        PreviewUnavailableBadge = textService.Resolve(TextTokens.InspectionPreviewUnavailableBadge);
        PreviewPlayableHint = textService.Resolve(TextTokens.InspectionPreviewPlayableHint);
        PreviewUnavailableHint = textService.Resolve(TextTokens.InspectionPreviewUnavailableHint);
        PreviewPlayableCanvas = textService.Resolve(TextTokens.InspectionPreviewPlayableCanvas);
        PreviewUnavailableCanvas = textService.Resolve(TextTokens.InspectionPreviewUnavailableCanvas);
        CoordinateReserveActionText = textService.Resolve(TextTokens.InspectionCoordinateReserveAction);
        HistorySectionTitle = textService.Resolve(TextTokens.InspectionDetailHistoryTitle);
        HistorySectionDescription = textService.Resolve(TextTokens.InspectionRecordDescription);
        LastFaultLabel = textService.Resolve(TextTokens.InspectionDetailLastFaultLabel);
        DispatchEntryLabel = textService.Resolve(TextTokens.InspectionDetailDispatchEntryLabel);
        LastConclusionLabel = textService.Resolve(TextTokens.InspectionDetailLastConclusionLabel);
        UnmappedPointsTitle = textService.Resolve(TextTokens.InspectionUnmappedPointsTitle);
        UnmappedPointsDescription = textService.Resolve(TextTokens.InspectionUnmappedPointsDescription);

        ExecuteInspectionText = textService.Resolve(TextTokens.InspectionActionExecute);
        ViewHistoryText = textService.Resolve(TextTokens.InspectionActionHistory);
        OpenDispatchWorkspaceText = textService.Resolve(TextTokens.DispatchActionOpenWorkspace);
        OpenReportsCenterText = textService.Resolve(TextTokens.ReportsActionOpenCenter);
        InitializeReviewText(textService);

        SelectGroupCommand = new RelayCommand(parameter =>
        {
            if (parameter is InspectionGroupSummaryState group)
            {
                LoadGroup(group);
            }
        });

        SelectPointCommand = new RelayCommand(parameter =>
        {
            if (parameter is InspectionPointState point)
            {
                SelectPoint(point);
            }
            else if (parameter is MapPointState mapPoint)
            {
                SelectPoint(mapPoint.PointId);
            }
        });

        SelectRecentFaultCommand = new RelayCommand(parameter =>
        {
            if (parameter is RecentFaultSummaryState recentFault)
            {
                var point = Points.FirstOrDefault(candidate => candidate.Id == recentFault.PointId);
                if (point is not null)
                {
                    SelectPoint(point);
                }
            }
        });

        _executeInspectionCommand = new RelayCommand(_ => SimulateInspectionExecution(), _ => ExecutionState?.IsEnabled == true);
        _openDispatchWorkspaceCommand = new RelayCommand(_ => RequestNavigate(AppSectionId.Dispatch));
        _openReportsCenterCommand = new RelayCommand(_ => RequestNavigate(AppSectionId.Reports));
        ExecuteInspectionCommand = _executeInspectionCommand;
        OpenDispatchWorkspaceCommand = _openDispatchWorkspaceCommand;
        OpenReportsCenterCommand = _openReportsCenterCommand;
        ViewHistoryCommand = new RelayCommand(_ =>
        {
            if (ExecutionState is not null)
            {
                ExecutionState.SimulationNote = _textService.Resolve(TextTokens.InspectionHistoryPlaceholder);
            }
        });
        ToggleGroupCommand = new RelayCommand(_ => ToggleGroupEnabled());
        InitializeReviewCommands();

        _workspaceByGroupId = CreateWorkspaces(inspectionTaskService.GetWorkspace().Data);
        Groups = new ObservableCollection<InspectionGroupSummaryState>(_workspaceByGroupId.Values.Select(workspace => workspace.Group));
        LoadGroup(Groups.First());
    }

    public string GroupSectionTitle { get; }
    public string GroupSectionDescription { get; }
    public string CurrentGroupLabel { get; }
    public string AvailableGroupsLabel { get; }
    public string StrategySectionTitle { get; }
    public string StrategySectionDescription { get; }
    public string StrategyFirstRunLabel { get; }
    public string StrategyDailyRunsLabel { get; }
    public string StrategyIntervalLabel { get; }
    public string StrategyResultModeLabel { get; }
    public string StrategyDispatchModeLabel { get; }
    public string ExecutionSectionTitle { get; }
    public string ExecutionSectionDescription { get; }
    public string ExecutionExecutedTodayLabel { get; }
    public string ExecutionTaskStatusLabel { get; }
    public string ExecutionNextRunLabel { get; }
    public string ExecutionProgressLabel { get; }
    public string ExecutionSimulationLabel { get; }
    public string RecentFaultsSectionTitle { get; }
    public string RecentFaultsSectionDescription { get; }
    public string RecentFaultTimeLabel { get; }
    public string WorkbenchTitle { get; }
    public string WorkbenchDescription { get; }
    public string WorkbenchMapBadge { get; }
    public string WorkbenchHint { get; }
    public string RunSummaryStartedAtLabel { get; }
    public string RunSummaryTotalPointsLabel { get; }
    public string RunSummaryInspectedPointsLabel { get; }
    public string RunSummaryNormalLabel { get; }
    public string RunSummaryFaultLabel { get; }
    public string RunSummaryCurrentPointLabel { get; }
    public string LegendPendingText { get; }
    public string LegendActiveText { get; }
    public string LegendNormalText { get; }
    public string LegendFaultText { get; }
    public string LegendSilentText { get; }
    public string LegendPausedText { get; }
    public string DetailsSectionTitle { get; }
    public string DetailsSectionDescription { get; }
    public string DetailBasicTitle { get; }
    public string DetailPointNameLabel { get; }
    public string DetailUnitLabel { get; }
    public string DetailCurrentHandlingUnitLabel { get; }
    public string DetailCurrentStatusLabel { get; }
    public string DetailCoordinateLabel { get; }
    public string DetailStatusTitle { get; }
    public string DetailOnlineLabel { get; }
    public string DetailPlaybackLabel { get; }
    public string DetailImageLabel { get; }
    public string DetailFaultTypeLabel { get; }
    public string DetailFaultNoteLabel { get; }
    public string PreviewTitle { get; }
    public string PreviewDescription { get; }
    public string PreviewPlayableBadge { get; }
    public string PreviewUnavailableBadge { get; }
    public string PreviewPlayableHint { get; }
    public string PreviewUnavailableHint { get; }
    public string PreviewPlayableCanvas { get; }
    public string PreviewUnavailableCanvas { get; }
    public string CoordinateReserveActionText { get; }
    public string HistorySectionTitle { get; }
    public string HistorySectionDescription { get; }
    public string LastFaultLabel { get; }
    public string DispatchEntryLabel { get; }
    public string LastConclusionLabel { get; }
    public string UnmappedPointsTitle { get; }
    public string UnmappedPointsDescription { get; }
    public string ExecuteInspectionText { get; }
    public string ViewHistoryText { get; }
    public string OpenDispatchWorkspaceText { get; }
    public string OpenReportsCenterText { get; }
    public MapProviderSettings MapProvider { get; }

    public ObservableCollection<InspectionGroupSummaryState> Groups { get; }

    public InspectionGroupSummaryState? SelectedGroup
    {
        get => _selectedGroup;
        private set => SetProperty(ref _selectedGroup, value);
    }

    public InspectionStrategySummaryState? StrategySummary
    {
        get => _strategySummary;
        private set => SetProperty(ref _strategySummary, value);
    }

    public InspectionTaskExecutionState? ExecutionState
    {
        get => _executionState;
        private set => SetProperty(ref _executionState, value);
    }

    public InspectionRunSummaryState? RunSummary
    {
        get => _runSummary;
        private set => SetProperty(ref _runSummary, value);
    }

    public ObservableCollection<InspectionPointState> Points
    {
        get => _points;
        private set => SetProperty(ref _points, value);
    }

    public ObservableCollection<MapPointState> MapPoints
    {
        get => _mapPoints;
        private set => SetProperty(ref _mapPoints, value);
    }

    public ObservableCollection<MapPointState> UnmappedPoints
    {
        get => _unmappedPoints;
        private set => SetProperty(ref _unmappedPoints, value);
    }

    public ObservableCollection<RecentFaultSummaryState> RecentFaults
    {
        get => _recentFaults;
        private set => SetProperty(ref _recentFaults, value);
    }

    public InspectionPointDetailState? SelectedPointDetail
    {
        get => _selectedPointDetail;
        private set => SetProperty(ref _selectedPointDetail, value);
    }

    public InspectionPointState? SelectedPoint
    {
        get => _selectedPoint;
        private set => SetProperty(ref _selectedPoint, value);
    }

    public string SelectedMapPointId
    {
        get => _selectedMapPointId;
        private set => SetProperty(ref _selectedMapPointId, value);
    }

    public string ToggleGroupActionText
    {
        get => _toggleGroupActionText;
        private set => SetProperty(ref _toggleGroupActionText, value);
    }

    public ICommand SelectGroupCommand { get; }
    public ICommand SelectPointCommand { get; }
    public ICommand SelectRecentFaultCommand { get; }
    public ICommand ExecuteInspectionCommand { get; }
    public ICommand OpenDispatchWorkspaceCommand { get; }
    public ICommand OpenReportsCenterCommand { get; }
    public ICommand ViewHistoryCommand { get; }
    public ICommand ToggleGroupCommand { get; }
    public ICommand OpenReviewWallCommand { get; private set; } = null!;
    public ICommand ReturnToInspectionWorkspaceCommand { get; private set; } = null!;
    public ICommand ConfirmReviewCompletedCommand { get; private set; } = null!;
    public ICommand SelectReviewCardCommand { get; private set; } = null!;
    public ICommand MarkSelectedReviewCommand { get; private set; } = null!;
    public ICommand SelectReviewQuickFilterCommand { get; private set; } = null!;

    private void LoadGroup(InspectionGroupSummaryState group)
    {
        foreach (var item in Groups)
        {
            item.IsSelected = item.Id == group.Id;
        }

        var workspace = _workspaceByGroupId[group.Id];

        SelectedGroup = group;
        StrategySummary = workspace.StrategySummary;
        ExecutionState = workspace.ExecutionState;
        RunSummary = workspace.RunSummary;
        Points = workspace.Points;
        MapPoints = workspace.MapPoints;
        UnmappedPoints = new ObservableCollection<MapPointState>(workspace.MapPoints.Where(point => !point.CanRenderOnMap));
        RecentFaults = workspace.RecentFaults;

        ToggleGroupActionText = group.IsEnabled
            ? _textService.Resolve(TextTokens.InspectionActionDisable)
            : _textService.Resolve(TextTokens.InspectionActionEnable);

        _executeInspectionCommand.RaiseCanExecuteChanged();

        var initialPoint = Points.FirstOrDefault(point => point.IsCurrent)
            ?? Points.FirstOrDefault(point => point.Status == InspectionPointStatus.Fault)
            ?? Points.FirstOrDefault();

        if (initialPoint is not null)
        {
            SelectPoint(initialPoint);
        }

        RefreshSummary(workspace);
        LoadReviewState(workspace, refreshFromPoints: false);
    }

    private void SelectPoint(InspectionPointState point)
    {
        foreach (var candidate in Points)
        {
            candidate.IsSelected = candidate.Id == point.Id;
        }

        foreach (var mapPoint in MapPoints)
        {
            mapPoint.IsSelected = mapPoint.PointId == point.Id;
            mapPoint.IsCurrent = mapPoint.PointId == point.Id && point.IsCurrent;
        }

        foreach (var fault in RecentFaults)
        {
            fault.IsSelected = fault.PointId == point.Id;
        }

        SelectedPoint = point;
        SelectedMapPointId = point.Id;
        SelectedPointDetail = CreatePointDetail(point);
    }

    private void SelectPoint(string pointId)
    {
        var point = Points.FirstOrDefault(candidate => candidate.Id == pointId);
        if (point is not null)
        {
            SelectPoint(point);
        }
    }

    private void SimulateInspectionExecution()
    {
        if (SelectedGroup is null || !_workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace))
        {
            return;
        }

        var activePoint = workspace.Points.FirstOrDefault(point => point.IsCurrent);
        if (activePoint is not null)
        {
            activePoint.IsCurrent = false;
            if (activePoint.Status == InspectionPointStatus.Inspecting)
            {
                activePoint.Status = activePoint.CompletionStatus;
                activePoint.IsSelected = false;
                EnsureFaultSummary(activePoint, workspace.RecentFaults);
            }

            SyncMapPoint(activePoint, workspace.MapPoints);
        }

        var nextPoint = workspace.Points.FirstOrDefault(point => point.Status == InspectionPointStatus.Pending);
        if (nextPoint is not null)
        {
            nextPoint.Status = InspectionPointStatus.Inspecting;
            nextPoint.IsCurrent = true;
            workspace.ExecutionState.CurrentTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusRunning);
            workspace.ExecutionState.SimulationNote = _textService.Resolve(TextTokens.InspectionSimulationTriggered);
            SyncMapPoint(nextPoint, workspace.MapPoints);
            SelectPoint(nextPoint);
        }
        else
        {
            workspace.ExecutionState.CurrentTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusCompleted);
            workspace.ExecutionState.SimulationNote = _textService.Resolve(TextTokens.InspectionSimulationCompleted);

            if (activePoint is not null)
            {
                SelectPoint(activePoint);
            }
        }

        RefreshSummary(workspace);
        RefreshReviewStateAfterSimulation(workspace);
    }

    private void ToggleGroupEnabled()
    {
        if (SelectedGroup is null || !_workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace))
        {
            return;
        }

        SelectedGroup.IsEnabled = !SelectedGroup.IsEnabled;
        workspace.ExecutionState.IsEnabled = SelectedGroup.IsEnabled;
        workspace.ExecutionState.CurrentTaskStatus = SelectedGroup.IsEnabled
            ? _textService.Resolve(TextTokens.InspectionTaskStatusIdle)
            : _textService.Resolve(TextTokens.InspectionTaskStatusPaused);

        ToggleGroupActionText = SelectedGroup.IsEnabled
            ? _textService.Resolve(TextTokens.InspectionActionDisable)
            : _textService.Resolve(TextTokens.InspectionActionEnable);

        _executeInspectionCommand.RaiseCanExecuteChanged();
    }

    private void EnsureFaultSummary(InspectionPointState point, ObservableCollection<RecentFaultSummaryState> recentFaults)
    {
        if (point.Status != InspectionPointStatus.Fault && point.Status != InspectionPointStatus.PausedUntilRecovery)
        {
            return;
        }

        if (recentFaults.Any(item => item.PointId == point.Id))
        {
            return;
        }

        recentFaults.Insert(0, new RecentFaultSummaryState(point.Id, point.Name, point.FaultType, point.LastFaultTime));
    }

    private void RefreshSummary(GroupWorkspaceState workspace)
    {
        var totalPoints = workspace.Points.Count;
        var normalCount = workspace.Points.Count(point => point.Status == InspectionPointStatus.Normal);
        var faultCount = workspace.Points.Count(point => point.Status == InspectionPointStatus.Fault);
        var inspectedCount = workspace.Points.Count(point =>
            point.Status is InspectionPointStatus.Normal or InspectionPointStatus.Fault or InspectionPointStatus.Inspecting);
        var inspectableCount = Math.Max(1, workspace.Points.Count(point =>
            point.Status is not InspectionPointStatus.Silent and not InspectionPointStatus.PausedUntilRecovery));

        workspace.RunSummary.GroupName = workspace.Group.Name;
        workspace.RunSummary.TotalPoints = totalPoints.ToString();
        workspace.RunSummary.InspectedPoints = inspectedCount.ToString();
        workspace.RunSummary.NormalCount = normalCount.ToString();
        workspace.RunSummary.FaultCount = faultCount.ToString();
        workspace.RunSummary.CurrentPointName = workspace.Points.FirstOrDefault(point => point.IsCurrent)?.Name ?? "--";

        workspace.ExecutionState.CurrentProgressValue = Math.Round(inspectedCount * 100d / inspectableCount, 0);
        workspace.ExecutionState.CurrentProgressText = $"{inspectedCount} / {inspectableCount}";
    }

    private InspectionPointDetailState CreatePointDetail(InspectionPointState point)
    {
        return new InspectionPointDetailState(
            point.Name,
            point.UnitName,
            point.CurrentHandlingUnit,
            ResolvePointStatus(point.Status),
            point.CoordinateStatusText,
            point.OnlineStatus,
            point.PlaybackStatus,
            point.ImageStatus,
            point.FaultType,
            point.FaultDescription,
            point.IsPreviewAvailable,
            !point.CanRenderOnMap,
            point.LastFaultTime,
            point.DispatchPoolEntry,
            point.LastInspectionConclusion);
    }

    private string ResolvePointStatus(InspectionPointStatus status)
    {
        return status switch
        {
            InspectionPointStatus.Pending => _textService.Resolve(TextTokens.InspectionStatusPending),
            InspectionPointStatus.Inspecting => _textService.Resolve(TextTokens.InspectionStatusInspecting),
            InspectionPointStatus.Normal => _textService.Resolve(TextTokens.InspectionStatusNormal),
            InspectionPointStatus.Fault => _textService.Resolve(TextTokens.InspectionStatusFault),
            InspectionPointStatus.Silent => _textService.Resolve(TextTokens.InspectionStatusSilent),
            InspectionPointStatus.PausedUntilRecovery => _textService.Resolve(TextTokens.InspectionStatusPausedUntilRecovery),
            _ => string.Empty
        };
    }

    private Dictionary<string, GroupWorkspaceState> CreateWorkspaces(InspectionWorkspaceSnapshot snapshot)
    {
        return snapshot.Groups
            .Select(workspace =>
            {
                var inspectionPoints = new ObservableCollection<InspectionPointState>(workspace.Points.Select(CreatePoint));
                var mapPoints = new ObservableCollection<MapPointState>(inspectionPoints.Select(CreateMapPoint));

                return new GroupWorkspaceState(
                    new InspectionGroupSummaryState(
                        workspace.Group.Id,
                        workspace.Group.Name,
                        workspace.Group.Summary,
                        workspace.Group.IsEnabled),
                    new InspectionStrategySummaryState(
                        workspace.Strategy.FirstRunTime,
                        workspace.Strategy.DailyExecutionCount,
                        workspace.Strategy.Interval,
                        workspace.Strategy.ResultMode,
                        workspace.Strategy.DispatchMode),
                    CreateExecutionState(workspace.Execution),
                    CreateRunSummary(workspace.RunSummary),
                    workspace.TaskFinishedAt,
                    inspectionPoints,
                    mapPoints,
                    new ObservableCollection<RecentFaultSummaryState>(workspace.RecentFaults.Select(CreateRecentFault)));
            })
            .ToDictionary(workspace => workspace.Group.Id, workspace => workspace);
    }

    private InspectionTaskExecutionState CreateExecutionState(InspectionExecutionModel execution)
    {
        return new InspectionTaskExecutionState
        {
            ExecutedToday = execution.ExecutedToday,
            CurrentTaskStatus = execution.CurrentTaskStatus,
            NextRunTime = execution.NextRunTime,
            CurrentProgressText = "0 / 0",
            CurrentProgressValue = 0,
            SimulationNote = execution.SimulationNote,
            IsEnabled = execution.IsEnabled
        };
    }

    private InspectionRunSummaryState CreateRunSummary(InspectionRunSummaryModel runSummary)
    {
        return new InspectionRunSummaryState
        {
            GroupName = runSummary.GroupName,
            StartedAt = runSummary.StartedAt,
            TotalPoints = "0",
            InspectedPoints = "0",
            NormalCount = "0",
            FaultCount = "0",
            CurrentPointName = "--"
        };
    }

    private RecentFaultSummaryState CreateRecentFault(InspectionRecentFaultModel recentFault)
        => new(recentFault.PointId, recentFault.PointName, recentFault.FaultType, recentFault.LatestFaultTime);

    private InspectionTaskExecutionState CreateExecutionState(string executedToday, string taskStatus, string nextRunTime, bool isEnabled)
    {
        return new InspectionTaskExecutionState
        {
            ExecutedToday = executedToday,
            CurrentTaskStatus = taskStatus,
            NextRunTime = nextRunTime,
            CurrentProgressText = "0 / 0",
            CurrentProgressValue = 0,
            SimulationNote = _textService.Resolve(TextTokens.InspectionHistoryPlaceholder),
            IsEnabled = isEnabled
        };
    }

    private InspectionRunSummaryState CreateRunSummary(string groupName, string startedAt)
    {
        return new InspectionRunSummaryState
        {
            GroupName = groupName,
            StartedAt = startedAt,
            TotalPoints = "0",
            InspectedPoints = "0",
            NormalCount = "0",
            FaultCount = "0",
            CurrentPointName = "--"
        };
    }

    private InspectionPointState CreatePoint(InspectionPointModel point)
    {
        return CreatePoint(
            point.Id,
            point.DeviceCode,
            point.Name,
            point.UnitName,
            point.CurrentHandlingUnit,
            point.Longitude,
            point.Latitude,
            point.CanRenderOnMap,
            point.CoordinateStatusText,
            point.X,
            point.Y,
            MapPointStatus(point.Status),
            MapPointStatus(point.CompletionStatus),
            point.IsOnline,
            point.IsPlayable,
            point.IsImageAbnormal,
            point.IsPreviewAvailable,
            point.FaultSummary,
            point.LastFaultTime,
            point.EntersDispatchPool);
    }

    private InspectionPointState CreatePoint(
        string id,
        string deviceCode,
        string name,
        string unitName,
        string currentHandlingUnit,
        double longitude,
        double latitude,
        bool canRenderOnMap,
        string coordinateStatusText,
        double x,
        double y,
        InspectionPointStatus status,
        InspectionPointStatus completionStatus,
        bool isOnline,
        bool isPlayable,
        bool isImageAbnormal,
        bool isPreviewAvailable,
        string faultSummary,
        string lastFaultTime,
        bool entersDispatchPool)
    {
        var faultType = ResolveFaultType(status, completionStatus, isOnline, isPlayable, isImageAbnormal);

        return new InspectionPointState(
            id,
            deviceCode,
            name,
            unitName,
            currentHandlingUnit,
            longitude,
            latitude,
            canRenderOnMap,
            coordinateStatusText,
            x,
            y,
            status,
            completionStatus,
            isOnline ? _textService.Resolve(TextTokens.InspectionOnlineOnline) : _textService.Resolve(TextTokens.InspectionOnlineOffline),
            isPlayable ? _textService.Resolve(TextTokens.InspectionPlaybackPlayable) : _textService.Resolve(TextTokens.InspectionPlaybackFailed),
            isImageAbnormal ? _textService.Resolve(TextTokens.InspectionImageAbnormal) : _textService.Resolve(TextTokens.InspectionImageNormal),
            faultType,
            BuildFaultDescription(status, completionStatus, isOnline, isPlayable, isImageAbnormal, faultSummary, canRenderOnMap, coordinateStatusText),
            lastFaultTime,
            entersDispatchPool ? _textService.Resolve(TextTokens.InspectionDispatchPoolYes) : _textService.Resolve(TextTokens.InspectionDispatchPoolNo),
            ResolveConclusion(status, completionStatus),
            isPreviewAvailable)
        {
            IsCurrent = status == InspectionPointStatus.Inspecting
        };
    }

    private static InspectionPointStatus MapPointStatus(InspectionPointStatusModel status)
    {
        return status switch
        {
            InspectionPointStatusModel.Pending => InspectionPointStatus.Pending,
            InspectionPointStatusModel.Inspecting => InspectionPointStatus.Inspecting,
            InspectionPointStatusModel.Normal => InspectionPointStatus.Normal,
            InspectionPointStatusModel.Fault => InspectionPointStatus.Fault,
            InspectionPointStatusModel.Silent => InspectionPointStatus.Silent,
            InspectionPointStatusModel.PausedUntilRecovery => InspectionPointStatus.PausedUntilRecovery,
            _ => InspectionPointStatus.Pending
        };
    }

    private string ResolveFaultType(
        InspectionPointStatus status,
        InspectionPointStatus completionStatus,
        bool isOnline,
        bool isPlayable,
        bool isImageAbnormal)
    {
        var effectiveStatus = status == InspectionPointStatus.Pending ? completionStatus : status;
        if (effectiveStatus is InspectionPointStatus.Normal or InspectionPointStatus.Silent)
        {
            return _textService.Resolve(TextTokens.InspectionFaultTypeNone);
        }

        if (!isOnline)
        {
            return _textService.Resolve(TextTokens.InspectionFaultTypeOffline);
        }

        if (!isPlayable)
        {
            return _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed);
        }

        if (isImageAbnormal)
        {
            return _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal);
        }

        return _textService.Resolve(TextTokens.InspectionFaultTypeNone);
    }

    private string BuildFaultDescription(
        InspectionPointStatus status,
        InspectionPointStatus completionStatus,
        bool isOnline,
        bool isPlayable,
        bool isImageAbnormal,
        string faultSummary,
        bool canRenderOnMap,
        string coordinateStatusText)
    {
        var effectiveStatus = status == InspectionPointStatus.Pending ? completionStatus : status;

        if (!string.IsNullOrWhiteSpace(faultSummary)
            && !string.Equals(faultSummary, "无故障", StringComparison.Ordinal))
        {
            return faultSummary;
        }

        if (!canRenderOnMap)
        {
            return $"当前点位暂不可落图：{coordinateStatusText}。";
        }

        if (effectiveStatus == InspectionPointStatus.Silent)
        {
            return _textService.Resolve(TextTokens.InspectionStatusSilent);
        }

        if (effectiveStatus == InspectionPointStatus.PausedUntilRecovery)
        {
            return _textService.Resolve(TextTokens.InspectionStatusPausedUntilRecovery);
        }

        if (!isOnline)
        {
            return "设备当前离线，后续应展示接口重试结果与责任单位跟进信息。";
        }

        if (!isPlayable)
        {
            return "播放检查失败，后续在此承接重试与协议切换过程说明。";
        }

        if (isImageAbnormal)
        {
            return "画面疑似异常，后续接入接口判定结果与本地截图分析说明。";
        }

        return "当前点位状态正常，本轮用于展示列表与中台联动骨架。";
    }

    private string ResolveConclusion(InspectionPointStatus status, InspectionPointStatus completionStatus)
    {
        var effectiveStatus = status == InspectionPointStatus.Pending ? completionStatus : status;

        return effectiveStatus is InspectionPointStatus.Fault or InspectionPointStatus.PausedUntilRecovery
            ? _textService.Resolve(TextTokens.InspectionConclusionFault)
            : _textService.Resolve(TextTokens.InspectionConclusionNormal);
    }

    private MapPointState CreateMapPoint(InspectionPointState point)
    {
        return new MapPointState(
            point.Id,
            point.DeviceCode,
            point.Name,
            point.UnitName,
            point.CurrentHandlingUnit,
            point.Longitude,
            point.Latitude,
            point.CanRenderOnMap,
            point.CoordinateStatusText,
            point.X,
            point.Y,
            ToMapVisualKind(point.Status),
            ResolvePointStatus(point.Status),
            point.FaultType,
            point.FaultDescription,
            point.LastFaultTime,
            point.IsPreviewAvailable)
        {
            IsCurrent = point.IsCurrent
        };
    }

    private void SyncMapPoint(InspectionPointState point, ObservableCollection<MapPointState> mapPoints)
    {
        var mapPoint = mapPoints.FirstOrDefault(candidate => candidate.PointId == point.Id);
        if (mapPoint is null)
        {
            return;
        }

        mapPoint.VisualKind = ToMapVisualKind(point.Status);
        mapPoint.StatusText = ResolvePointStatus(point.Status);
        mapPoint.FaultType = point.FaultType;
        mapPoint.Summary = point.FaultDescription;
        mapPoint.LatestFaultTime = point.LastFaultTime;
        mapPoint.IsCurrent = point.IsCurrent;
    }

    private static MapPointVisualKind ToMapVisualKind(InspectionPointStatus status)
    {
        return status switch
        {
            InspectionPointStatus.Inspecting => MapPointVisualKind.Inspecting,
            InspectionPointStatus.Fault => MapPointVisualKind.Fault,
            InspectionPointStatus.Silent => MapPointVisualKind.Silent,
            InspectionPointStatus.PausedUntilRecovery => MapPointVisualKind.Paused,
            _ => MapPointVisualKind.Normal
        };
    }

    private sealed class GroupWorkspaceState
    {
        public GroupWorkspaceState(
            InspectionGroupSummaryState group,
            InspectionStrategySummaryState strategySummary,
            InspectionTaskExecutionState executionState,
            InspectionRunSummaryState runSummary,
            string taskFinishedAt,
            ObservableCollection<InspectionPointState> points,
            ObservableCollection<MapPointState> mapPoints,
            ObservableCollection<RecentFaultSummaryState> recentFaults)
        {
            Group = group;
            StrategySummary = strategySummary;
            ExecutionState = executionState;
            RunSummary = runSummary;
            TaskFinishedAt = taskFinishedAt;
            Points = points;
            MapPoints = mapPoints;
            RecentFaults = recentFaults;
        }

        public InspectionGroupSummaryState Group { get; }
        public InspectionStrategySummaryState StrategySummary { get; }
        public InspectionTaskExecutionState ExecutionState { get; }
        public InspectionRunSummaryState RunSummary { get; }
        public string TaskFinishedAt { get; }
        public ObservableCollection<InspectionPointState> Points { get; }
        public ObservableCollection<MapPointState> MapPoints { get; }
        public ObservableCollection<RecentFaultSummaryState> RecentFaults { get; }
        public InspectionReviewTaskSummaryState? ReviewSummary { get; set; }
        public ObservableCollection<InspectionReviewCardState>? ReviewCards { get; set; }
        public InspectionReviewFilterState? ReviewFilter { get; set; }
    }
}
