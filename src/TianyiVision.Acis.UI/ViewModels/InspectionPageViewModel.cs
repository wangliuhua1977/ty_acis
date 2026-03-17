using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Inspection;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class InspectionPageViewModel : PageViewModelBase
{
    private readonly PointSelectionContext _pointSelectionContext;
    private readonly ITextService _textService;
    private readonly IInspectionTaskService _inspectionTaskService;
    private Dictionary<string, GroupWorkspaceState> _workspaceByGroupId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedScopePlanIdByGroupId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InspectionPointPreviewSessionModel> _previewSessionByPointId = new(StringComparer.Ordinal);
    private readonly RelayCommand _selectScopePlanCommand;
    private readonly RelayCommand _executeInspectionCommand;
    private readonly RelayCommand _saveCurrentScopePlanCommand;
    private readonly RelayCommand _startSinglePointInspectionCommand;
    private readonly RelayCommand _openDispatchWorkspaceCommand;
    private readonly RelayCommand _openReportsCenterCommand;
    private readonly RelayCommand _selectFirstUnmappedPointCommand;
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
    private InspectionTaskSummaryState? _currentTask;
    private ObservableCollection<InspectionTaskSummaryState> _recentTasks = [];
    private PointBusinessSummaryState? _selectedPointSummary;
    private InspectionPointDetailState? _selectedPointDetail;
    private InspectionPointState? _selectedPoint;
    private InspectionPointState? _selectedScopePoint;
    private MapPointState? _selectedMapPoint;
    private RecentFaultSummaryState? _selectedRecentFault;
    private CancellationTokenSource? _selectedPointPreviewCts;
    private long _selectedPointPreviewToken;
    private string _currentPointSourceType = string.Empty;
    private string _toggleGroupActionText = string.Empty;
    private string _selectedMapPointId = string.Empty;
    private bool _isRealMapAvailable;
    private bool _isMapAvailabilityKnown;
    private string _mapAvailabilityBadgeText = string.Empty;
    private string _singlePointInspectionPointName = "暂无记录";
    private string _singlePointInspectionTaskStatus = "暂无记录";
    private string _singlePointInspectionLastTime = "暂无记录";
    private string _singlePointInspectionResultSummary = "暂无记录";
    private string _taskRoutingSummary = string.Empty;
    private string _reviewWallEntrySummary = string.Empty;
    private string _dispatchPoolCandidateSummary = string.Empty;
    private InspectionScopePlanPreviewModel _scopePlanPreview = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, string.Empty);
    private ObservableCollection<InspectionScopePlanOptionState> _availableScopePlans = [];
    private ObservableCollection<InspectionPointState> _scopeMatchedPoints = [];
    private ObservableCollection<InspectionPointState> _scopeUnmatchedPoints = [];
    private string _currentViewingScopePlanName = "--";
    private string _currentExecutionScopePlanName = "--";
    private string _scopePlanAlignmentSummary = string.Empty;
    private string _scopePlanSaveFeedback = string.Empty;
    private string _scopePlanFallbackHint = string.Empty;
    private string _scopeSaveButtonText = string.Empty;
    private ScopePlanSaveVisualState _scopePlanSaveVisualState = ScopePlanSaveVisualState.Idle;

    public InspectionPageViewModel(
        ITextService textService,
        IInspectionTaskService inspectionTaskService,
        PointSelectionContext pointSelectionContext,
        MapProviderSettings mapProvider)
        : base(
            textService.Resolve(TextTokens.InspectionTitle),
            textService.Resolve(TextTokens.InspectionDescription))
    {
        _pointSelectionContext = pointSelectionContext;
        _textService = textService;
        _inspectionTaskService = inspectionTaskService;
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

        TasksSectionTitle = textService.Resolve(TextTokens.InspectionTasksTitle);
        TasksSectionDescription = textService.Resolve(TextTokens.InspectionTasksDescription);
        TaskNameLabel = textService.Resolve(TextTokens.InspectionTaskNameLabel);
        TaskTypeLabel = textService.Resolve(TextTokens.InspectionTaskTypeLabel);
        TaskTriggerLabel = textService.Resolve(TextTokens.InspectionTaskTriggerLabel);
        TaskScopeLabel = textService.Resolve(TextTokens.InspectionTaskScopeLabel);
        TaskCurrentPointLabel = textService.Resolve(TextTokens.InspectionTaskCurrentPointLabel);
        TaskSummaryLabel = textService.Resolve(TextTokens.InspectionTaskSummaryLabel);
        TaskRecentListLabel = textService.Resolve(TextTokens.InspectionTaskRecentListLabel);
        TaskTimeLabel = textService.Resolve(TextTokens.InspectionTaskTimeLabel);
        TaskEmptyText = textService.Resolve(TextTokens.InspectionTaskEmptyText);

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
        ScopePreviewTitle = textService.Resolve(TextTokens.InspectionScopePreviewTitle);
        ScopePreviewDescription = textService.Resolve(TextTokens.InspectionScopePreviewDescription);
        ScopePlanSelectionLabel = textService.Resolve(TextTokens.InspectionScopeSelectionLabel);
        ScopePlanNameLabel = textService.Resolve(TextTokens.InspectionScopePlanNameLabel);
        ScopeMatchedCountLabel = textService.Resolve(TextTokens.InspectionScopeMatchedCountLabel);
        ScopeUnmatchedCountLabel = textService.Resolve(TextTokens.InspectionScopeUnmatchedCountLabel);
        ScopeRuleSummaryLabel = textService.Resolve(TextTokens.InspectionScopeRuleSummaryLabel);
        ScopeExecutionSummaryLabel = textService.Resolve(TextTokens.InspectionScopeExecutionSummaryLabel);
        ScopeUnmatchedReasonLabel = textService.Resolve(TextTokens.InspectionScopeUnmatchedReasonLabel);
        ScopeMatchedListLabel = textService.Resolve(TextTokens.InspectionScopeMatchedListLabel);
        ScopeUnmatchedListLabel = textService.Resolve(TextTokens.InspectionScopeUnmatchedListLabel);
        ScopeCurrentViewingPlanLabel = textService.Resolve(TextTokens.InspectionScopeCurrentViewingPlanLabel);
        ScopeCurrentExecutionPlanLabel = textService.Resolve(TextTokens.InspectionScopeCurrentExecutionPlanLabel);
        ScopeReadonlyHint = textService.Resolve(TextTokens.InspectionScopeReadonlyHint);
        ScopeSaveActionText = textService.Resolve(TextTokens.InspectionScopeSaveAction);
        ScopeSaveButtonText = ScopeSaveActionText;
        WorkbenchMapBadge = textService.Resolve(TextTokens.InspectionWorkbenchMapBadge);
        WorkbenchHint = textService.Resolve(TextTokens.InspectionWorkbenchHint);
        MapModeRealText = textService.Resolve(TextTokens.InspectionMapModeReal);
        MapModeFallbackText = textService.Resolve(TextTokens.InspectionMapModeFallback);
        MapOverlayReserveActionText = textService.Resolve(TextTokens.InspectionMapOverlayReserveAction);
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
        DetailLastSyncLabel = textService.Resolve(TextTokens.InspectionDetailLastSyncLabel);
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
        SinglePointInspectionTitle = "单点巡检";
        SinglePointInspectionActionText = "发起巡检";
        SinglePointInspectionPointNameLabel = "当前点位名称";
        SinglePointInspectionTaskStatusLabel = "当前任务状态";
        SinglePointInspectionLastTimeLabel = "最近一次巡检时间";
        SinglePointInspectionResultSummaryLabel = "最近一次结果摘要";
        SinglePointInspectionTitle = textService.Resolve(TextTokens.InspectionSinglePointTitle);
        SinglePointInspectionActionText = textService.Resolve(TextTokens.InspectionSinglePointActionText);
        SinglePointInspectionPointNameLabel = textService.Resolve(TextTokens.InspectionSinglePointPointNameLabel);
        SinglePointInspectionTaskStatusLabel = textService.Resolve(TextTokens.InspectionSinglePointTaskStatusLabel);
        SinglePointInspectionLastTimeLabel = textService.Resolve(TextTokens.InspectionSinglePointLastTimeLabel);
        SinglePointInspectionResultSummaryLabel = textService.Resolve(TextTokens.InspectionSinglePointResultSummaryLabel);
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
            else if (parameter is string pointId)
            {
                SelectPoint(pointId);
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
        _selectScopePlanCommand = new RelayCommand(parameter =>
        {
            if (parameter is not InspectionScopePlanOptionState option)
            {
                return;
            }

            var group = SelectedGroup;
            if (group is null)
            {
                return;
            }

            var groupId = group.Id;
            if (!_workspaceByGroupId.TryGetValue(groupId, out var workspace)
                || workspace is null)
            {
                return;
            }

            ClearScopePlanSaveFeedback();
            ApplyScopePlanSelection(workspace, option.PlanId);
        }, _ => !IsScopePlanSavePending);

        SelectScopePlanCommand = _selectScopePlanCommand;
        _saveCurrentScopePlanCommand = new RelayCommand(_ => SaveCurrentScopePlan(), _ => CanSaveCurrentScopePlan);
        _executeInspectionCommand = new RelayCommand(_ => StartGroupInspectionAsyncSafe(), _ => ExecutionState?.IsEnabled == true);
        _startSinglePointInspectionCommand = new RelayCommand(_ => StartSinglePointInspectionAsyncSafe(), _ => CanStartSinglePointInspection);
        _openDispatchWorkspaceCommand = new RelayCommand(_ => RequestNavigate(AppSectionId.Dispatch));
        _openReportsCenterCommand = new RelayCommand(_ => RequestNavigate(AppSectionId.Reports));
        _selectFirstUnmappedPointCommand = new RelayCommand(
            _ =>
            {
                var firstUnmapped = UnmappedPoints.FirstOrDefault();
                if (firstUnmapped is not null)
                {
                    SelectPoint(firstUnmapped.PointId);
                }
            },
            _ => UnmappedPoints.Count > 0);
        ExecuteInspectionCommand = _executeInspectionCommand;
        StartSinglePointInspectionCommand = _startSinglePointInspectionCommand;
        OpenDispatchWorkspaceCommand = _openDispatchWorkspaceCommand;
        OpenReportsCenterCommand = _openReportsCenterCommand;
        SelectFirstUnmappedPointCommand = _selectFirstUnmappedPointCommand;
        SaveCurrentScopePlanCommand = _saveCurrentScopePlanCommand;
        ViewHistoryCommand = new RelayCommand(_ =>
        {
            RequestRefreshWorkspace(SelectedGroup?.Id, SelectedPoint?.Id);
        });
        ToggleGroupCommand = new RelayCommand(_ => ToggleGroupEnabled());
        InitializeReviewCommands();

        _workspaceByGroupId = CreateWorkspaces(inspectionTaskService.GetWorkspace().Data);
        Groups = new ObservableCollection<InspectionGroupSummaryState>(_workspaceByGroupId.Values.Select(workspace => workspace.Group));
        _inspectionTaskService.TaskBoardChanged += HandleTaskBoardChanged;
        LoadGroup(Groups.First(), _pointSelectionContext.CurrentSummary?.PointId);
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
    public string TasksSectionTitle { get; }
    public string TasksSectionDescription { get; }
    public string TaskNameLabel { get; }
    public string TaskTypeLabel { get; }
    public string TaskTriggerLabel { get; }
    public string TaskScopeLabel { get; }
    public string TaskCurrentPointLabel { get; }
    public string TaskSummaryLabel { get; }
    public string TaskRecentListLabel { get; }
    public string TaskTimeLabel { get; }
    public string TaskEmptyText { get; }
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
    public string ScopePreviewTitle { get; }
    public string ScopePreviewDescription { get; }
    public string ScopePlanSelectionLabel { get; }
    public string ScopePlanNameLabel { get; }
    public string ScopeMatchedCountLabel { get; }
    public string ScopeUnmatchedCountLabel { get; }
    public string ScopeRuleSummaryLabel { get; }
    public string ScopeExecutionSummaryLabel { get; }
    public string ScopeUnmatchedReasonLabel { get; }
    public string ScopeMatchedListLabel { get; }
    public string ScopeUnmatchedListLabel { get; }
    public string ScopeCurrentViewingPlanLabel { get; }
    public string ScopeCurrentExecutionPlanLabel { get; }
    public string ScopeReadonlyHint { get; }
    public string ScopeSaveActionText { get; }
    public string WorkbenchMapBadge { get; }
    public string WorkbenchHint { get; }
    public string MapModeRealText { get; }
    public string MapModeFallbackText { get; }
    public string MapOverlayReserveActionText { get; }
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
    public string DetailLastSyncLabel { get; }
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
    public string SinglePointInspectionTitle { get; }
    public string SinglePointInspectionActionText { get; }
    public string SinglePointInspectionPointNameLabel { get; }
    public string SinglePointInspectionTaskStatusLabel { get; }
    public string SinglePointInspectionLastTimeLabel { get; }
    public string SinglePointInspectionResultSummaryLabel { get; }
    public MapProviderSettings MapProvider { get; }

    public ObservableCollection<InspectionGroupSummaryState> Groups { get; }

    public InspectionGroupSummaryState? SelectedGroup
    {
        get => _selectedGroup;
        private set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                OnPropertyChanged(nameof(CanStartSinglePointInspection));
                _startSinglePointInspectionCommand.RaiseCanExecuteChanged();
            }
        }
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
        private set
        {
            if (SetProperty(ref _unmappedPoints, value))
            {
                OnPropertyChanged(nameof(HasUnmappedPoints));
                OnPropertyChanged(nameof(UnmappedPointCount));
                _selectFirstUnmappedPointCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<RecentFaultSummaryState> RecentFaults
    {
        get => _recentFaults;
        private set => SetProperty(ref _recentFaults, value);
    }

    public InspectionTaskSummaryState? CurrentTask
    {
        get => _currentTask;
        private set => SetProperty(ref _currentTask, value);
    }

    public ObservableCollection<InspectionTaskSummaryState> RecentTasks
    {
        get => _recentTasks;
        private set => SetProperty(ref _recentTasks, value);
    }

    public InspectionPointDetailState? SelectedPointDetail
    {
        get => _selectedPointDetail;
        private set
        {
            if (SetProperty(ref _selectedPointDetail, value))
            {
                OnPropertyChanged(nameof(HasSelectedPoint));
                OnPropertyChanged(nameof(CanStartSinglePointInspection));
                _startSinglePointInspectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public PointBusinessSummaryState? SelectedPointSummary
    {
        get => _selectedPointSummary;
        private set => SetProperty(ref _selectedPointSummary, value);
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

    public string CurrentPointSourceType
    {
        get => _currentPointSourceType;
        private set
        {
            if (SetProperty(ref _currentPointSourceType, value))
            {
                OnPropertyChanged(nameof(CanStartSinglePointInspection));
                _startSinglePointInspectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsRealMapAvailable
    {
        get => _isRealMapAvailable;
        private set => SetProperty(ref _isRealMapAvailable, value);
    }

    public bool IsMapAvailabilityKnown
    {
        get => _isMapAvailabilityKnown;
        private set => SetProperty(ref _isMapAvailabilityKnown, value);
    }

    public string MapAvailabilityBadgeText
    {
        get => _mapAvailabilityBadgeText;
        private set => SetProperty(ref _mapAvailabilityBadgeText, value);
    }

    public bool HasSelectedPoint => SelectedPointDetail is not null;

    public bool CanStartSinglePointInspection
        => HasSelectedPoint
            && SelectedGroup?.IsEnabled == true;

    public bool HasUnmappedPoints => UnmappedPoints.Count > 0;

    public int UnmappedPointCount => UnmappedPoints.Count;

    public string SinglePointInspectionPointName
    {
        get => _singlePointInspectionPointName;
        private set => SetProperty(ref _singlePointInspectionPointName, value);
    }

    public string SinglePointInspectionTaskStatus
    {
        get => _singlePointInspectionTaskStatus;
        private set => SetProperty(ref _singlePointInspectionTaskStatus, value);
    }

    public string SinglePointInspectionLastTime
    {
        get => _singlePointInspectionLastTime;
        private set => SetProperty(ref _singlePointInspectionLastTime, value);
    }

    public string SinglePointInspectionResultSummary
    {
        get => _singlePointInspectionResultSummary;
        private set => SetProperty(ref _singlePointInspectionResultSummary, value);
    }

    public string TaskRoutingSummary
    {
        get => _taskRoutingSummary;
        private set => SetProperty(ref _taskRoutingSummary, value);
    }

    public string ReviewWallEntrySummary
    {
        get => _reviewWallEntrySummary;
        private set => SetProperty(ref _reviewWallEntrySummary, value);
    }

    public string DispatchPoolCandidateSummary
    {
        get => _dispatchPoolCandidateSummary;
        private set => SetProperty(ref _dispatchPoolCandidateSummary, value);
    }

    public InspectionScopePlanPreviewModel ScopePlanPreview
    {
        get => _scopePlanPreview;
        private set => SetProperty(ref _scopePlanPreview, value);
    }

    public ObservableCollection<InspectionScopePlanOptionState> AvailableScopePlans
    {
        get => _availableScopePlans;
        private set => SetProperty(ref _availableScopePlans, value);
    }

    public ObservableCollection<InspectionPointState> ScopeMatchedPoints
    {
        get => _scopeMatchedPoints;
        private set => SetProperty(ref _scopeMatchedPoints, value);
    }

    public ObservableCollection<InspectionPointState> ScopeUnmatchedPoints
    {
        get => _scopeUnmatchedPoints;
        private set
        {
            if (SetProperty(ref _scopeUnmatchedPoints, value))
            {
                OnPropertyChanged(nameof(HasScopeUnmatchedPoints));
            }
        }
    }

    public bool HasScopeUnmatchedPoints => ScopeUnmatchedPoints.Count > 0;

    public bool CanSaveCurrentScopePlan
        => !IsScopePlanSavePending
            && SelectedGroup is not null
            && _workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace)
            && !string.IsNullOrWhiteSpace(ResolveScopePlanSelection(workspace, null))
            && HasSavableScopePlan(workspace)
            && HasPendingScopePlanSelection(workspace);

    public string CurrentViewingScopePlanName
    {
        get => _currentViewingScopePlanName;
        private set => SetProperty(ref _currentViewingScopePlanName, value);
    }

    public string CurrentExecutionScopePlanName
    {
        get => _currentExecutionScopePlanName;
        private set => SetProperty(ref _currentExecutionScopePlanName, value);
    }

    public string ScopePlanAlignmentSummary
    {
        get => _scopePlanAlignmentSummary;
        private set => SetProperty(ref _scopePlanAlignmentSummary, value);
    }

    public string ScopePlanSaveFeedback
    {
        get => _scopePlanSaveFeedback;
        private set
        {
            if (SetProperty(ref _scopePlanSaveFeedback, value))
            {
                OnPropertyChanged(nameof(HasScopePlanSaveFeedback));
            }
        }
    }

    public bool HasScopePlanSaveFeedback => !string.IsNullOrWhiteSpace(ScopePlanSaveFeedback);

    public string ScopePlanFallbackHint
    {
        get => _scopePlanFallbackHint;
        private set
        {
            if (SetProperty(ref _scopePlanFallbackHint, value))
            {
                OnPropertyChanged(nameof(HasScopePlanFallbackHint));
            }
        }
    }

    public bool HasScopePlanFallbackHint => !string.IsNullOrWhiteSpace(ScopePlanFallbackHint);

    public string ScopeSaveButtonText
    {
        get => _scopeSaveButtonText;
        private set => SetProperty(ref _scopeSaveButtonText, value);
    }

    public bool IsScopePlanSavePending => _scopePlanSaveVisualState == ScopePlanSaveVisualState.Saving;

    public bool IsScopePlanSaveSuccess => _scopePlanSaveVisualState == ScopePlanSaveVisualState.Success;

    public bool IsScopePlanSaveFailure => _scopePlanSaveVisualState == ScopePlanSaveVisualState.Failure;

    public ICommand SelectGroupCommand { get; }
    public ICommand SelectPointCommand { get; }
    public ICommand SelectRecentFaultCommand { get; }
    public ICommand SelectScopePlanCommand { get; }
    public ICommand SelectFirstUnmappedPointCommand { get; }
    public ICommand SaveCurrentScopePlanCommand { get; }
    public ICommand ExecuteInspectionCommand { get; }
    public ICommand StartSinglePointInspectionCommand { get; }
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

    public override void OnNavigatedTo()
    {
        var summary = _pointSelectionContext.CurrentSummary;
        if (summary is null)
        {
            return;
        }

        var workspace = _workspaceByGroupId.Values.FirstOrDefault(candidate =>
            candidate.Points.Any(point => point.Id == summary.PointId));
        if (workspace is null)
        {
            return;
        }

        LoadGroup(workspace.Group, summary.PointId);
    }

    private void LoadGroup(InspectionGroupSummaryState group, string? preferredPointId = null)
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
        CurrentTask = workspace.CurrentTask;
        RecentTasks = workspace.RecentTasks;
        AvailableScopePlans = workspace.ScopePlanOptions;
        ClearScopePlanSaveFeedback();
        ApplyScopePlanSelection(workspace, ResolveScopePlanSelection(workspace, null));
        UpdateTaskAbnormalFlowPresentation(workspace.TaskBoard.CurrentTask);
        _selectFirstUnmappedPointCommand.RaiseCanExecuteChanged();

        ToggleGroupActionText = group.IsEnabled
            ? _textService.Resolve(TextTokens.InspectionActionDisable)
            : _textService.Resolve(TextTokens.InspectionActionEnable);

        _saveCurrentScopePlanCommand.RaiseCanExecuteChanged();
        _executeInspectionCommand.RaiseCanExecuteChanged();

        var initialPoint = !string.IsNullOrWhiteSpace(preferredPointId)
            ? Points.FirstOrDefault(point => point.Id == preferredPointId)
            : null;

        initialPoint ??= Points.FirstOrDefault(point => point.IsCurrent)
            ?? Points.FirstOrDefault(point => point.Status == InspectionPointStatus.Fault)
            ?? Points.FirstOrDefault();

        if (initialPoint is not null)
        {
            SelectPoint(initialPoint);
        }
        else
        {
            RefreshSinglePointInspectionSummary(workspace, null);
        }

        RefreshSummary(workspace);
        LoadReviewState(workspace, refreshFromPoints: false);
    }

    private string? ResolveScopePlanSelection(GroupWorkspaceState workspace, string? requestedPlanId)
    {
        if (!string.IsNullOrWhiteSpace(requestedPlanId)
            && workspace.ScopePlansById.ContainsKey(requestedPlanId))
        {
            return requestedPlanId;
        }

        if (_selectedScopePlanIdByGroupId.TryGetValue(workspace.Group.Id, out var selectedPlanId)
            && workspace.ScopePlansById.ContainsKey(selectedPlanId))
        {
            return selectedPlanId;
        }

        if (!string.IsNullOrWhiteSpace(workspace.SelectedScopePlanId)
            && workspace.ScopePlansById.ContainsKey(workspace.SelectedScopePlanId))
        {
            return workspace.SelectedScopePlanId;
        }

        if (!string.IsNullOrWhiteSpace(workspace.ExecutionScopePlanId)
            && workspace.ScopePlansById.ContainsKey(workspace.ExecutionScopePlanId))
        {
            return workspace.ExecutionScopePlanId;
        }

        return workspace.ScopePlans.FirstOrDefault()?.PlanId;
    }

    private void ApplyScopePlanSelection(GroupWorkspaceState workspace, string? requestedPlanId)
    {
        var selectedPlanId = ResolveScopePlanSelection(workspace, requestedPlanId);
        var selectedPlan = !string.IsNullOrWhiteSpace(selectedPlanId)
            && workspace.ScopePlansById.TryGetValue(selectedPlanId, out var resolvedPlan)
                ? resolvedPlan
                : workspace.ScopePlans.FirstOrDefault();

        if (selectedPlan is null)
        {
            ScopePlanPreview = CreateEmptyScopePlanPreview();
            workspace.ScopeMatchedPoints = new ObservableCollection<InspectionPointState>();
            workspace.ScopeUnmatchedPoints = new ObservableCollection<InspectionPointState>();
            ScopeMatchedPoints = workspace.ScopeMatchedPoints;
            ScopeUnmatchedPoints = workspace.ScopeUnmatchedPoints;
            CurrentViewingScopePlanName = TaskEmptyText;
            CurrentExecutionScopePlanName = string.IsNullOrWhiteSpace(workspace.ExecutionScopePlanName)
                ? TaskEmptyText
                : workspace.ExecutionScopePlanName;
            ScopePlanAlignmentSummary = TaskEmptyText;
            UpdateScopePlanFallbackHint(workspace);
            _selectScopePlanCommand.RaiseCanExecuteChanged();
            _saveCurrentScopePlanCommand.RaiseCanExecuteChanged();
            return;
        }

        workspace.SelectedScopePlanId = selectedPlan.PlanId;
        _selectedScopePlanIdByGroupId[workspace.Group.Id] = selectedPlan.PlanId;

        foreach (var option in workspace.ScopePlanOptions)
        {
            option.IsSelected = string.Equals(option.PlanId, selectedPlan.PlanId, StringComparison.Ordinal);
        }

        ScopePlanPreview = selectedPlan.Preview;
        CurrentViewingScopePlanName = selectedPlan.PlanName;
        CurrentExecutionScopePlanName = string.IsNullOrWhiteSpace(workspace.ExecutionScopePlanName)
            ? selectedPlan.PlanName
            : workspace.ExecutionScopePlanName;
        ScopePlanAlignmentSummary = string.Equals(selectedPlan.PlanId, workspace.ExecutionScopePlanId, StringComparison.Ordinal)
            ? _textService.Resolve(TextTokens.InspectionScopeCurrentViewEqualsExecution)
            : string.Format(
                _textService.Resolve(TextTokens.InspectionScopeCurrentViewDiffersExecutionPattern),
                selectedPlan.PlanName,
                CurrentExecutionScopePlanName);

        var decisionsByPointId = selectedPlan.PointDecisions
            .ToDictionary(decision => decision.PointId, decision => decision, StringComparer.Ordinal);
        var scopePoints = workspace.Points
            .Select(point => CreateScopePointState(point, decisionsByPointId.GetValueOrDefault(point.Id)))
            .ToList();

        workspace.ScopeMatchedPoints = new ObservableCollection<InspectionPointState>(scopePoints.Where(point => point.IsInDefaultScope));
        workspace.ScopeUnmatchedPoints = new ObservableCollection<InspectionPointState>(scopePoints.Where(point => !point.IsInDefaultScope));
        ScopeMatchedPoints = workspace.ScopeMatchedPoints;
        ScopeUnmatchedPoints = workspace.ScopeUnmatchedPoints;
        UpdateScopePlanFallbackHint(workspace);
        _selectScopePlanCommand.RaiseCanExecuteChanged();
        _saveCurrentScopePlanCommand.RaiseCanExecuteChanged();
        SyncScopePointSelection(SelectedPoint?.Id);
    }

    private InspectionPointState CreateScopePointState(
        InspectionPointState point,
        InspectionScopePlanPointDecisionModel? decision)
    {
        var scopePoint = point.CreateScopeSnapshot(
            decision?.IsInScope ?? true,
            string.IsNullOrWhiteSpace(decision?.Summary) ? TaskEmptyText : decision.Summary);
        scopePoint.IsCurrent = point.IsCurrent;
        return scopePoint;
    }

    private void SyncScopePointSelection(string? pointId)
    {
        foreach (var point in ScopeMatchedPoints)
        {
            point.IsSelected = string.Equals(point.Id, pointId, StringComparison.Ordinal);
        }

        foreach (var point in ScopeUnmatchedPoints)
        {
            point.IsSelected = string.Equals(point.Id, pointId, StringComparison.Ordinal);
        }
    }

    private void HandleTaskBoardChanged(object? sender, InspectionTaskBoardChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                ApplyTaskBoardChanged(e.GroupId, e.TaskBoard);
                return;
            }

            dispatcher.Invoke(() => ApplyTaskBoardChanged(e.GroupId, e.TaskBoard));
            return;
        }

        ApplyTaskBoardChanged(e.GroupId, e.TaskBoard);
    }

    private void RequestRefreshWorkspace(string? preferredGroupId = null, string? preferredPointId = null)
    {
        var snapshot = _inspectionTaskService.GetWorkspace().Data;
        TryApplyWorkspaceSnapshot(snapshot, preferredGroupId, preferredPointId);
    }

    private void ApplyTaskBoardChanged(string groupId, InspectionTaskBoardModel taskBoard)
    {
        if (!_workspaceByGroupId.TryGetValue(groupId, out var workspace))
        {
            RequestRefreshWorkspace(groupId, SelectedPoint?.Id);
            return;
        }

        workspace.TaskBoard = taskBoard;
        workspace.CurrentTask = taskBoard.CurrentTask is null
            ? null
            : CreateTaskSummaryState(taskBoard.CurrentTask);
        ReplaceRecentTasks(workspace.RecentTasks, taskBoard.RecentTasks.Select(CreateTaskSummaryState));

        var currentTask = taskBoard.CurrentTask;
        var executionsByPointId = currentTask?.PointExecutions.ToDictionary(point => point.PointId, StringComparer.Ordinal)
            ?? new Dictionary<string, InspectionTaskPointExecutionModel>(StringComparer.Ordinal);

        foreach (var point in workspace.Points)
        {
            ApplyPointExecutionState(point, currentTask, executionsByPointId.GetValueOrDefault(point.Id));
            SyncMapPoint(point, workspace.MapPoints);
        }

        foreach (var scopePoint in workspace.ScopeMatchedPoints)
        {
            if (workspace.PointsById.TryGetValue(scopePoint.Id, out var sourcePoint))
            {
                MirrorPointVisualState(scopePoint, sourcePoint);
            }
        }

        foreach (var scopePoint in workspace.ScopeUnmatchedPoints)
        {
            if (workspace.PointsById.TryGetValue(scopePoint.Id, out var sourcePoint))
            {
                MirrorPointVisualState(scopePoint, sourcePoint);
            }
        }

        if (SelectedGroup?.Id == groupId)
        {
            CurrentTask = workspace.CurrentTask;
            ReplaceRecentTasks(RecentTasks, workspace.RecentTasks);
            RefreshSummary(workspace);
            RefreshSinglePointInspectionSummary(workspace, SelectedPoint);

            if (SelectedPoint is not null && workspace.PointsById.TryGetValue(SelectedPoint.Id, out var selectedPoint))
            {
                _selectedPoint = selectedPoint;
                SelectedPoint = selectedPoint;
                SelectedPointDetail = CreatePointDetail(selectedPoint, selectedPoint.BusinessSummary);
            }
        }
    }

    private static void ReplaceRecentTasks(
        ObservableCollection<InspectionTaskSummaryState> target,
        IEnumerable<InspectionTaskSummaryState> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void ApplyPointExecutionState(
        InspectionPointState point,
        InspectionTaskRecordModel? currentTask,
        InspectionTaskPointExecutionModel? execution)
    {
        point.Status = ResolvePointStatusFromExecution(point, execution);
        point.StatusText = ResolvePointStatus(point.Status);
        point.IsCurrent = execution?.Status == InspectionPointExecutionStatusModel.Running;
        point.PlaybackStatus = ResolveDetailPlaybackStatus(point, execution);
        point.FaultType = ResolveDetailFaultType(point, point.BusinessSummary, execution);
        point.FaultDescription = AppendAiSummary(ResolveDetailSummary(point, point.BusinessSummary, execution), execution);
        point.DispatchPoolEntry = ResolveDispatchPoolEntryText(currentTask, point.Id);
        point.LastInspectionConclusion = !string.IsNullOrWhiteSpace(execution?.RecoverySummary)
            ? execution.RecoverySummary
            : !string.IsNullOrWhiteSpace(execution?.AiAnalysisSummary)
                ? execution.AiAnalysisSummary
                : string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
                    ? point.LastInspectionConclusion
                    : execution.FinalPlaybackResult;
        point.IsPreviewAvailable = !string.IsNullOrWhiteSpace(execution?.PreviewUrl)
            || _previewSessionByPointId.ContainsKey(point.Id);
    }

    private static void MirrorPointVisualState(InspectionPointState target, InspectionPointState source)
    {
        target.Status = source.Status;
        target.StatusText = source.StatusText;
        target.IsCurrent = source.IsCurrent;
        target.PlaybackStatus = source.PlaybackStatus;
        target.FaultType = source.FaultType;
        target.FaultDescription = source.FaultDescription;
        target.DispatchPoolEntry = source.DispatchPoolEntry;
        target.LastInspectionConclusion = source.LastInspectionConclusion;
        target.IsPreviewAvailable = source.IsPreviewAvailable;
    }

    private InspectionPointStatus ResolvePointStatusFromExecution(
        InspectionPointState point,
        InspectionTaskPointExecutionModel? execution)
    {
        if (execution is null)
        {
            return point.Status;
        }

        return execution.Status switch
        {
            InspectionPointExecutionStatusModel.Pending => InspectionPointStatus.Pending,
            InspectionPointExecutionStatusModel.Running => InspectionPointStatus.Inspecting,
            InspectionPointExecutionStatusModel.Succeeded => InspectionPointStatus.Normal,
            InspectionPointExecutionStatusModel.Skipped => InspectionPointStatus.Silent,
            InspectionPointExecutionStatusModel.Failed => execution.FailureCategory == InspectionPointFailureCategoryModel.DeviceOffline
                ? InspectionPointStatus.PausedUntilRecovery
                : InspectionPointStatus.Fault,
            _ => point.Status
        };
    }

    public bool TryWritePointEvidence(InspectionPointEvidenceWriteRequest request)
    {
        var response = _inspectionTaskService.WritePointEvidence(request);
        return response.IsSuccess;
    }

    public void UpdateMapAvailability(bool isAvailable)
    {
        IsMapAvailabilityKnown = true;
        IsRealMapAvailable = isAvailable;
        MapAvailabilityBadgeText = isAvailable ? MapModeRealText : MapModeFallbackText;
    }

    private void SelectPoint(InspectionPointState point)
    {
        if (!ReferenceEquals(_selectedPoint, point))
        {
            if (_selectedPoint is not null)
            {
                _selectedPoint.IsSelected = false;
            }

            point.IsSelected = true;
            _selectedPoint = point;
        }

        UpdateMapSelection(point);
        UpdateRecentFaultSelection(point.Id);
        UpdateScopeSelection(point.Id);

        SelectedPoint = point;
        SelectedMapPointId = point.Id;
        var summary = point.BusinessSummary;
        SelectedPointSummary = summary;
        CurrentPointSourceType = summary.SourceType;
        SelectedPointDetail = CreatePointDetail(point, summary);
        RefreshSinglePointInspectionSummary(GetCurrentWorkspace(), point);
        _pointSelectionContext.Update(summary, nameof(InspectionPageViewModel));
        _ = PrepareSelectedPointPreviewAsync(point);

        MapPointSourceDiagnostics.Write(
            "InspectionSummary",
            $"inspection point summary binding = pointId:{summary.PointId}, deviceCode:{summary.DeviceCode}, source:{summary.SourceType}, online:{summary.OnlineStatus}, coordinate:{summary.CoordinateStatus}, fault:{summary.FaultType}, lastSync:{summary.LastSyncTime}");
    }

    private void SelectPoint(string pointId)
    {
        var point = Points.FirstOrDefault(candidate => candidate.Id == pointId);
        if (point is not null)
        {
            SelectPoint(point);
        }
    }

    private void UpdateMapSelection(InspectionPointState point)
    {
        if (_selectedMapPoint is not null && !string.Equals(_selectedMapPoint.PointId, point.Id, StringComparison.Ordinal))
        {
            _selectedMapPoint.IsSelected = false;
            _selectedMapPoint.IsCurrent = false;
        }

        var mapPoint = MapPoints.FirstOrDefault(candidate => candidate.PointId == point.Id);
        if (mapPoint is null)
        {
            _selectedMapPoint = null;
            return;
        }

        mapPoint.IsSelected = true;
        mapPoint.IsCurrent = point.IsCurrent;
        _selectedMapPoint = mapPoint;
    }

    private void UpdateRecentFaultSelection(string pointId)
    {
        if (_selectedRecentFault is not null
            && !string.Equals(_selectedRecentFault.PointId, pointId, StringComparison.Ordinal))
        {
            _selectedRecentFault.IsSelected = false;
        }

        _selectedRecentFault = RecentFaults.FirstOrDefault(candidate => candidate.PointId == pointId);
        if (_selectedRecentFault is not null)
        {
            _selectedRecentFault.IsSelected = true;
        }
    }

    private void UpdateScopeSelection(string pointId)
    {
        if (_selectedScopePoint is not null
            && !string.Equals(_selectedScopePoint.Id, pointId, StringComparison.Ordinal))
        {
            _selectedScopePoint.IsSelected = false;
        }

        _selectedScopePoint = ScopeMatchedPoints.FirstOrDefault(candidate => candidate.Id == pointId)
            ?? ScopeUnmatchedPoints.FirstOrDefault(candidate => candidate.Id == pointId);
        if (_selectedScopePoint is not null)
        {
            _selectedScopePoint.IsSelected = true;
        }
    }

    private async Task PrepareSelectedPointPreviewAsync(InspectionPointState point)
    {
        if (SelectedGroup is null || SelectedPoint?.Id != point.Id)
        {
            return;
        }

        var currentDetail = SelectedPointDetail;
        if (currentDetail is null || currentDetail.PreviewHostUri is not null)
        {
            return;
        }

        _selectedPointPreviewCts?.Cancel();
        _selectedPointPreviewCts?.Dispose();
        _selectedPointPreviewCts = new CancellationTokenSource();
        var previewToken = Interlocked.Increment(ref _selectedPointPreviewToken);

        SelectedPointDetail = currentDetail with
        {
            PlaybackStatus = "实时预览加载中",
            PreviewBusinessTitle = "实时预览准备中",
            PreviewBusinessDescription = "AI智能巡检中心正在为当前点位加载真实预览。"
        };

        ServiceResponse<InspectionPointPreviewSessionModel> response;
        try
        {
            response = await _inspectionTaskService.PreparePointPreviewAsync(
                SelectedGroup.Id,
                point.Id,
                _selectedPointPreviewCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionPreview",
                $"preview prepare failed: pointId={point.Id}, reason={exception.Message}");
            return;
        }

        if (previewToken != _selectedPointPreviewToken
            || SelectedPoint?.Id != point.Id)
        {
            return;
        }

        if (response.IsSuccess)
        {
            _previewSessionByPointId[point.Id] = response.Data;
            point.IsPreviewAvailable = true;
        }

        SelectedPointDetail = CreatePointDetail(point, point.BusinessSummary);
    }

    private async void StartSinglePointInspection()
    {
        var workspace = GetCurrentWorkspace();
        if (workspace is null || SelectedPoint is null)
        {
            return;
        }

        SinglePointInspectionTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusRunning);
        SinglePointInspectionResultSummary = "AI智能巡检中心已启动当前点位巡检。";
        ExecutionState?.GetType();

        var selectedPoint = SelectedPoint;
        var response = await Task.Run(() => _inspectionTaskService.StartSinglePointInspection(workspace.Group.Id, selectedPoint.Id));
        if (!response.IsSuccess)
        {
            CurrentTask = CreateTaskSummaryState(response.Data);
            UpdateTaskAbnormalFlowPresentation(response.Data);
            ApplySinglePointInspectionRecord(response.Data, selectedPoint);
            return;
        }
    }

    private async void StartGroupInspection()
    {
        if (SelectedGroup is null || !_workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace))
        {
            return;
        }

        workspace.ExecutionState.CurrentTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusRunning);
        workspace.ExecutionState.SimulationNote = "AI智能巡检中心已启动本组巡检。";
        if (ReferenceEquals(ExecutionState, workspace.ExecutionState))
        {
            ExecutionState = workspace.ExecutionState;
        }

        var response = await Task.Run(() => _inspectionTaskService.StartDefaultScopeInspection(workspace.Group.Id));
        if (!response.IsSuccess)
        {
            CurrentTask = CreateTaskSummaryState(response.Data);
            UpdateTaskAbnormalFlowPresentation(response.Data);
            workspace.ExecutionState.CurrentTaskStatus = ResolveTaskStatusText(response.Data.Status);
            workspace.ExecutionState.CurrentPointName = "--";
            workspace.ExecutionState.SimulationNote = response.Data.Summary;
            return;
        }
    }

    private async void StartSinglePointInspectionAsyncSafe()
    {
        var workspace = GetCurrentWorkspace();
        var selectedPoint = SelectedPoint;
        if (workspace is null || selectedPoint is null)
        {
            return;
        }

        var pendingSummary = "AI智能巡检中心已启动当前点位巡检。";
        workspace.ExecutionState.CurrentTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusRunning);
        workspace.ExecutionState.CurrentPointName = selectedPoint.Name;
        workspace.ExecutionState.CurrentProgressText = "0 / 1";
        workspace.ExecutionState.CurrentProgressValue = 0;
        workspace.ExecutionState.SimulationNote = pendingSummary;
        if (ReferenceEquals(ExecutionState, workspace.ExecutionState))
        {
            ExecutionState = workspace.ExecutionState;
        }

        SinglePointInspectionTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusRunning);
        SinglePointInspectionLastTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        SinglePointInspectionResultSummary = pendingSummary;
        workspace.CurrentTask = CreatePendingTaskSummary(
            selectedPoint.Name,
            InspectionTaskTypeModel.SinglePoint,
            selectedPoint.Name,
            pendingSummary);
        if (SelectedGroup?.Id == workspace.Group.Id)
        {
            CurrentTask = workspace.CurrentTask;
        }

        ServiceResponse<InspectionTaskRecordModel> response;
        try
        {
            response = await Task.Run(() => _inspectionTaskService.StartSinglePointInspection(workspace.Group.Id, selectedPoint.Id));
        }
        catch (Exception exception)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionTask",
                $"single inspection start failed: groupId={workspace.Group.Id}, pointId={selectedPoint.Id}, reason={exception.Message}");
            ApplyInspectionStartFailure(workspace, "AI智能巡检中心启动单点巡检失败，请稍后重试。", selectedPoint.Name);
            return;
        }

        if (response.IsSuccess)
        {
            workspace.CurrentTask = CreateTaskSummaryState(response.Data);
            if (SelectedGroup?.Id == workspace.Group.Id)
            {
                CurrentTask = workspace.CurrentTask;
            }

            UpdateTaskAbnormalFlowPresentation(response.Data);
            ApplySinglePointInspectionRecord(response.Data, selectedPoint);
            return;
        }

        ApplyInspectionStartFailure(
            workspace,
            string.IsNullOrWhiteSpace(response.Message) ? response.Data.Summary : response.Message,
            selectedPoint.Name);
    }

    private async void StartGroupInspectionAsyncSafe()
    {
        if (SelectedGroup is null || !_workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace))
        {
            return;
        }

        var pendingSummary = "AI智能巡检中心已启动本组巡检。";
        workspace.ExecutionState.CurrentTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusRunning);
        workspace.ExecutionState.CurrentPointName = "--";
        workspace.ExecutionState.CurrentProgressText = $"0 / {Math.Max(0, workspace.Points.Count)}";
        workspace.ExecutionState.CurrentProgressValue = 0;
        workspace.ExecutionState.SimulationNote = pendingSummary;
        if (ReferenceEquals(ExecutionState, workspace.ExecutionState))
        {
            ExecutionState = workspace.ExecutionState;
        }

        workspace.CurrentTask = CreatePendingTaskSummary(
            workspace.Group.Name,
            InspectionTaskTypeModel.ScopePlan,
            "--",
            pendingSummary);
        if (SelectedGroup?.Id == workspace.Group.Id)
        {
            CurrentTask = workspace.CurrentTask;
        }

        ServiceResponse<InspectionTaskRecordModel> response;
        try
        {
            response = await Task.Run(() => _inspectionTaskService.StartDefaultScopeInspection(workspace.Group.Id));
        }
        catch (Exception exception)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionTask",
                $"group inspection start failed: groupId={workspace.Group.Id}, reason={exception.Message}");
            ApplyInspectionStartFailure(workspace, "AI智能巡检中心启动本组巡检失败，请稍后重试。", "--");
            return;
        }

        if (response.IsSuccess)
        {
            workspace.CurrentTask = CreateTaskSummaryState(response.Data);
            if (SelectedGroup?.Id == workspace.Group.Id)
            {
                CurrentTask = workspace.CurrentTask;
            }

            UpdateTaskAbnormalFlowPresentation(response.Data);
            workspace.ExecutionState.CurrentTaskStatus = ResolveTaskStatusText(response.Data.Status);
            workspace.ExecutionState.CurrentPointName = response.Data.ResolveCurrentPointDisplayName();
            workspace.ExecutionState.SimulationNote = response.Data.Summary;
            return;
        }

        ApplyInspectionStartFailure(
            workspace,
            string.IsNullOrWhiteSpace(response.Message) ? response.Data.Summary : response.Message,
            "--");
    }

    private InspectionTaskSummaryState CreatePendingTaskSummary(
        string taskName,
        InspectionTaskTypeModel taskType,
        string currentPointName,
        string summary)
    {
        return new InspectionTaskSummaryState
        {
            TaskId = string.Empty,
            TaskName = taskName,
            TaskTypeText = ResolveTaskTypeText(taskType),
            TriggerText = ResolveTaskTriggerText(InspectionTaskTriggerModel.Manual),
            StatusText = _textService.Resolve(TextTokens.InspectionTaskStatusRunning),
            ScopePlanText = string.IsNullOrWhiteSpace(CurrentViewingScopePlanName) ? TaskEmptyText : CurrentViewingScopePlanName,
            CurrentPointText = string.IsNullOrWhiteSpace(currentPointName) ? "--" : currentPointName,
            TimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Summary = summary
        };
    }

    private void ApplyInspectionStartFailure(
        GroupWorkspaceState workspace,
        string summary,
        string currentPointName)
    {
        workspace.ExecutionState.CurrentTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusFailed);
        workspace.ExecutionState.CurrentPointName = string.IsNullOrWhiteSpace(currentPointName) ? "--" : currentPointName;
        workspace.ExecutionState.SimulationNote = summary;
        if (ReferenceEquals(ExecutionState, workspace.ExecutionState))
        {
            ExecutionState = workspace.ExecutionState;
        }

        SinglePointInspectionTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusFailed);
        SinglePointInspectionResultSummary = summary;
    }

    private async void SaveCurrentScopePlan()
    {
        if (SelectedGroup is null
            || !_workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace))
        {
            SetScopePlanSaveFeedback(
                ScopePlanSaveVisualState.Failure,
                _textService.Resolve(TextTokens.InspectionScopeSaveMissingPlan));
            return;
        }

        var selectedPlanId = ResolveScopePlanSelection(workspace, null);
        if (string.IsNullOrWhiteSpace(selectedPlanId))
        {
            SetScopePlanSaveFeedback(
                ScopePlanSaveVisualState.Failure,
                _textService.Resolve(TextTokens.InspectionScopeSaveMissingPlan));
            return;
        }

        if (_inspectionTaskService is not IInspectionScopePlanPersistenceService persistenceService)
        {
            SetScopePlanSaveFeedback(
                ScopePlanSaveVisualState.Failure,
                _textService.Resolve(TextTokens.InspectionScopeSaveFailure));
            return;
        }

        if (IsScopePlanSavePending)
        {
            return;
        }

        var selectedPointId = SelectedPoint?.Id;
        SetScopePlanSaveFeedback(
            ScopePlanSaveVisualState.Saving,
            _textService.Resolve(TextTokens.InspectionScopeSavePending));

        ServiceResponse<InspectionScopePlanSaveResult> response;
        try
        {
            response = await Task.Run(() => persistenceService.SaveDefaultScopePlan(workspace.Group.Id, selectedPlanId));
        }
        catch (Exception exception)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionTask",
                $"scope plan save execution failed: groupId={workspace.Group.Id}, scopePlanId={selectedPlanId}, reason={exception.Message}");
            SetScopePlanSaveFeedback(
                ScopePlanSaveVisualState.Failure,
                _textService.Resolve(TextTokens.InspectionScopeSaveFailure));
            return;
        }

        var saveResult = response.Data;
        var shouldRollbackSelection = saveResult.Outcome is not InspectionScopePlanSaveOutcomeModel.Succeeded;
        var applied = TrySyncWorkspaceAfterScopePlanSave(
            saveResult.WorkspaceSnapshot,
            workspace.Group.Id,
            selectedPointId,
            shouldRollbackSelection);

        if (!applied)
        {
            SetScopePlanSaveFeedback(
                ScopePlanSaveVisualState.Failure,
                _textService.Resolve(TextTokens.InspectionScopeSaveRefreshRollback));
            return;
        }

        switch (saveResult.Outcome)
        {
            case InspectionScopePlanSaveOutcomeModel.Succeeded:
                SetScopePlanSaveFeedback(
                    ScopePlanSaveVisualState.Success,
                    _textService.Resolve(TextTokens.InspectionScopeSaveSuccessPattern));
                UpdateScopePlanFallbackHint(GetCurrentWorkspace());
                return;
            case InspectionScopePlanSaveOutcomeModel.RefreshRolledBack:
                SetScopePlanSaveFeedback(
                    ScopePlanSaveVisualState.Failure,
                    _textService.Resolve(TextTokens.InspectionScopeSaveRefreshRollback));
                UpdateScopePlanFallbackHint(GetCurrentWorkspace());
                return;
            case InspectionScopePlanSaveOutcomeModel.DefaultPlanMissing:
                SetScopePlanSaveFeedback(
                    ScopePlanSaveVisualState.Failure,
                    _textService.Resolve(TextTokens.InspectionScopeSaveMissingPlan));
                UpdateScopePlanFallbackHint(GetCurrentWorkspace(), saveResult.Outcome);
                return;
            case InspectionScopePlanSaveOutcomeModel.CurrentPlanInvalid:
                SetScopePlanSaveFeedback(
                    ScopePlanSaveVisualState.Failure,
                    _textService.Resolve(TextTokens.InspectionScopeSaveFailure));
                UpdateScopePlanFallbackHint(GetCurrentWorkspace(), saveResult.Outcome);
                return;
            default:
                SetScopePlanSaveFeedback(
                    ScopePlanSaveVisualState.Failure,
                    _textService.Resolve(TextTokens.InspectionScopeSaveFailure));
                UpdateScopePlanFallbackHint(GetCurrentWorkspace());
                return;
        }
    }

    private bool TrySyncWorkspaceAfterScopePlanSave(
        InspectionWorkspaceSnapshot snapshot,
        string groupId,
        string? selectedPointId,
        bool rollbackSelectionToExecution)
    {
        if (rollbackSelectionToExecution)
        {
            ResetScopePlanSelection(groupId);
        }

        if (TryApplyWorkspaceSnapshot(snapshot, groupId, selectedPointId))
        {
            return true;
        }

        try
        {
            var refreshedSnapshot = _inspectionTaskService.GetWorkspace().Data;
            if (rollbackSelectionToExecution)
            {
                ResetScopePlanSelection(groupId);
            }

            return TryApplyWorkspaceSnapshot(refreshedSnapshot, groupId, selectedPointId);
        }
        catch (Exception exception)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionTask",
                $"scope plan save workspace sync failed: groupId={groupId}, reason={exception.Message}");
            return false;
        }
    }

    private void ResetScopePlanSelection(string groupId)
    {
        _selectedScopePlanIdByGroupId.Remove(groupId);

        if (_workspaceByGroupId.TryGetValue(groupId, out var workspace))
        {
            workspace.SelectedScopePlanId = workspace.ExecutionScopePlanId;
        }
    }

    private void ClearScopePlanSaveFeedback()
    {
        SetScopePlanSaveFeedback(ScopePlanSaveVisualState.Idle, string.Empty);
    }

    private void SetScopePlanSaveFeedback(ScopePlanSaveVisualState state, string feedback)
    {
        ScopePlanSaveFeedback = feedback;
        ScopeSaveButtonText = state == ScopePlanSaveVisualState.Saving
            ? _textService.Resolve(TextTokens.InspectionScopeSavePending)
            : ScopeSaveActionText;
        SetScopePlanSaveVisualState(state);
    }

    private void SetScopePlanSaveVisualState(ScopePlanSaveVisualState state)
    {
        if (_scopePlanSaveVisualState == state)
        {
            _selectScopePlanCommand.RaiseCanExecuteChanged();
            _saveCurrentScopePlanCommand.RaiseCanExecuteChanged();
            return;
        }

        _scopePlanSaveVisualState = state;
        OnPropertyChanged(nameof(IsScopePlanSavePending));
        OnPropertyChanged(nameof(IsScopePlanSaveSuccess));
        OnPropertyChanged(nameof(IsScopePlanSaveFailure));
        OnPropertyChanged(nameof(HasScopePlanSaveFeedback));
        OnPropertyChanged(nameof(CanSaveCurrentScopePlan));
        _selectScopePlanCommand.RaiseCanExecuteChanged();
        _saveCurrentScopePlanCommand.RaiseCanExecuteChanged();
    }

    private void UpdateScopePlanFallbackHint(
        GroupWorkspaceState? workspace,
        InspectionScopePlanSaveOutcomeModel? saveOutcome = null)
    {
        if (saveOutcome == InspectionScopePlanSaveOutcomeModel.CurrentPlanInvalid)
        {
            ScopePlanFallbackHint = _textService.Resolve(TextTokens.InspectionScopeCurrentPlanInvalidHint);
            return;
        }

        if (saveOutcome == InspectionScopePlanSaveOutcomeModel.DefaultPlanMissing
            || workspace is not null && IsFallbackScopePlanId(workspace.ExecutionScopePlanId))
        {
            ScopePlanFallbackHint = _textService.Resolve(TextTokens.InspectionScopeDefaultPlanMissingHint);
            return;
        }

        if (workspace is not null && workspace.Points.Count == 0)
        {
            ScopePlanFallbackHint = _textService.Resolve(TextTokens.InspectionScopeWorkspaceEmptyHint);
            return;
        }

        ScopePlanFallbackHint = string.Empty;
    }

    private static bool HasSavableScopePlan(GroupWorkspaceState workspace)
    {
        var selectedPlanId = string.IsNullOrWhiteSpace(workspace.SelectedScopePlanId)
            ? workspace.ExecutionScopePlanId
            : workspace.SelectedScopePlanId;
        return !IsFallbackScopePlanId(selectedPlanId);
    }

    private static bool HasPendingScopePlanSelection(GroupWorkspaceState workspace)
    {
        var selectedPlanId = string.IsNullOrWhiteSpace(workspace.SelectedScopePlanId)
            ? workspace.ExecutionScopePlanId
            : workspace.SelectedScopePlanId;
        return !string.Equals(selectedPlanId, workspace.ExecutionScopePlanId, StringComparison.Ordinal);
    }

    private static bool IsFallbackScopePlanId(string? scopePlanId)
        => string.IsNullOrWhiteSpace(scopePlanId)
            || string.Equals(scopePlanId, "__default_scope__", StringComparison.Ordinal);

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
        _startSinglePointInspectionCommand.RaiseCanExecuteChanged();
        RefreshSinglePointInspectionSummary(workspace, SelectedPoint);
    }

    private bool TryApplyWorkspaceSnapshot(
        InspectionWorkspaceSnapshot snapshot,
        string? preferredGroupId = null,
        string? preferredPointId = null)
    {
        try
        {
            if (snapshot.Groups.Count == 0)
            {
                MapPointSourceDiagnostics.Write(
                    "InspectionTask",
                    $"inspection workspace apply skipped: groupId={preferredGroupId ?? "none"}, reason=empty_snapshot");
                return false;
            }

            var updatedWorkspaces = CreateWorkspaces(snapshot);
            var updatedGroups = updatedWorkspaces.Values
                .Select(workspace => workspace.Group)
                .ToList();

            var targetGroup = !string.IsNullOrWhiteSpace(preferredGroupId)
                ? updatedGroups.FirstOrDefault(group => group.Id == preferredGroupId)
                : null;
            targetGroup ??= SelectedGroup is not null
                ? updatedGroups.FirstOrDefault(group => group.Id == SelectedGroup.Id)
                : null;
            targetGroup ??= updatedGroups.FirstOrDefault();

            _workspaceByGroupId = updatedWorkspaces;

            Groups.Clear();
            foreach (var group in updatedGroups)
            {
                Groups.Add(group);
            }

            if (targetGroup is not null)
            {
                LoadGroup(targetGroup, preferredPointId);
            }

            _saveCurrentScopePlanCommand.RaiseCanExecuteChanged();
            return true;
        }
        catch (Exception exception)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionTask",
                $"inspection workspace apply failed: groupId={preferredGroupId ?? "none"}, reason={exception.Message}");
            return false;
        }
    }

    private void RefreshSummary(GroupWorkspaceState workspace)
    {
        var currentTask = workspace.TaskBoard.CurrentTask;
        var totalPoints = currentTask?.TotalPointCount > 0 ? currentTask.TotalPointCount : workspace.Points.Count;
        var normalCount = currentTask?.SuccessCount ?? workspace.Points.Count(point => point.Status == InspectionPointStatus.Normal);
        var faultCount = currentTask?.FailureCount ?? workspace.Points.Count(point => point.Status == InspectionPointStatus.Fault);
        var currentPointName = currentTask?.ResolveCurrentPointDisplayName() ?? "--";
        var inspectedCount = currentTask is null
            ? workspace.Points.Count(point => point.Status is InspectionPointStatus.Normal or InspectionPointStatus.Fault or InspectionPointStatus.Inspecting)
            : currentTask.GetCompletedPointCount()
                + (currentTask.Status == InspectionTaskStatusModel.Running ? 1 : 0);
        var inspectableCount = Math.Max(1, totalPoints);

        workspace.RunSummary.GroupName = workspace.Group.Name;
        workspace.RunSummary.StartedAt = currentTask?.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss")
            ?? currentTask?.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            ?? workspace.RunSummary.StartedAt;
        workspace.RunSummary.TotalPoints = totalPoints.ToString();
        workspace.RunSummary.InspectedPoints = inspectedCount.ToString();
        workspace.RunSummary.NormalCount = normalCount.ToString();
        workspace.RunSummary.FaultCount = faultCount.ToString();
        workspace.RunSummary.CurrentPointName = currentPointName;

        workspace.ExecutionState.CurrentProgressValue = Math.Round(inspectedCount * 100d / inspectableCount, 0);
        workspace.ExecutionState.CurrentProgressText = $"{inspectedCount} / {inspectableCount}";
        workspace.ExecutionState.CurrentPointName = currentPointName;
        workspace.ExecutionState.CurrentTaskStatus = currentTask is null
            ? workspace.ExecutionState.CurrentTaskStatus
            : ResolveTaskStatusText(currentTask.Status);
        workspace.ExecutionState.SimulationNote = currentTask?.Summary ?? workspace.ExecutionState.SimulationNote;
        UpdateTaskAbnormalFlowPresentation(currentTask);
    }

    private void RefreshSinglePointInspectionSummary(GroupWorkspaceState? workspace, InspectionPointState? point)
    {
        SinglePointInspectionPointName = point?.Name ?? TaskEmptyText;

        if (workspace is null || point is null)
        {
            SinglePointInspectionTaskStatus = TaskEmptyText;
            SinglePointInspectionLastTime = TaskEmptyText;
            SinglePointInspectionResultSummary = TaskEmptyText;
            return;
        }

        var record = EnumerateTaskCandidates(workspace)
            .FirstOrDefault(task => task.FindPointExecution(point.Id) is not null);
        if (record is null)
        {
            SinglePointInspectionTaskStatus = TaskEmptyText;
            SinglePointInspectionLastTime = TaskEmptyText;
            SinglePointInspectionResultSummary = TaskEmptyText;
            return;
        }

        ApplySinglePointInspectionRecord(record, point);
    }

    private void ApplySinglePointInspectionRecord(InspectionTaskRecordModel record, InspectionPointState point)
    {
        SinglePointInspectionPointName = point.Name;
        SinglePointInspectionTaskStatus = ResolveTaskStatusText(record.Status);
        SinglePointInspectionLastTime = record.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss")
            ?? record.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss")
            ?? record.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        var resultSummary = record.ResolvePointInspectionSummary(point.Id);
        SinglePointInspectionResultSummary = string.IsNullOrWhiteSpace(resultSummary)
            ? TaskEmptyText
            : resultSummary;
    }

    private InspectionPointDetailState CreatePointDetail(
        InspectionPointState point,
        PointBusinessSummaryState summary)
    {
        var workspace = GetCurrentWorkspace();
        var task = ResolveTaskRecord(workspace, point.Id);
        var execution = ResolvePointExecution(workspace, point.Id);
        var previewSession = ResolvePreviewSession(point.Id, execution);
        var playbackStatus = ResolveDetailPlaybackStatus(point, execution, previewSession);
        var faultType = ResolveDetailFaultType(point, summary, execution, previewSession);
        var faultDescription = AppendAiSummary(ResolveDetailSummary(point, summary, execution, previewSession), execution);
        var previewBusinessTitle = ResolvePreviewBusinessTitle(execution, previewSession, point);
        var previewBusinessDescription = AppendAiSummary(ResolvePreviewBusinessDescription(execution, previewSession, point), execution);
        var previewHostUri = ResolvePreviewHostUri(execution, previewSession);
        var pointId = execution?.PointId ?? point.Id;

        return new InspectionPointDetailState(
            ResolveExecutionTaskId(workspace, point.Id),
            point.Id,
            point.DeviceCode,
            summary.DeviceName,
            point.UnitName,
            point.CurrentHandlingUnit,
            ResolvePointStatus(point.Status),
            summary.CoordinateStatus,
            summary.LastSyncTime,
            summary.OnlineStatus,
            playbackStatus,
            point.ImageStatus,
            faultType,
            faultDescription,
            previewHostUri is not null,
            summary.IsCoordinatePending,
            point.LastFaultTime,
            ResolveDispatchPoolEntryText(task, pointId),
            !string.IsNullOrWhiteSpace(execution?.RecoverySummary)
                ? execution.RecoverySummary
                : !string.IsNullOrWhiteSpace(execution?.AiAnalysisSummary)
                ? execution.AiAnalysisSummary
                : execution?.FinalPlaybackResult ?? point.LastInspectionConclusion)
        {
            PreviewHostUri = previewHostUri,
            PreviewHostKind = !string.IsNullOrWhiteSpace(execution?.PreviewHostKind)
                ? execution.PreviewHostKind
                : previewSession?.PreviewHostKind ?? string.Empty,
            PreviewBusinessTitle = previewBusinessTitle,
            PreviewBusinessDescription = previewBusinessDescription,
            OnlineCheckResult = !string.IsNullOrWhiteSpace(execution?.OnlineCheckResult)
                ? execution.OnlineCheckResult
                : previewSession?.OnlineCheckResult ?? string.Empty,
            StreamUrlAcquireResult = !string.IsNullOrWhiteSpace(execution?.StreamUrlAcquireResult)
                ? execution.StreamUrlAcquireResult
                : previewSession?.StreamUrlAcquireResult ?? string.Empty,
            FinalPlaybackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
                ? execution.FinalPlaybackResult
                : previewSession?.FinalPlaybackResult ?? string.Empty,
            PlaybackAttemptCount = execution?.PlaybackAttemptCount ?? previewSession?.PlaybackAttemptCount ?? 0,
            ProtocolFallbackUsed = execution?.ProtocolFallbackUsed ?? previewSession?.ProtocolFallbackUsed ?? false,
            ScreenshotPlannedCount = execution?.ScreenshotPlannedCount ?? 0,
            ScreenshotIntervalSeconds = execution?.ScreenshotIntervalSeconds ?? 0,
            ScreenshotSuccessCount = execution?.ScreenshotSuccessCount ?? 0,
            EvidenceCaptureState = execution?.EvidenceCaptureState ?? InspectionEvidenceValueKeys.CaptureStateNone,
            EvidenceSummary = execution?.EvidenceSummary ?? string.Empty,
            AllowManualSupplementScreenshot = execution?.AllowManualSupplementScreenshot ?? false,
            EvidenceRetentionMode = execution?.EvidenceRetentionMode ?? string.Empty,
            EvidenceRetentionDays = execution?.EvidenceRetentionDays ?? 0,
            AiAnalysisStatus = execution?.AiAnalysisStatus ?? InspectionEvidenceValueKeys.AiAnalysisReserved,
            AiAnalysisSummary = execution?.AiAnalysisSummary ?? string.Empty,
            IsAiAbnormalDetected = execution?.IsAiAbnormalDetected ?? false,
            AiRecognitionSummary = ResolveAiRecognitionSummary(execution),
            AiAbnormalTags = execution?.AiAbnormalTags ?? [],
            AiConfidence = execution?.AiConfidence ?? 0d,
            AiSuggestedAction = execution?.AiSuggestedAction ?? string.Empty,
            RouteToReviewWallReserved = execution?.RouteToReviewWallReserved ?? false,
            RouteToDispatchPoolReserved = execution?.RouteToDispatchPoolReserved ?? false,
            ManualReviewRequiredReserved = execution?.ManualReviewRequiredReserved ?? false,
            ReviewWallEntryStatus = ResolveReviewWallEntryStatus(task, pointId),
            DispatchCandidateEntryStatus = ResolveDispatchCandidateEntryStatus(task, execution, pointId),
            ManualSupplementEntryStatus = ResolveManualSupplementEntryStatus(task, pointId),
            BusinessRoutingDescription = AppendDispatchRoutingDescription(
                BuildBusinessRoutingDescription(task, execution, pointId),
                execution),
            ManualSupplementEntryActionText = ResolveManualSupplementEntryActionText(execution),
            ScreenshotReserved = execution?.ScreenshotReserved ?? "reserved",
            EvidenceReserved = execution?.EvidenceReserved ?? "reserved",
            AiAnalysisReserved = execution?.AiAnalysisReserved ?? "reserved"
        };
    }

    private InspectionTaskRecordModel? ResolveTaskRecord(GroupWorkspaceState? workspace, string pointId)
    {
        if (workspace is null)
        {
            return null;
        }

        foreach (var task in EnumerateTaskCandidates(workspace))
        {
            if (task.FindPointExecution(pointId) is not null)
            {
                return task;
            }
        }

        return null;
    }

    private InspectionTaskPointExecutionModel? ResolvePointExecution(GroupWorkspaceState? workspace, string pointId)
    {
        return ResolveTaskRecord(workspace, pointId)?.FindPointExecution(pointId);
    }

    private string ResolveExecutionTaskId(GroupWorkspaceState? workspace, string pointId)
    {
        return ResolveTaskRecord(workspace, pointId)?.TaskId ?? string.Empty;
    }

    private string ResolveReviewWallEntryStatus(
        InspectionTaskRecordModel? task,
        string? pointId)
    {
        if (string.IsNullOrWhiteSpace(pointId))
        {
            return "复核墙暂存：当前点位尚未形成异常流转写回。";
        }

        return task?.FindReviewWallEntry(pointId) is not null
            ? "复核墙暂存：已进入复核墙暂存集合，待人工复核。"
            : "复核墙暂存：当前未进入复核墙暂存集合。";
    }

    private string ResolveDispatchCandidateEntryStatus(
        InspectionTaskRecordModel? task,
        InspectionTaskPointExecutionModel? execution,
        string? pointId)
    {
        if (string.IsNullOrWhiteSpace(pointId))
        {
            return "派单池候选：当前点位尚未形成异常流转写回。";
        }

        var entry = task?.FindDispatchPoolEntry(pointId);
        if (entry is null)
        {
            return "派单池候选：当前未写入派单池候选集合。";
        }

        if (string.Equals(entry.RecoveryStatus, InspectionRecoveryValueKeys.Recovered, StringComparison.Ordinal))
        {
            return $"派单池候选：已恢复，确认时间 {entry.RecoveryConfirmedAt}。";
        }

        if (execution?.ReopenTriggered == true)
        {
            return "派单池候选：同点位同故障已恢复后再次出现，本轮已重开新的待派单快照。";
        }

        if (execution?.DispatchDeduplicated == true)
        {
            return "派单池候选：同点位同故障仍未恢复，本轮已幂等合并到现有快照。";
        }

        return string.Equals(entry.DispatchStatus, InspectionDispatchValueKeys.PendingDispatch, StringComparison.Ordinal)
            ? "派单池候选：已桥接到派单模块待派单快照。"
            : string.Equals(entry.DispatchStatus, InspectionDispatchValueKeys.Dispatched, StringComparison.Ordinal)
                ? "派单池候选：已派单，待恢复确认。"
                : "派单池候选：已写入派单池候选集合，待桥接待派单快照。";
    }

    private string ResolveManualSupplementEntryStatus(
        InspectionTaskRecordModel? task,
        string? pointId)
    {
        if (string.IsNullOrWhiteSpace(pointId))
        {
            return "人工补图兼容标记：当前点位尚未形成兼容标记。";
        }

        return task?.FindManualReviewCompatibilityEntry(pointId) is not null
            ? "人工补图兼容标记：字段仍保留兼容，本轮不再作为主线入口。"
            : "人工补图兼容标记：当前无兼容标记。";
    }

    private string BuildBusinessRoutingDescription(
        InspectionTaskRecordModel? task,
        InspectionTaskPointExecutionModel? execution,
        string? pointId)
    {
        if (string.IsNullOrWhiteSpace(pointId))
        {
            return "AI智能巡检中心尚未为当前点位写回异常流转入口。";
        }

        var abnormalFlow = task?.AbnormalFlow ?? InspectionTaskAbnormalFlowModel.Empty;
        var pointSegments = new List<string>();

        if (task?.FindReviewWallEntry(pointId) is not null)
        {
            pointSegments.Add("已进入复核墙暂存");
        }

        if (task?.FindDispatchPoolEntry(pointId) is not null)
        {
            var dispatchEntry = task.FindDispatchPoolEntry(pointId)!;
            pointSegments.Add(string.Equals(dispatchEntry.RecoveryStatus, InspectionRecoveryValueKeys.Recovered, StringComparison.Ordinal)
                ? $"已恢复确认（{dispatchEntry.RecoveryConfirmedAt}）"
                : string.Equals(dispatchEntry.DispatchStatus, InspectionDispatchValueKeys.PendingDispatch, StringComparison.Ordinal)
                    ? "已桥接待派单快照"
                    : string.Equals(dispatchEntry.DispatchStatus, InspectionDispatchValueKeys.Dispatched, StringComparison.Ordinal)
                        ? "已派单待恢复"
                        : "已进入派单池候选");
        }

        if (task?.FindManualReviewCompatibilityEntry(pointId) is not null)
        {
            pointSegments.Add("保留人工补图兼容标记");
        }

        if (pointSegments.Count == 0)
        {
            pointSegments.Add("当前未触发正式处置入口");
        }

        var compatibilitySuffix = abnormalFlow.ManualReviewCompatibilityCount > 0
            ? $" 人工补图仅保留兼容标记 {abnormalFlow.ManualReviewCompatibilityCount} 个。"
            : string.Empty;
        return $"AI智能巡检中心异常流转：{string.Join("，", pointSegments)}。当前任务累计复核墙暂存 {abnormalFlow.ReviewWallPendingCount} 个，派单池候选 {abnormalFlow.DispatchPoolCandidateCount} 个，待派单快照 {abnormalFlow.DispatchPendingCount} 个，已恢复 {abnormalFlow.DispatchRecoveredCount} 个。{compatibilitySuffix}".Trim();
    }

    private static string AppendDispatchRoutingDescription(
        string description,
        InspectionTaskPointExecutionModel? execution)
    {
        if (execution?.ReopenTriggered == true)
        {
            return $"{description} 同点位同故障已恢复后再次出现，本轮重开新的待派单快照。";
        }

        if (execution?.DispatchDeduplicated == true)
        {
            return $"{description} 同点位同故障仍未恢复，本轮幂等合并到现有快照。";
        }

        return description;
    }

    private static string ResolveManualSupplementEntryActionText(InspectionTaskPointExecutionModel? execution)
    {
        return string.Empty;
    }

    private string ResolveDispatchPoolEntryText(InspectionTaskRecordModel? task, string? pointId)
    {
        return task?.FindDispatchPoolEntry(pointId) is not null
            ? _textService.Resolve(TextTokens.InspectionDispatchPoolYes)
            : _textService.Resolve(TextTokens.InspectionDispatchPoolNo);
    }

    private void UpdateTaskAbnormalFlowPresentation(InspectionTaskRecordModel? task)
    {
        var abnormalFlow = task?.AbnormalFlow ?? InspectionTaskAbnormalFlowModel.Empty;
        TaskRoutingSummary = BuildTaskRoutingSummary(abnormalFlow);
        ReviewWallEntrySummary = BuildReviewWallEntrySummary(abnormalFlow);
        DispatchPoolCandidateSummary = BuildDispatchPoolCandidateSummary(abnormalFlow);
    }

    private static string BuildTaskRoutingSummary(InspectionTaskAbnormalFlowModel abnormalFlow)
    {
        var segments = new List<string>
        {
            $"复核墙暂存 {abnormalFlow.ReviewWallPendingCount} 个",
            $"派单池候选 {abnormalFlow.DispatchPoolCandidateCount} 个",
            $"待派单快照 {abnormalFlow.DispatchPendingCount} 个",
            $"已恢复 {abnormalFlow.DispatchRecoveredCount} 个"
        };

        if (abnormalFlow.ManualReviewCompatibilityCount > 0)
        {
            segments.Add($"人工补图兼容标记 {abnormalFlow.ManualReviewCompatibilityCount} 个");
        }

        return $"AI智能巡检中心任务级异常流转快照：{string.Join("，", segments)}。";
    }

    private static string BuildReviewWallEntrySummary(InspectionTaskAbnormalFlowModel abnormalFlow)
    {
        return abnormalFlow.ReviewWallPendingCount > 0
            ? $"复核墙入口：当前任务已有 {abnormalFlow.ReviewWallPendingCount} 个点位进入复核墙暂存集合。"
            : "复核墙入口：当前任务暂无待复核暂存点位。";
    }

    private static string BuildDispatchPoolCandidateSummary(InspectionTaskAbnormalFlowModel abnormalFlow)
    {
        return abnormalFlow.DispatchPoolCandidateCount > 0
            ? abnormalFlow.DispatchPendingCount > 0 || abnormalFlow.DispatchRecoveredCount > 0
                ? $"派单池候选：当前任务已有 {abnormalFlow.DispatchPoolCandidateCount} 个点位进入派单池候选集合，其中 {abnormalFlow.DispatchPendingCount} 个待派单，{abnormalFlow.DispatchRecoveredCount} 个已恢复。"
                : $"派单池候选：当前任务已有 {abnormalFlow.DispatchPoolCandidateCount} 个点位进入派单池候选集合。"
            : "派单池候选：当前任务暂无可承接的派单池候选点位。";
    }

