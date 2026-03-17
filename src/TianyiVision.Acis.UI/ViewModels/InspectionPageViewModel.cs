using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
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
    private Dictionary<string, GroupWorkspaceState> _workspaceByGroupId;
    private readonly RelayCommand _executeInspectionCommand;
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
    private ObservableCollection<InspectionPointState> _scopeMatchedPoints = [];
    private ObservableCollection<InspectionPointState> _scopeUnmatchedPoints = [];

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
        ScopePlanNameLabel = textService.Resolve(TextTokens.InspectionScopePlanNameLabel);
        ScopeMatchedCountLabel = textService.Resolve(TextTokens.InspectionScopeMatchedCountLabel);
        ScopeUnmatchedCountLabel = textService.Resolve(TextTokens.InspectionScopeUnmatchedCountLabel);
        ScopeRuleSummaryLabel = textService.Resolve(TextTokens.InspectionScopeRuleSummaryLabel);
        ScopeExecutionSummaryLabel = textService.Resolve(TextTokens.InspectionScopeExecutionSummaryLabel);
        ScopeUnmatchedReasonLabel = textService.Resolve(TextTokens.InspectionScopeUnmatchedReasonLabel);
        ScopeMatchedListLabel = textService.Resolve(TextTokens.InspectionScopeMatchedListLabel);
        ScopeUnmatchedListLabel = textService.Resolve(TextTokens.InspectionScopeUnmatchedListLabel);
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

        _executeInspectionCommand = new RelayCommand(_ => StartGroupInspection(), _ => ExecutionState?.IsEnabled == true);
        _startSinglePointInspectionCommand = new RelayCommand(_ => StartSinglePointInspection(), _ => CanStartSinglePointInspection);
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
    public string ScopePlanNameLabel { get; }
    public string ScopeMatchedCountLabel { get; }
    public string ScopeUnmatchedCountLabel { get; }
    public string ScopeRuleSummaryLabel { get; }
    public string ScopeExecutionSummaryLabel { get; }
    public string ScopeUnmatchedReasonLabel { get; }
    public string ScopeMatchedListLabel { get; }
    public string ScopeUnmatchedListLabel { get; }
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

    public ICommand SelectGroupCommand { get; }
    public ICommand SelectPointCommand { get; }
    public ICommand SelectRecentFaultCommand { get; }
    public ICommand SelectFirstUnmappedPointCommand { get; }
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
        ScopePlanPreview = workspace.ScopePlanPreview;
        ScopeMatchedPoints = new ObservableCollection<InspectionPointState>(workspace.Points.Where(point => point.IsInDefaultScope));
        ScopeUnmatchedPoints = new ObservableCollection<InspectionPointState>(workspace.Points.Where(point => !point.IsInDefaultScope));
        MapPoints = workspace.MapPoints;
        UnmappedPoints = new ObservableCollection<MapPointState>(workspace.MapPoints.Where(point => !point.CanRenderOnMap));
        RecentFaults = workspace.RecentFaults;
        CurrentTask = workspace.CurrentTask;
        RecentTasks = workspace.RecentTasks;
        UpdateTaskAbnormalFlowPresentation(workspace.TaskBoard.CurrentTask);
        _selectFirstUnmappedPointCommand.RaiseCanExecuteChanged();

        ToggleGroupActionText = group.IsEnabled
            ? _textService.Resolve(TextTokens.InspectionActionDisable)
            : _textService.Resolve(TextTokens.InspectionActionEnable);

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

    private void HandleTaskBoardChanged(object? sender, InspectionTaskBoardChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                RequestRefreshWorkspace(e.GroupId, SelectedPoint?.Id);
                return;
            }

            dispatcher.Invoke(() => RequestRefreshWorkspace(e.GroupId, SelectedPoint?.Id));
            return;
        }

        RequestRefreshWorkspace(e.GroupId, SelectedPoint?.Id);
    }

    private void RequestRefreshWorkspace(string? preferredGroupId = null, string? preferredPointId = null)
    {
        var snapshot = _inspectionTaskService.GetWorkspace().Data;
        _workspaceByGroupId = CreateWorkspaces(snapshot);

        Groups.Clear();
        foreach (var workspace in _workspaceByGroupId.Values)
        {
            Groups.Add(workspace.Group);
        }

        var targetGroup = !string.IsNullOrWhiteSpace(preferredGroupId)
            ? Groups.FirstOrDefault(group => group.Id == preferredGroupId)
            : null;
        targetGroup ??= SelectedGroup is not null
            ? Groups.FirstOrDefault(group => group.Id == SelectedGroup.Id)
            : null;
        targetGroup ??= Groups.FirstOrDefault();

        if (targetGroup is not null)
        {
            LoadGroup(targetGroup, preferredPointId);
        }
    }

    public bool TryWritePointEvidence(InspectionPointEvidenceWriteRequest request)
    {
        var response = _inspectionTaskService.WritePointEvidence(request);
        if (!response.IsSuccess)
        {
            return false;
        }

        RequestRefreshWorkspace(response.Data.GroupId, request.PointId);
        return true;
    }

    public void UpdateMapAvailability(bool isAvailable)
    {
        IsMapAvailabilityKnown = true;
        IsRealMapAvailable = isAvailable;
        MapAvailabilityBadgeText = isAvailable ? MapModeRealText : MapModeFallbackText;
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
        var summary = point.BusinessSummary;
        SelectedPointSummary = summary;
        CurrentPointSourceType = summary.SourceType;
        SelectedPointDetail = CreatePointDetail(point, summary);
        RefreshSinglePointInspectionSummary(GetCurrentWorkspace(), point);
        _pointSelectionContext.Update(summary, nameof(InspectionPageViewModel));

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

    private void StartSinglePointInspection()
    {
        var workspace = GetCurrentWorkspace();
        if (workspace is null || SelectedPoint is null)
        {
            return;
        }

        var response = _inspectionTaskService.StartSinglePointInspection(workspace.Group.Id, SelectedPoint.Id);
        if (!response.IsSuccess)
        {
            CurrentTask = CreateTaskSummaryState(response.Data);
            UpdateTaskAbnormalFlowPresentation(response.Data);
            ApplySinglePointInspectionRecord(response.Data, SelectedPoint);
            return;
        }

        RequestRefreshWorkspace(workspace.Group.Id, SelectedPoint.Id);
    }

    private void StartGroupInspection()
    {
        if (SelectedGroup is null || !_workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace))
        {
            return;
        }

        var response = _inspectionTaskService.StartDefaultScopeInspection(workspace.Group.Id);
        if (!response.IsSuccess)
        {
            CurrentTask = CreateTaskSummaryState(response.Data);
            UpdateTaskAbnormalFlowPresentation(response.Data);
            workspace.ExecutionState.CurrentTaskStatus = ResolveTaskStatusText(response.Data.Status);
            workspace.ExecutionState.CurrentPointName = "--";
            workspace.ExecutionState.SimulationNote = response.Data.Summary;
            return;
        }

        RequestRefreshWorkspace(workspace.Group.Id, SelectedPoint?.Id);
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
        _startSinglePointInspectionCommand.RaiseCanExecuteChanged();
        RefreshSinglePointInspectionSummary(workspace, SelectedPoint);
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
        var playbackStatus = ResolveDetailPlaybackStatus(point, execution);
        var faultType = ResolveDetailFaultType(point, summary, execution);
        var faultDescription = AppendAiSummary(ResolveDetailSummary(point, summary, execution), execution);
        var previewBusinessTitle = ResolvePreviewBusinessTitle(execution, point);
        var previewBusinessDescription = AppendAiSummary(ResolvePreviewBusinessDescription(execution, point), execution);
        var previewHostUri = ResolvePreviewHostUri(execution);
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
            PreviewHostKind = execution?.PreviewHostKind ?? string.Empty,
            PreviewBusinessTitle = previewBusinessTitle,
            PreviewBusinessDescription = previewBusinessDescription,
            OnlineCheckResult = execution?.OnlineCheckResult ?? string.Empty,
            StreamUrlAcquireResult = execution?.StreamUrlAcquireResult ?? string.Empty,
            FinalPlaybackResult = execution?.FinalPlaybackResult ?? string.Empty,
            PlaybackAttemptCount = execution?.PlaybackAttemptCount ?? 0,
            ProtocolFallbackUsed = execution?.ProtocolFallbackUsed ?? false,
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

    private static Uri? ResolvePreviewHostUri(InspectionTaskPointExecutionModel? execution)
    {
        if (execution is null || string.IsNullOrWhiteSpace(execution.PreviewUrl))
        {
            return null;
        }

        return Uri.TryCreate(execution.PreviewUrl, UriKind.Absolute, out var previewUri)
            ? previewUri
            : null;
    }

    private string ResolveDetailPlaybackStatus(
        InspectionPointState point,
        InspectionTaskPointExecutionModel? execution)
    {
        return string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            ? point.PlaybackStatus
            : execution.FinalPlaybackResult;
    }

    private static string ResolveDetailFaultType(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution)
    {
        if (!string.IsNullOrWhiteSpace(execution?.FinalPlaybackResult)
            && !string.Equals(execution.FinalPlaybackResult, "播放成功", StringComparison.Ordinal))
        {
            return execution.FinalPlaybackResult;
        }

        return summary.FaultType == "暂无" ? point.FaultType : summary.FaultType;
    }

    private static string ResolveDetailSummary(
        InspectionPointState point,
        PointBusinessSummaryState summary,
        InspectionTaskPointExecutionModel? execution)
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
                    workspace.ScopePlanPreview ?? CreateEmptyScopePlanPreview(),
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

    private sealed class GroupWorkspaceState
    {
        public GroupWorkspaceState(
            InspectionGroupSummaryState group,
            InspectionScopePlanPreviewModel scopePlanPreview,
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
            ScopePlanPreview = scopePlanPreview;
            StrategySummary = strategySummary;
            ExecutionState = executionState;
            RunSummary = runSummary;
            TaskFinishedAt = taskFinishedAt;
            TaskBoard = taskBoard;
            CurrentTask = currentTask;
            RecentTasks = recentTasks;
            Points = points;
            MapPoints = mapPoints;
            RecentFaults = recentFaults;
        }

        public InspectionGroupSummaryState Group { get; }
        public InspectionScopePlanPreviewModel ScopePlanPreview { get; }
        public InspectionStrategySummaryState StrategySummary { get; }
        public InspectionTaskExecutionState ExecutionState { get; }
        public InspectionRunSummaryState RunSummary { get; }
        public string TaskFinishedAt { get; }
        public InspectionTaskBoardModel TaskBoard { get; }
        public InspectionTaskSummaryState? CurrentTask { get; }
        public ObservableCollection<InspectionTaskSummaryState> RecentTasks { get; }
        public ObservableCollection<InspectionPointState> Points { get; }
        public ObservableCollection<MapPointState> MapPoints { get; }
        public ObservableCollection<RecentFaultSummaryState> RecentFaults { get; }
        public InspectionReviewTaskSummaryState? ReviewSummary { get; set; }
        public ObservableCollection<InspectionReviewCardState>? ReviewCards { get; set; }
        public InspectionReviewFilterState? ReviewFilter { get; set; }
    }
}
