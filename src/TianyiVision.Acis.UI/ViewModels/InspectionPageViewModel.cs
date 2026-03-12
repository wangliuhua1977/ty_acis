using System.Collections.ObjectModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
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
    private ObservableCollection<RecentFaultSummaryState> _recentFaults = [];
    private InspectionPointDetailState? _selectedPointDetail;
    private InspectionPointState? _selectedPoint;
    private string _toggleGroupActionText = string.Empty;

    public InspectionPageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.InspectionTitle),
            textService.Resolve(TextTokens.InspectionDescription))
    {
        _textService = textService;

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
        HistorySectionTitle = textService.Resolve(TextTokens.InspectionDetailHistoryTitle);
        HistorySectionDescription = textService.Resolve(TextTokens.InspectionRecordDescription);
        LastFaultLabel = textService.Resolve(TextTokens.InspectionDetailLastFaultLabel);
        DispatchEntryLabel = textService.Resolve(TextTokens.InspectionDetailDispatchEntryLabel);
        LastConclusionLabel = textService.Resolve(TextTokens.InspectionDetailLastConclusionLabel);

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

        _workspaceByGroupId = CreateFakeWorkspaces();
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
    public string HistorySectionTitle { get; }
    public string HistorySectionDescription { get; }
    public string LastFaultLabel { get; }
    public string DispatchEntryLabel { get; }
    public string LastConclusionLabel { get; }
    public string ExecuteInspectionText { get; }
    public string ViewHistoryText { get; }
    public string OpenDispatchWorkspaceText { get; }
    public string OpenReportsCenterText { get; }

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

        foreach (var fault in RecentFaults)
        {
            fault.IsSelected = fault.PointId == point.Id;
        }

        SelectedPoint = point;
        SelectedPointDetail = CreatePointDetail(point);
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
        }

        var nextPoint = workspace.Points.FirstOrDefault(point => point.Status == InspectionPointStatus.Pending);
        if (nextPoint is not null)
        {
            nextPoint.Status = InspectionPointStatus.Inspecting;
            nextPoint.IsCurrent = true;
            workspace.ExecutionState.CurrentTaskStatus = _textService.Resolve(TextTokens.InspectionTaskStatusRunning);
            workspace.ExecutionState.SimulationNote = _textService.Resolve(TextTokens.InspectionSimulationTriggered);
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
            point.OnlineStatus,
            point.PlaybackStatus,
            point.ImageStatus,
            point.FaultType,
            point.FaultDescription,
            point.IsPreviewAvailable,
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

    private Dictionary<string, GroupWorkspaceState> CreateFakeWorkspaces()
    {
        var reviewMode = _textService.Resolve(TextTokens.InspectionResultModeReview);
        var directDispatchMode = _textService.Resolve(TextTokens.InspectionResultModeDirectDispatch);
        var autoDispatch = _textService.Resolve(TextTokens.InspectionDispatchModeAuto);
        var manualDispatch = _textService.Resolve(TextTokens.InspectionDispatchModeManual);
        var taskIdle = _textService.Resolve(TextTokens.InspectionTaskStatusIdle);

        return new[]
        {
            new GroupWorkspaceState(
                new InspectionGroupSummaryState("g-telecom-river", "沿江慢直播保障一组", "12 项监看范围 · 上墙复核 · 自动派单", true),
                new InspectionStrategySummaryState("08:30", "4 次 / 日", "每 4 小时", reviewMode, autoDispatch),
                CreateExecutionState("2 / 4", taskIdle, "14:30", true),
                CreateRunSummary("沿江慢直播保障一组", "2026-03-12 09:10"),
                "2026-03-12 09:46",
                new ObservableCollection<InspectionPointState>(
                [
                    CreatePoint("p-101", "江滩观景台 1 号位", "沿江运营一中心", "沿江维护班组", 90, 150, InspectionPointStatus.Normal, InspectionPointStatus.Normal, true, true, false, false, "2026-03-11 21:30", true),
                    CreatePoint("p-102", "轮渡码头北口", "沿江运营一中心", "沿江维护班组", 270, 120, InspectionPointStatus.Fault, InspectionPointStatus.Fault, true, false, false, false, "2026-03-12 08:42", true),
                    CreatePoint("p-103", "跨江大桥东塔", "桥梁联防中心", "桥梁值守班", 430, 190, InspectionPointStatus.Inspecting, InspectionPointStatus.Normal, true, true, false, true, "2026-03-10 18:12", false),
                    CreatePoint("p-104", "城市阳台主广场", "文旅联合中心", "文旅夜景保障组", 600, 130, InspectionPointStatus.Pending, InspectionPointStatus.Fault, true, true, true, false, "2026-03-09 20:44", true),
                    CreatePoint("p-105", "滨江步道南段", "沿江运营二中心", "沿江维护班组", 700, 260, InspectionPointStatus.Pending, InspectionPointStatus.Normal, true, true, false, true, "2026-03-08 16:02", false),
                    CreatePoint("p-106", "地铁口联防点", "轨交换乘保障组", "轨道联动班", 220, 280, InspectionPointStatus.Silent, InspectionPointStatus.Silent, true, true, false, true, "--", false),
                    CreatePoint("p-107", "防汛泵站外侧", "防汛保障中心", "防汛应急班", 360, 330, InspectionPointStatus.PausedUntilRecovery, InspectionPointStatus.PausedUntilRecovery, false, false, false, false, "2026-03-12 07:10", true),
                    CreatePoint("p-108", "江心灯塔监看点", "航道监护中心", "航道维护组", 560, 320, InspectionPointStatus.Fault, InspectionPointStatus.Fault, false, false, false, false, "2026-03-12 06:51", true),
                    CreatePoint("p-109", "文化展亭西侧", "文旅联合中心", "文旅夜景保障组", 790, 180, InspectionPointStatus.Normal, InspectionPointStatus.Normal, true, true, false, true, "2026-03-07 11:13", false),
                    CreatePoint("p-110", "亲水平台北端", "沿江运营二中心", "沿江维护班组", 860, 310, InspectionPointStatus.Pending, InspectionPointStatus.Normal, true, true, false, true, "--", false),
                    CreatePoint("p-111", "演艺广场东门", "文旅联合中心", "文旅夜景保障组", 980, 150, InspectionPointStatus.Pending, InspectionPointStatus.Fault, true, false, true, false, "--", true),
                    CreatePoint("p-112", "景观桥步道口", "桥梁联防中心", "桥梁值守班", 1040, 260, InspectionPointStatus.Normal, InspectionPointStatus.Normal, true, true, false, true, "2026-03-06 13:27", false)
                ]),
                new ObservableCollection<RecentFaultSummaryState>(
                [
                    new("p-102", "轮渡码头北口", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), "2026-03-12 08:42"),
                    new("p-108", "江心灯塔监看点", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), "2026-03-12 06:51"),
                    new("p-107", "防汛泵站外侧", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), "2026-03-12 07:10")
                ])),
            new GroupWorkspaceState(
                new InspectionGroupSummaryState("g-city-night", "城区夜景值守二组", "10 项监看范围 · 直接派单 · 人工派单", true),
                new InspectionStrategySummaryState("19:00", "3 次 / 日", "每 3 小时", directDispatchMode, manualDispatch),
                CreateExecutionState("1 / 3", taskIdle, "22:00", true),
                CreateRunSummary("城区夜景值守二组", "2026-03-12 19:05"),
                "2026-03-12 19:37",
                new ObservableCollection<InspectionPointState>(
                [
                    CreatePoint("p-201", "城市中轴灯光秀主屏", "城区值守中心", "夜景值守班", 140, 140, InspectionPointStatus.Normal, InspectionPointStatus.Normal, true, true, false, true, "2026-03-05 09:20", false),
                    CreatePoint("p-202", "会展中心南入口", "城区值守中心", "夜景值守班", 280, 210, InspectionPointStatus.Inspecting, InspectionPointStatus.Normal, true, true, false, true, "--", false),
                    CreatePoint("p-203", "商业街 3 号塔", "商圈联防中心", "商圈维护组", 430, 120, InspectionPointStatus.Pending, InspectionPointStatus.Fault, true, true, true, false, "--", true),
                    CreatePoint("p-204", "游客集散广场", "文旅联合中心", "文旅夜景保障组", 580, 220, InspectionPointStatus.Fault, InspectionPointStatus.Fault, true, false, false, false, "2026-03-12 18:36", true),
                    CreatePoint("p-205", "滨湖步道转角", "湖区保障组", "湖区值守班", 720, 160, InspectionPointStatus.Pending, InspectionPointStatus.Normal, true, true, false, true, "--", false),
                    CreatePoint("p-206", "交通枢纽东平台", "城区值守中心", "交通联动班", 860, 260, InspectionPointStatus.Pending, InspectionPointStatus.Normal, true, true, false, true, "--", false),
                    CreatePoint("p-207", "东湖观景塔", "湖区保障组", "湖区值守班", 980, 110, InspectionPointStatus.Silent, InspectionPointStatus.Silent, true, true, false, true, "--", false),
                    CreatePoint("p-208", "主会场外场屏", "商圈联防中心", "商圈维护组", 1030, 280, InspectionPointStatus.PausedUntilRecovery, InspectionPointStatus.PausedUntilRecovery, false, false, false, false, "2026-03-12 17:10", true),
                    CreatePoint("p-209", "东门宣传屏", "城区值守中心", "夜景值守班", 650, 330, InspectionPointStatus.Normal, InspectionPointStatus.Normal, true, true, false, true, "2026-03-03 11:20", false),
                    CreatePoint("p-210", "会展中心北侧广角位", "城区值守中心", "夜景值守班", 380, 330, InspectionPointStatus.Pending, InspectionPointStatus.Fault, true, false, true, false, "--", true)
                ]),
                new ObservableCollection<RecentFaultSummaryState>(
                [
                    new("p-204", "游客集散广场", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), "2026-03-12 18:36"),
                    new("p-208", "主会场外场屏", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), "2026-03-12 17:10")
                ]))
        }.ToDictionary(workspace => workspace.Group.Id, workspace => workspace);
    }

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

    private InspectionPointState CreatePoint(
        string id,
        string name,
        string unitName,
        string currentHandlingUnit,
        double x,
        double y,
        InspectionPointStatus status,
        InspectionPointStatus completionStatus,
        bool isOnline,
        bool isPlayable,
        bool isImageAbnormal,
        bool isPreviewAvailable,
        string lastFaultTime,
        bool entersDispatchPool)
    {
        var faultType = ResolveFaultType(status, completionStatus, isOnline, isPlayable, isImageAbnormal);

        return new InspectionPointState(
            id,
            name,
            unitName,
            currentHandlingUnit,
            x,
            y,
            status,
            completionStatus,
            isOnline ? _textService.Resolve(TextTokens.InspectionOnlineOnline) : _textService.Resolve(TextTokens.InspectionOnlineOffline),
            isPlayable ? _textService.Resolve(TextTokens.InspectionPlaybackPlayable) : _textService.Resolve(TextTokens.InspectionPlaybackFailed),
            isImageAbnormal ? _textService.Resolve(TextTokens.InspectionImageAbnormal) : _textService.Resolve(TextTokens.InspectionImageNormal),
            faultType,
            BuildFaultDescription(status, completionStatus, isOnline, isPlayable, isImageAbnormal),
            lastFaultTime,
            entersDispatchPool ? _textService.Resolve(TextTokens.InspectionDispatchPoolYes) : _textService.Resolve(TextTokens.InspectionDispatchPoolNo),
            ResolveConclusion(status, completionStatus),
            isPreviewAvailable)
        {
            IsCurrent = status == InspectionPointStatus.Inspecting
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
        bool isImageAbnormal)
    {
        var effectiveStatus = status == InspectionPointStatus.Pending ? completionStatus : status;

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

    private sealed class GroupWorkspaceState
    {
        public GroupWorkspaceState(
            InspectionGroupSummaryState group,
            InspectionStrategySummaryState strategySummary,
            InspectionTaskExecutionState executionState,
            InspectionRunSummaryState runSummary,
            string taskFinishedAt,
            ObservableCollection<InspectionPointState> points,
            ObservableCollection<RecentFaultSummaryState> recentFaults)
        {
            Group = group;
            StrategySummary = strategySummary;
            ExecutionState = executionState;
            RunSummary = runSummary;
            TaskFinishedAt = taskFinishedAt;
            Points = points;
            RecentFaults = recentFaults;
        }

        public InspectionGroupSummaryState Group { get; }
        public InspectionStrategySummaryState StrategySummary { get; }
        public InspectionTaskExecutionState ExecutionState { get; }
        public InspectionRunSummaryState RunSummary { get; }
        public string TaskFinishedAt { get; }
        public ObservableCollection<InspectionPointState> Points { get; }
        public ObservableCollection<RecentFaultSummaryState> RecentFaults { get; }
        public InspectionReviewTaskSummaryState? ReviewSummary { get; set; }
        public ObservableCollection<InspectionReviewCardState>? ReviewCards { get; set; }
        public InspectionReviewFilterState? ReviewFilter { get; set; }
    }
}