#if false
    private InspectionPointPreviewSessionModel? ResolvePreviewSession(
        string pointId,
        InspectionTaskPointExecutionModel? execution)
    {
        if (!string.IsNullOrWhiteSpace(execution?.PreviewUrl))
        {
            var session = new InspectionPointPreviewSessionModel(
                SelectedGroup?.Id ?? string.Empty,
                execution.PointId,
                execution.DeviceCode,
                execution.PreviewUrl,
                execution.PreviewHostKind ?? string.Empty,
                execution.ExecutionSummary)
            {
                OnlineCheckResult = execution.OnlineCheckResult ?? string.Empty,
                StreamUrlAcquireResult = execution.StreamUrlAcquireResult ?? string.Empty,
                PlaybackAttemptCount = execution.PlaybackAttemptCount,
                ProtocolFallbackUsed = execution.ProtocolFallbackUsed,
                FinalPlaybackResult = execution.FinalPlaybackResult ?? string.Empty
            };
            _previewSessionByPointId[pointId] = session;
            return session;
        }

        return _previewSessionByPointId.GetValueOrDefault(pointId);
    }

    private static Uri? ResolvePreviewHostUri(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        var previewUrl = !string.IsNullOrWhiteSpace(execution?.PreviewUrl)
            ? execution.PreviewUrl
            : previewSession?.PreviewUrl;
        if (string.IsNullOrWhiteSpace(previewUrl))
        {
            return null;
        }

        return Uri.TryCreate(previewUrl, UriKind.Absolute, out var previewUri)
            ? previewUri
            : null;
    }

    private string ResolveDetailPlaybackStatus(
        InspectionPointState point,
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        if (!string.IsNullOrWhiteSpace(playbackResult)
            && !string.Equals(playbackResult, "播放成功", StringComparison.Ordinal))
        {
            return playbackResult;
        }

        return summary.FaultType == "鏆傛棤" ? point.FaultType : summary.FaultType;
/*
        return string.IsNullOrWhiteSpace(playbackResult)
            ? point.PlaybackStatus
            : playbackResult;
    }

    private static string ResolveDetailFaultType(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        if (string.IsNullOrWhiteSpace(playbackResult))
        {
        return summary.FaultType == "鏆傛棤" ? point.FaultType : summary.FaultType;
*/
    }

        if (!string.Equals(playbackResult, "播放成功", StringComparison.Ordinal))
        {
            return playbackResult;
        }

        return summary.FaultType == "鏆傛棤" ? point.FaultType : summary.FaultType;
        if (!string.IsNullOrWhiteSpace(playbackResult)
            && !string.Equals(execution.FinalPlaybackResult, "播放成功", StringComparison.Ordinal))
        {
            return execution.FinalPlaybackResult;
        }

        return summary.FaultType == "暂无" ? point.FaultType : summary.FaultType;
    }

    private static string ResolveDetailSummary(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        if (!string.IsNullOrWhiteSpace(execution?.RecoverySummary))
        {
            return execution.RecoverySummary;
        }

        if (!string.IsNullOrWhiteSpace(execution?.ExecutionSummary))
        {
            return execution.ExecutionSummary;
        }

        return string.IsNullOrWhiteSpace(summary.StatusSummary) || summary.StatusSummary == "暂无"
            ? point.FaultDescription
            : summary.StatusSummary;
    }

    private static string ResolvePreviewBusinessTitle(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointState point)
    {
        return execution?.FinalPlaybackResult switch
        {
            "播放成功" => "实时预览已接入",
            "在线检查失败" => "设备未通过在线检查",
            "无流地址" => "未获取到可用流地址",
            "播放超时" => "播放校验超时",
            "协议切换后仍失败" => "协议切换后仍未恢复",
            _ => point.IsPreviewAvailable ? "实时预览已接入" : "预览待建立"
        };
    }

    private static string ResolvePreviewBusinessDescription(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointState point)
    {
        var playbackDescription = execution?.FinalPlaybackResult switch
        {
            "播放成功" => "已完成在线检查、流地址获取和播放校验，右侧已切换为实时预览宿主。",
            "在线检查失败" => "设备当前未通过在线检查，本轮未继续推进流地址获取与播放校验。",
            "无流地址" => "设备在线，但平台未返回可用流地址，本轮无法建立右侧实时预览。",
            "播放超时" => "已获取流地址，但在本轮设定超时内未建立可用播放，右侧保留业务失败说明。",
            "协议切换后仍失败" => "已执行播放失败重试和协议切换重试，但仍未建立可用播放，下一轮可继续承接截图取证。",
            _ => point.IsPreviewAvailable
                ? "当前点位已具备预览条件，可在右侧查看实时画面。"
                : "当前点位尚未形成可用预览，本轮先展示业务状态说明。"
        };

        if (execution is null || string.IsNullOrWhiteSpace(execution.EvidenceSummary))
        {
            return playbackDescription;
        }

        return $"{playbackDescription} {execution.EvidenceSummary}";
    }

    private string ResolveDetailPlaybackStatus(
        InspectionPointState point,
        InspectionTaskPointExecutionModel? execution)
    {
        return ResolveDetailPlaybackStatus(point, execution, null);
    }

    private static string ResolveDetailFaultType(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution)
    {
        return ResolveDetailFaultType(point, summary, execution, null);
    }

    private static string ResolveDetailSummary(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution)
    {
        return ResolveDetailSummary(point, summary, execution, null);
    }

    private static string ResolvePreviewBusinessTitle(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession,
        InspectionPointState point)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        return playbackResult switch
        {
            "播放成功" => "实时预览已接入",
            "在线检查失败" => "设备未通过在线检查",
            "无流地址" => "未获取到可用视频流",
            "协议切换后仍失败" => "当前点位暂未建立预览",
            _ => point.IsPreviewAvailable ? "实时预览已接入" : "预览待建立"
        };
    }

    private static string ResolvePreviewBusinessDescription(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession,
        InspectionPointState point)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        var playbackDescription = playbackResult switch
        {
            "播放成功" => "已完成流地址获取并切换到当前点位的真实预览。",
            "在线检查失败" => "当前点位未通过在线检查，本轮未继续建立右侧预览。",
            "无流地址" => "当前点位暂未返回可直接预览的视频流。",
            "协议切换后仍失败" => "已尝试协议切换，但当前点位仍未建立稳定预览。",
            "画面异常" => "已接入当前点位预览，同时识别到画面异常，等待后续复核。",
            _ => point.IsPreviewAvailable
                ? "右侧预览区已优先承载当前选中点位。"
                : "当前点位暂未建立真实预览，可先查看基础详情与最近结论。"
        };

        if (execution is null || string.IsNullOrWhiteSpace(execution.EvidenceSummary))
        {
            return playbackDescription;
        }

        return $"{playbackDescription} {execution.EvidenceSummary}";
    }

    #endif

    private InspectionPointPreviewSessionModel? ResolvePreviewSession(
        string pointId,
        InspectionTaskPointExecutionModel? execution)
    {
        if (!string.IsNullOrWhiteSpace(execution?.PreviewUrl))
        {
            var session = new InspectionPointPreviewSessionModel(
                SelectedGroup?.Id ?? string.Empty,
                execution.PointId,
                execution.DeviceCode,
                execution.PreviewUrl,
                execution.PreviewHostKind ?? string.Empty,
                execution.ExecutionSummary)
            {
                OnlineCheckResult = execution.OnlineCheckResult ?? string.Empty,
                StreamUrlAcquireResult = execution.StreamUrlAcquireResult ?? string.Empty,
                PlaybackAttemptCount = execution.PlaybackAttemptCount,
                ProtocolFallbackUsed = execution.ProtocolFallbackUsed,
                FinalPlaybackResult = execution.FinalPlaybackResult ?? string.Empty
            };
            _previewSessionByPointId[pointId] = session;
            return session;
        }

        return _previewSessionByPointId.GetValueOrDefault(pointId);
    }

    private static bool HasDisplayValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, "--", StringComparison.Ordinal)
            && !string.Equals(value, "暂无", StringComparison.Ordinal);
    }

    private static Uri? ResolvePreviewHostUri(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        var previewUrl = !string.IsNullOrWhiteSpace(execution?.PreviewUrl)
            ? execution.PreviewUrl
            : previewSession?.PreviewUrl;
        if (string.IsNullOrWhiteSpace(previewUrl))
        {
            return null;
        }

        return Uri.TryCreate(previewUrl, UriKind.Absolute, out var previewUri)
            ? previewUri
            : null;
    }

    private string ResolveDetailPlaybackStatus(
        InspectionPointState point,
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        if (string.IsNullOrWhiteSpace(playbackResult))
        {
            return point.PlaybackStatus;
        }

        return string.Equals(playbackResult, "播放成功", StringComparison.Ordinal)
            ? _textService.Resolve(TextTokens.InspectionPlaybackPlayable)
            : playbackResult;
    }

    private string ResolveDetailPlaybackStatus(
        InspectionPointState point,
        InspectionTaskPointExecutionModel? execution)
    {
        return ResolveDetailPlaybackStatus(point, execution, null);
    }

    private static string ResolveDetailFaultType(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        if (!string.IsNullOrWhiteSpace(playbackResult)
            && !string.Equals(playbackResult, "播放成功", StringComparison.Ordinal))
        {
            return playbackResult;
        }

        return HasDisplayValue(summary.FaultType)
            ? summary.FaultType
            : point.FaultType;
    }

    private static string ResolveDetailFaultType(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution)
    {
        return ResolveDetailFaultType(point, summary, execution, null);
    }

    private static string ResolveDetailSummary(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession)
    {
        if (!string.IsNullOrWhiteSpace(execution?.RecoverySummary))
        {
            return execution.RecoverySummary;
        }

        if (!string.IsNullOrWhiteSpace(execution?.ExecutionSummary))
        {
            return execution.ExecutionSummary;
        }

        if (HasDisplayValue(previewSession?.ResultSummary))
        {
            return previewSession!.ResultSummary;
        }

        return HasDisplayValue(summary.StatusSummary)
            ? summary.StatusSummary
            : point.FaultDescription;
    }

    private static string ResolveDetailSummary(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution)
    {
        return ResolveDetailSummary(point, summary, execution, null);
    }

    private static string ResolvePreviewBusinessTitle(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession,
        InspectionPointState point)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        return playbackResult switch
        {
            "播放成功" => "实时预览已接入",
            "在线检查失败" => "设备未通过在线检查",
            "无流地址" => "未获取到可用视频流",
            "协议切换后仍失败" => "当前点位暂未建立预览",
            _ => point.IsPreviewAvailable ? "实时预览已接入" : "预览准备中"
        };
    }

    private static string ResolvePreviewBusinessDescription(
        InspectionTaskPointExecutionModel? execution,
        InspectionPointPreviewSessionModel? previewSession,
        InspectionPointState point)
    {
        var playbackResult = !string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? execution.FinalPlaybackResult
            : previewSession?.FinalPlaybackResult;
        var playbackDescription = playbackResult switch
        {
            "播放成功" => "已完成流地址获取，并切换到当前点位的真实预览。",
            "在线检查失败" => "当前点位未通过在线检查，本轮不建立右侧真实预览。",
            "无流地址" => "当前点位暂未返回可播放流地址，右侧保留业务化失败说明。",
            "协议切换后仍失败" => "已经执行协议切换重试，但当前点位仍未建立稳定预览。",
            "画面异常" => "当前点位已接入真实预览，同时识别到画面异常，等待后续复核。",
            _ => point.IsPreviewAvailable
                ? "右侧预览区已优先承载当前选中点位。"
                : "当前点位暂未建立真实预览，可先查看基础详情与最近结论。"
        };

        if (execution is null || string.IsNullOrWhiteSpace(execution.EvidenceSummary))
        {
            return playbackDescription;
        }

        return $"{playbackDescription} {execution.EvidenceSummary}";
    }

    private static string AppendAiSummary(string baseSummary, InspectionTaskPointExecutionModel? execution)
    {
        if (execution is null || string.IsNullOrWhiteSpace(execution.AiAnalysisSummary))
        {
            return baseSummary;
        }

        if (string.IsNullOrWhiteSpace(baseSummary))
        {
            return execution.AiAnalysisSummary;
        }

        return $"{baseSummary} {execution.AiAnalysisSummary}";
    }

    private static string ResolveAiRecognitionSummary(InspectionTaskPointExecutionModel? execution)
    {
        if (execution is null)
        {
            return "AI画面分析待执行";
        }

        return execution.AiAnalysisStatus switch
        {
            InspectionEvidenceValueKeys.AiAnalysisCompleted when execution.IsAiAbnormalDetected
                => $"AI识别异常，置信度 {execution.AiConfidence:P0}",
            InspectionEvidenceValueKeys.AiAnalysisCompleted
                => $"AI未识别异常，置信度 {execution.AiConfidence:P0}",
            InspectionEvidenceValueKeys.AiAnalysisFailed => "AI画面分析未完成",
            InspectionEvidenceValueKeys.AiAnalysisPending => "AI画面分析进行中",
            _ => "AI画面分析待执行"
        };
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

    private string ResolveTaskTypeText(InspectionTaskTypeModel taskType)
    {
        return taskType switch
        {
            InspectionTaskTypeModel.SinglePoint => _textService.Resolve(TextTokens.InspectionTaskTypeSinglePoint),
            InspectionTaskTypeModel.Batch => _textService.Resolve(TextTokens.InspectionTaskTypeBatch),
            _ => _textService.Resolve(TextTokens.InspectionTaskTypeScopePlan)
        };
    }

    private string ResolveTaskTriggerText(InspectionTaskTriggerModel triggerMode)
    {
        return triggerMode == InspectionTaskTriggerModel.Scheduled
            ? _textService.Resolve(TextTokens.InspectionTaskTriggerScheduled)
            : _textService.Resolve(TextTokens.InspectionTaskTriggerManual);
    }

    private string ResolveTaskStatusText(InspectionTaskStatusModel status)
    {
        return status switch
        {
            InspectionTaskStatusModel.Pending => _textService.Resolve(TextTokens.InspectionTaskStatusPending),
            InspectionTaskStatusModel.Running => _textService.Resolve(TextTokens.InspectionTaskStatusExecuting),
            InspectionTaskStatusModel.Completed => _textService.Resolve(TextTokens.InspectionTaskStatusCompletedBusiness),
            InspectionTaskStatusModel.PartialFailure => _textService.Resolve(TextTokens.InspectionTaskStatusPartialFailure),
            InspectionTaskStatusModel.Failed => _textService.Resolve(TextTokens.InspectionTaskStatusFailed),
            InspectionTaskStatusModel.Cancelled => _textService.Resolve(TextTokens.InspectionTaskStatusCancelled),
            _ => TaskEmptyText
        };
    }

    private Dictionary<string, GroupWorkspaceState> CreateWorkspaces(InspectionWorkspaceSnapshot snapshot)
    {
        return snapshot.Groups
            .Select(workspace =>
            {
                var inspectionPoints = new ObservableCollection<InspectionPointState>(workspace.Points.Select(CreatePoint));
                var mapPoints = new ObservableCollection<MapPointState>(inspectionPoints.Select(CreateMapPoint));
                var taskBoard = workspace.TaskBoard;
                var scopePlans = NormalizeScopePlans(workspace);
                var currentTask = taskBoard.CurrentTask is null
                    ? null
                    : CreateTaskSummaryState(taskBoard.CurrentTask);
                var recentTasks = new ObservableCollection<InspectionTaskSummaryState>(taskBoard.RecentTasks.Select(CreateTaskSummaryState));

                var groupWorkspace = new GroupWorkspaceState(
                    new InspectionGroupSummaryState(
                        workspace.Group.Id,
                        workspace.Group.Name,
                        workspace.Group.Summary,
                        workspace.Group.IsEnabled),
                    scopePlans,
                    CreateScopePlanOptions(scopePlans),
                    workspace.ScopePlanPreview ?? scopePlans.FirstOrDefault()?.Preview ?? CreateEmptyScopePlanPreview(),
                    string.IsNullOrWhiteSpace(workspace.ExecutionScopePlanId)
                        ? scopePlans.FirstOrDefault()?.PlanId ?? string.Empty
                        : workspace.ExecutionScopePlanId,
                    string.IsNullOrWhiteSpace(workspace.ExecutionScopePlanName)
                        ? scopePlans.FirstOrDefault()?.PlanName ?? TaskEmptyText
                        : workspace.ExecutionScopePlanName,
                    new InspectionStrategySummaryState(
                        workspace.Strategy.FirstRunTime,
                        workspace.Strategy.DailyExecutionCount,
                        workspace.Strategy.Interval,
                        workspace.Strategy.ResultMode,
                        workspace.Strategy.DispatchMode),
                    CreateExecutionState(workspace.Execution),
                    CreateRunSummary(workspace.RunSummary),
                    workspace.TaskFinishedAt,
                    taskBoard,
                    currentTask,
                    recentTasks,
                    inspectionPoints,
                    mapPoints,
                    new ObservableCollection<RecentFaultSummaryState>(workspace.RecentFaults.Select(CreateRecentFault)));
                RefreshSummary(groupWorkspace);
                return groupWorkspace;
            })
            .ToDictionary(workspace => workspace.Group.Id, workspace => workspace);
    }

    private IReadOnlyList<InspectionScopePlanSnapshotModel> NormalizeScopePlans(InspectionGroupWorkspaceModel workspace)
    {
        if (workspace.ScopePlanSnapshots is { Count: > 0 })
        {
            return workspace.ScopePlanSnapshots;
        }

        var preview = workspace.ScopePlanPreview ?? CreateEmptyScopePlanPreview();
        return
        [
            new InspectionScopePlanSnapshotModel(
                string.IsNullOrWhiteSpace(preview.PlanId) ? "__default_scope__" : preview.PlanId,
                string.IsNullOrWhiteSpace(preview.PlanName) ? TaskEmptyText : preview.PlanName,
                true,
                preview,
                [])
        ];
    }

    private ObservableCollection<InspectionScopePlanOptionState> CreateScopePlanOptions(
        IReadOnlyList<InspectionScopePlanSnapshotModel> scopePlans)
    {
        return new ObservableCollection<InspectionScopePlanOptionState>(
            scopePlans.Select(plan => new InspectionScopePlanOptionState(
                plan.PlanId,
                plan.IsDefault ? $"{plan.PlanName}（默认）" : plan.PlanName)));
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
            CurrentPointName = "--",
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

    private static InspectionScopePlanPreviewModel CreateEmptyScopePlanPreview()
        => new(string.Empty, "--", string.Empty, string.Empty, string.Empty, 0, 0, string.Empty);

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
            CurrentPointName = "--",
            SimulationNote = _textService.Resolve(TextTokens.InspectionHistoryPlaceholder),
            IsEnabled = isEnabled
        };
    }

    private InspectionTaskSummaryState CreateTaskSummaryState(InspectionTaskRecordModel task)
    {
        return new InspectionTaskSummaryState
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            TaskTypeText = ResolveTaskTypeText(task.TaskType),
            TriggerText = ResolveTaskTriggerText(task.TriggerMode),
            StatusText = ResolveTaskStatusText(task.Status),
            ScopePlanText = string.IsNullOrWhiteSpace(task.ScopePlanName) ? TaskEmptyText : task.ScopePlanName,
            CurrentPointText = task.ResolveCurrentPointDisplayName(),
            TimeText = task.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss")
                ?? task.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss")
                ?? task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            Summary = string.IsNullOrWhiteSpace(task.Summary) ? TaskEmptyText : task.Summary
        };
    }

    private IEnumerable<InspectionTaskRecordModel> EnumerateTaskCandidates(GroupWorkspaceState workspace)
    {
        var taskIds = new HashSet<string>(StringComparer.Ordinal);

        if (workspace.TaskBoard.CurrentTask is not null
            && taskIds.Add(workspace.TaskBoard.CurrentTask.TaskId))
        {
            yield return workspace.TaskBoard.CurrentTask;
        }

        foreach (var task in workspace.TaskBoard.RecentTasks)
        {
            if (taskIds.Add(task.TaskId))
            {
                yield return task;
            }
        }
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
            point.MapLongitude,
            point.MapLatitude,
            point.RegisteredLongitude,
            point.RegisteredLatitude,
            point.RegisteredCoordinateSystem,
            point.MapCoordinateSystem,
            point.CanRenderOnMap,
            point.CoordinateStatusText,
            point.RawLongitude,
            point.RawLatitude,
            point.CoordinateStatus,
            point.MapSource,
            point.X,
            point.Y,
            MapPointStatus(point.Status),
            ResolvePointStatus(MapPointStatus(point.Status)),
            MapPointStatus(point.CompletionStatus),
            point.IsOnline,
            point.IsPlayable,
            point.IsImageAbnormal,
            point.IsPreviewAvailable,
            point.FaultSummary,
            point.LastFaultTime,
            point.EntersDispatchPool,
            point.BusinessSummary,
            point.IsInDefaultScope,
            point.ScopeDecisionSummary);
    }

    private InspectionPointState CreatePoint(
        string id,
        string deviceCode,
        string name,
        string unitName,
        string currentHandlingUnit,
        double? mapLongitude,
        double? mapLatitude,
        double? registeredLongitude,
        double? registeredLatitude,
        CoordinateSystemKind registeredCoordinateSystem,
        CoordinateSystemKind mapCoordinateSystem,
        bool canRenderOnMap,
        string coordinateStatusText,
        string? rawLongitude,
        string? rawLatitude,
        PointCoordinateStatus coordinateStatus,
        string mapSource,
        double x,
        double y,
        InspectionPointStatus status,
        string statusText,
        InspectionPointStatus completionStatus,
        bool? isOnline,
        bool isPlayable,
        bool isImageAbnormal,
        bool isPreviewAvailable,
        string faultSummary,
        string lastFaultTime,
        bool entersDispatchPool,
        PointBusinessSummaryModel? businessSummary,
        bool isInDefaultScope,
        string scopeDecisionSummary)
    {
        var faultType = ResolveFaultType(status, completionStatus, isOnline, isPlayable, isImageAbnormal);
        var pointSummary = businessSummary is not null
            ? PointBusinessSummaryState.FromModel(businessSummary)
            : PointBusinessSummaryState.CreateFallback(
                id,
                deviceCode,
                name,
                canRenderOnMap ? mapLongitude ?? registeredLongitude : null,
                canRenderOnMap ? mapLatitude ?? registeredLatitude : null,
                "demo",
                coordinateStatusText,
                ResolveOnlineStatus(isOnline),
                unitName,
                BuildFaultDescription(status, completionStatus, isOnline, isPlayable, isImageAbnormal, faultSummary, canRenderOnMap, coordinateStatusText),
                faultType,
                canRenderOnMap
                    ? "进入AI巡检 / 查看报表"
                    : "进入AI巡检 / 坐标补录预留 / 查看报表",
                !canRenderOnMap);
        var onlineStatus = businessSummary is not null
            ? pointSummary.OnlineStatus
            : ResolveOnlineStatus(isOnline);
        var resolvedFaultType = businessSummary is not null
            ? pointSummary.FaultType
            : faultType;
        var resolvedFaultDescription = businessSummary is not null
            ? pointSummary.StatusSummary
            : BuildFaultDescription(status, completionStatus, isOnline, isPlayable, isImageAbnormal, faultSummary, canRenderOnMap, coordinateStatusText);

        return new InspectionPointState(
            id,
            deviceCode,
            name,
            unitName,
            currentHandlingUnit,
            mapLongitude,
            mapLatitude,
            registeredLongitude,
            registeredLatitude,
            registeredCoordinateSystem,
            mapCoordinateSystem,
            canRenderOnMap,
            coordinateStatusText,
            rawLongitude,
            rawLatitude,
            coordinateStatus,
            mapSource,
            x,
            y,
            status,
            statusText,
            completionStatus,
            onlineStatus,
            ResolvePlaybackStatus(isOnline, isPlayable),
            ResolveImageStatus(isOnline, isImageAbnormal),
            resolvedFaultType,
            resolvedFaultDescription,
            lastFaultTime,
            entersDispatchPool ? _textService.Resolve(TextTokens.InspectionDispatchPoolYes) : _textService.Resolve(TextTokens.InspectionDispatchPoolNo),
            ResolveConclusion(status, completionStatus, businessSummary?.StatusSummary, faultSummary),
            isInDefaultScope,
            string.IsNullOrWhiteSpace(scopeDecisionSummary) ? TaskEmptyText : scopeDecisionSummary,
            isPreviewAvailable,
            pointSummary)
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
        bool? isOnline,
        bool isPlayable,
        bool isImageAbnormal)
    {
        var effectiveStatus = status == InspectionPointStatus.Pending ? completionStatus : status;
        if (effectiveStatus is InspectionPointStatus.Normal or InspectionPointStatus.Silent)
        {
            return _textService.Resolve(TextTokens.InspectionFaultTypeNone);
        }

        if (isOnline == false)
        {
            return _textService.Resolve(TextTokens.InspectionFaultTypeOffline);
        }

        if (isOnline == true && !isPlayable)
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
        bool? isOnline,
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

        if (isOnline == false)
        {
            return "设备当前离线，后续应展示接口重试结果与责任单位跟进信息。";
        }

        if (isOnline == true && !isPlayable)
        {
            return "播放检查失败，后续在此承接重试与协议切换过程说明。";
        }

        if (isImageAbnormal)
        {
            return "画面疑似异常，后续接入接口判定结果与本地截图分析说明。";
        }

        if (isOnline is null)
        {
            return "当前点位在线状态待接入，先展示目录与地图基础信息。";
        }

        return "当前点位状态正常，本轮用于展示列表与中台联动骨架。";
    }

    private string ResolveOnlineStatus(bool? isOnline)
    {
        return isOnline switch
        {
            true => _textService.Resolve(TextTokens.InspectionOnlineOnline),
            false => _textService.Resolve(TextTokens.InspectionOnlineOffline),
            _ => "待接入"
        };
    }

    private string ResolvePlaybackStatus(bool? isOnline, bool isPlayable)
    {
        return isOnline switch
        {
            null => "待接入",
            false => "待确认",
            _ => isPlayable
                ? _textService.Resolve(TextTokens.InspectionPlaybackPlayable)
                : _textService.Resolve(TextTokens.InspectionPlaybackFailed)
        };
    }

    private string ResolveImageStatus(bool? isOnline, bool isImageAbnormal)
    {
        return isOnline switch
        {
            null => "待接入",
            false => "待确认",
            _ => isImageAbnormal
                ? _textService.Resolve(TextTokens.InspectionImageAbnormal)
                : _textService.Resolve(TextTokens.InspectionImageNormal)
        };
    }

    private static string ResolveDetailFaultType(
        InspectionPointState point,
        PointBusinessSummaryState summary)
    {
        return summary.FaultType == "暂无" ? point.FaultType : summary.FaultType;
    }

    private static string ResolveDetailSummary(
        InspectionPointState point,
        PointBusinessSummaryState summary)
    {
        return string.IsNullOrWhiteSpace(summary.StatusSummary) || summary.StatusSummary == "暂无"
            ? point.FaultDescription
            : summary.StatusSummary;
    }

    private string ResolveConclusion(
        InspectionPointStatus status,
        InspectionPointStatus completionStatus,
        string? businessSummary = null,
        string? faultSummary = null)
    {
        if ((!string.IsNullOrWhiteSpace(faultSummary) && faultSummary.Contains("确认恢复", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(businessSummary) && businessSummary.Contains("确认恢复", StringComparison.Ordinal)))
        {
            return "已恢复";
        }

        var effectiveStatus = status == InspectionPointStatus.Pending ? completionStatus : status;

        return effectiveStatus is InspectionPointStatus.Fault or InspectionPointStatus.PausedUntilRecovery
            ? _textService.Resolve(TextTokens.InspectionConclusionFault)
            : _textService.Resolve(TextTokens.InspectionConclusionNormal);
    }

    private MapPointState CreateMapPoint(InspectionPointState point)
    {
        return MapPointStateFactory.Create(
            point.Id,
            point.DeviceCode,
            point.Name,
            point.UnitName,
            point.CurrentHandlingUnit,
            point.MapLongitude,
            point.MapLatitude,
            point.RegisteredLongitude,
            point.RegisteredLatitude,
            point.RegisteredCoordinateSystem,
            point.MapCoordinateSystem,
            point.CanRenderOnMap,
            point.CoordinateStatusText,
            point.RawLongitude,
            point.RawLatitude,
            point.CoordinateStatus,
            point.MapSource,
            point.BusinessSummary.CoordinateStatus,
            point.X,
            point.Y,
            ResolveMapVisualKind(point),
            point.MapColorCategory,
            ResolvePointStatus(point.Status),
            point.FaultType,
            point.FaultDescription,
            point.LastFaultTime,
            point.IsPreviewAvailable,
            point.IsCurrent);
    }

    private void SyncMapPoint(InspectionPointState point, ObservableCollection<MapPointState> mapPoints)
    {
        var mapPoint = mapPoints.FirstOrDefault(candidate => candidate.PointId == point.Id);
        if (mapPoint is null)
        {
            return;
        }

        mapPoint.VisualKind = ResolveMapVisualKind(point);
        mapPoint.ColorCategory = point.MapColorCategory;
        mapPoint.StatusText = ResolvePointStatus(point.Status);
        mapPoint.FaultType = point.FaultType;
        mapPoint.Summary = point.FaultDescription;
        mapPoint.LatestFaultTime = point.LastFaultTime;
        mapPoint.IsCurrent = point.IsCurrent;
    }

    private static MapPointVisualKind ResolveMapVisualKind(InspectionPointState point)
    {
        return point.Status switch
        {
            InspectionPointStatus.Inspecting => MapPointVisualKind.Inspecting,
            InspectionPointStatus.Fault => MapPointVisualKind.Fault,
            InspectionPointStatus.PausedUntilRecovery => MapPointVisualKind.Fault,
            InspectionPointStatus.Silent => MapPointVisualKind.Silent,
            _ => MapPointVisualKind.Normal
        };
    }

    private enum ScopePlanSaveVisualState
    {
        Idle,
        Saving,
        Success,
        Failure
    }

    private sealed class GroupWorkspaceState
    {
        public GroupWorkspaceState(
            InspectionGroupSummaryState group,
            IReadOnlyList<InspectionScopePlanSnapshotModel> scopePlans,
            ObservableCollection<InspectionScopePlanOptionState> scopePlanOptions,
            InspectionScopePlanPreviewModel scopePlanPreview,
            string executionScopePlanId,
            string executionScopePlanName,
            InspectionStrategySummaryState strategySummary,
            InspectionTaskExecutionState executionState,
            InspectionRunSummaryState runSummary,
            string taskFinishedAt,
            InspectionTaskBoardModel taskBoard,
            InspectionTaskSummaryState? currentTask,
            ObservableCollection<InspectionTaskSummaryState> recentTasks,
            ObservableCollection<InspectionPointState> points,
            ObservableCollection<MapPointState> mapPoints,
            ObservableCollection<RecentFaultSummaryState> recentFaults)
        {
            Group = group;
            ScopePlans = scopePlans;
            ScopePlansById = scopePlans.ToDictionary(plan => plan.PlanId, plan => plan, StringComparer.Ordinal);
            ScopePlanOptions = scopePlanOptions;
            ScopePlanPreview = scopePlanPreview;
            ExecutionScopePlanId = executionScopePlanId;
            ExecutionScopePlanName = executionScopePlanName;
            StrategySummary = strategySummary;
            ExecutionState = executionState;
            RunSummary = runSummary;
            TaskFinishedAt = taskFinishedAt;
            TaskBoard = taskBoard;
            CurrentTask = currentTask;
            RecentTasks = recentTasks;
            Points = points;
            PointsById = points.ToDictionary(point => point.Id, point => point, StringComparer.Ordinal);
            MapPoints = mapPoints;
            RecentFaults = recentFaults;
            ScopeMatchedPoints = [];
            ScopeUnmatchedPoints = [];
        }

        public InspectionGroupSummaryState Group { get; }
        public IReadOnlyList<InspectionScopePlanSnapshotModel> ScopePlans { get; }
        public IReadOnlyDictionary<string, InspectionScopePlanSnapshotModel> ScopePlansById { get; }
        public ObservableCollection<InspectionScopePlanOptionState> ScopePlanOptions { get; }
        public InspectionScopePlanPreviewModel ScopePlanPreview { get; }
        public string ExecutionScopePlanId { get; }
        public string ExecutionScopePlanName { get; }
        public string SelectedScopePlanId { get; set; } = string.Empty;
        public InspectionStrategySummaryState StrategySummary { get; }
        public InspectionTaskExecutionState ExecutionState { get; }
        public InspectionRunSummaryState RunSummary { get; }
        public string TaskFinishedAt { get; }
        public InspectionTaskBoardModel TaskBoard { get; set; }
        public InspectionTaskSummaryState? CurrentTask { get; set; }
        public ObservableCollection<InspectionTaskSummaryState> RecentTasks { get; }
        public ObservableCollection<InspectionPointState> Points { get; }
        public IReadOnlyDictionary<string, InspectionPointState> PointsById { get; }
        public ObservableCollection<MapPointState> MapPoints { get; }
        public ObservableCollection<RecentFaultSummaryState> RecentFaults { get; }
        public ObservableCollection<InspectionPointState> ScopeMatchedPoints { get; set; }
        public ObservableCollection<InspectionPointState> ScopeUnmatchedPoints { get; set; }
        public InspectionReviewTaskSummaryState? ReviewSummary { get; set; }
        public ObservableCollection<InspectionReviewCardState>? ReviewCards { get; set; }
        public InspectionReviewFilterState? ReviewFilter { get; set; }
    }

    public sealed class InspectionScopePlanOptionState : ViewModelBase
    {
        private bool _isSelected;

        public InspectionScopePlanOptionState(string planId, string displayName)
        {
            PlanId = planId;
            DisplayName = displayName;
        }

        public string PlanId { get; }

        public string DisplayName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
