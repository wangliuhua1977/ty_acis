using System.Collections.ObjectModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class HomePageViewModel : PageViewModelBase
{
    private readonly ITextService _textService;
    private readonly RelayCommand _selectStatusMetricCommand;
    private HomePointSummaryState? _selectedPointSummary;
    private string _statusFeedback = string.Empty;

    public HomePageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.HomeTitle),
            textService.Resolve(TextTokens.HomeDescription))
    {
        _textService = textService;

        StatusBarDescription = textService.Resolve(TextTokens.HomeStatusBarDescription);
        MapStageBadge = textService.Resolve(TextTokens.HomeMapStageBadge);
        MapStageTitle = textService.Resolve(TextTokens.HomeMapStageTitle);
        MapStageDescription = textService.Resolve(TextTokens.HomeMapStageDescription);
        MapStageHint = textService.Resolve(TextTokens.HomeMapStageHint);
        LeftSummaryTitle = textService.Resolve(TextTokens.HomeLeftSummaryTitle);
        LeftSummaryDescription = textService.Resolve(TextTokens.HomeLeftSummaryDescription);
        CurrentGroupLabel = textService.Resolve(TextTokens.HomeCurrentGroupLabel);
        ExecutionProgressLabel = textService.Resolve(TextTokens.HomeExecutionProgressLabel);
        PendingReviewLabel = textService.Resolve(TextTokens.HomePendingReviewLabel);
        PendingDispatchLabel = textService.Resolve(TextTokens.HomePendingDispatchLabel);
        RecentFaultsTitle = textService.Resolve(TextTokens.HomeRecentFaultsTitle);
        RecentFaultsDescription = textService.Resolve(TextTokens.HomeRecentFaultsDescription);
        RecentFaultTimeLabel = textService.Resolve(TextTokens.HomeRecentFaultTimeLabel);
        RightSummaryTitle = textService.Resolve(TextTokens.HomeRightSummaryTitle);
        RightSummaryDescription = textService.Resolve(TextTokens.HomeRightSummaryDescription);
        SelectedPointStatusLabel = textService.Resolve(TextTokens.HomeSelectedPointStatusLabel);
        SelectedPointFaultTypeLabel = textService.Resolve(TextTokens.HomeSelectedPointFaultTypeLabel);
        SelectedPointSummaryLabel = textService.Resolve(TextTokens.HomeSelectedPointSummaryLabel);
        SelectedPointActionLabel = textService.Resolve(TextTokens.HomeSelectedPointActionLabel);
        LegendFaultText = textService.Resolve(TextTokens.HomeMapLegendFault);
        LegendNormalText = textService.Resolve(TextTokens.HomeMapLegendNormal);
        LegendKeyText = textService.Resolve(TextTokens.HomeMapLegendKey);
        LegendInspectingText = textService.Resolve(TextTokens.HomeMapLegendInspecting);
        CurrentGroupSummary = "沿江慢直播保障一组";
        ExecutionProgress = "8 / 12";
        PendingReviewSummary = "2 个待复核任务，优先关注沿江保障组与夜景值守组。";
        PendingDispatchSummary = "5 条待派单故障，当前以播放失败和离线类为主。";
        StatusFeedback = textService.Resolve(TextTokens.HomeStatusFeedbackIdle);

        _selectStatusMetricCommand = new RelayCommand(parameter =>
        {
            if (parameter is HomeStatusMetricState metric)
            {
                SelectStatusMetric(metric);
            }
        });
        SelectStatusMetricCommand = _selectStatusMetricCommand;

        StatusMetrics =
        [
            new HomeStatusMetricState(textService.Resolve(TextTokens.HomeMetricTasks), "18", "点击后预留进入 AI 智能巡检页"),
            new HomeStatusMetricState(textService.Resolve(TextTokens.HomeMetricFaults), "7", "点击后预留进入故障派单处理页"),
            new HomeStatusMetricState(textService.Resolve(TextTokens.HomeMetricOutstanding), "4", "点击后预留聚焦未恢复故障"),
            new HomeStatusMetricState(textService.Resolve(TextTokens.HomeMetricPendingReview), "2", "点击后预留进入复核墙"),
            new HomeStatusMetricState(textService.Resolve(TextTokens.HomeMetricPendingDispatch), "5", "点击后预留进入待派单列表")
        ];

        MapPoints =
        [
            CreatePoint("home-101", "江滩观景台 1 号位", "沿江运营一中心", HomeMapPointKind.Normal, 110, 190, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeNone), "当前画面和在线状态稳定，适合首页作为正常态示例。", "--", false),
            CreatePoint("home-102", "轮渡码头北口", "沿江运营一中心", HomeMapPointKind.Fault, 260, 130, textService.Resolve(TextTokens.InspectionStatusFault), textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), "当前以播放失败为主，后续应在业务页承接协议切换与重试过程。", "2026-03-12 08:42", true),
            CreatePoint("home-103", "跨江大桥东塔", "桥梁联防中心", HomeMapPointKind.Inspecting, 430, 220, textService.Resolve(TextTokens.InspectionStatusInspecting), textService.Resolve(TextTokens.InspectionFaultTypeNone), "当前点位处于巡检中，用于展示首页态势联动骨架。", "--", false),
            CreatePoint("home-104", "城市阳台主广场", "文旅联合中心", HomeMapPointKind.Key, 650, 120, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), "当前属于重点区域点位，首页优先保留其可视化态势。", "2026-03-12 07:54", true),
            CreatePoint("home-105", "防汛泵站外侧", "防汛保障中心", HomeMapPointKind.Fault, 340, 320, textService.Resolve(TextTokens.InspectionStatusPausedUntilRecovery), textService.Resolve(TextTokens.InspectionFaultTypeOffline), "点位当前离线且处于恢复前暂停巡检状态。", "2026-03-12 07:10", true),
            CreatePoint("home-106", "江心灯塔监看点", "航道监护中心", HomeMapPointKind.Fault, 560, 350, textService.Resolve(TextTokens.InspectionStatusFault), textService.Resolve(TextTokens.InspectionFaultTypeOffline), "该点位在首页维持高亮告警，用于强调地图主舞台的故障态势。", "2026-03-12 06:51", true),
            CreatePoint("home-107", "文化展亭西侧", "文旅联合中心", HomeMapPointKind.Key, 820, 200, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeNone), "重点区域点位，当前状态正常。", "--", false),
            CreatePoint("home-108", "景观桥步道口", "桥梁联防中心", HomeMapPointKind.Normal, 980, 300, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeNone), "桥梁点位当前可稳定显示，用于首页整体态势铺陈。", "--", false)
        ];

        RecentFaults =
        [
            new HomeRecentFaultState("home-102", "轮渡码头北口", textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), "2026-03-12 08:42"),
            new HomeRecentFaultState("home-105", "防汛泵站外侧", textService.Resolve(TextTokens.InspectionFaultTypeOffline), "2026-03-12 07:10"),
            new HomeRecentFaultState("home-106", "江心灯塔监看点", textService.Resolve(TextTokens.InspectionFaultTypeOffline), "2026-03-12 06:51"),
            new HomeRecentFaultState("home-104", "城市阳台主广场", textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), "2026-03-12 07:54")
        ];

        SelectMapPointCommand = new RelayCommand(parameter =>
        {
            if (parameter is HomeMapPointState point)
            {
                SelectPoint(point);
            }
        });
        SelectRecentFaultCommand = new RelayCommand(parameter =>
        {
            if (parameter is HomeRecentFaultState fault)
            {
                var point = MapPoints.FirstOrDefault(item => item.Id == fault.PointId);
                if (point is not null)
                {
                    SelectPoint(point);
                }
            }
        });

        SelectStatusMetric(StatusMetrics.First());
        SelectPoint(MapPoints.First(point => point.Id == "home-102"));
    }

    public string StatusBarDescription { get; }
    public string MapStageBadge { get; }
    public string MapStageTitle { get; }
    public string MapStageDescription { get; }
    public string MapStageHint { get; }
    public string LeftSummaryTitle { get; }
    public string LeftSummaryDescription { get; }
    public string CurrentGroupLabel { get; }
    public string ExecutionProgressLabel { get; }
    public string PendingReviewLabel { get; }
    public string PendingDispatchLabel { get; }
    public string RecentFaultsTitle { get; }
    public string RecentFaultsDescription { get; }
    public string RecentFaultTimeLabel { get; }
    public string RightSummaryTitle { get; }
    public string RightSummaryDescription { get; }
    public string SelectedPointStatusLabel { get; }
    public string SelectedPointFaultTypeLabel { get; }
    public string SelectedPointSummaryLabel { get; }
    public string SelectedPointActionLabel { get; }
    public string LegendFaultText { get; }
    public string LegendNormalText { get; }
    public string LegendKeyText { get; }
    public string LegendInspectingText { get; }
    public string CurrentGroupSummary { get; }
    public string ExecutionProgress { get; }
    public string PendingReviewSummary { get; }
    public string PendingDispatchSummary { get; }

    public ObservableCollection<HomeStatusMetricState> StatusMetrics { get; }
    public ObservableCollection<HomeMapPointState> MapPoints { get; }
    public ObservableCollection<HomeRecentFaultState> RecentFaults { get; }

    public HomePointSummaryState? SelectedPointSummary
    {
        get => _selectedPointSummary;
        private set => SetProperty(ref _selectedPointSummary, value);
    }

    public string StatusFeedback
    {
        get => _statusFeedback;
        private set => SetProperty(ref _statusFeedback, value);
    }

    public ICommand SelectStatusMetricCommand { get; }
    public ICommand SelectMapPointCommand { get; }
    public ICommand SelectRecentFaultCommand { get; }

    private HomeMapPointState CreatePoint(
        string id,
        string name,
        string unitName,
        HomeMapPointKind kind,
        double x,
        double y,
        string statusText,
        string faultType,
        string summary,
        string latestFaultTime,
        bool isInRecentFaultList)
    {
        return new HomeMapPointState(
            id,
            name,
            unitName,
            kind,
            x,
            y,
            statusText,
            faultType,
            summary,
            _textService.Resolve(TextTokens.HomeSelectedPointActionHint),
            latestFaultTime,
            isInRecentFaultList);
    }

    private void SelectStatusMetric(HomeStatusMetricState metric)
    {
        foreach (var item in StatusMetrics)
        {
            item.IsSelected = item == metric;
        }

        StatusFeedback = string.Format(
            _textService.Resolve(TextTokens.HomeStatusFeedbackPattern),
            metric.Title);
    }

    private void SelectPoint(HomeMapPointState point)
    {
        foreach (var item in MapPoints)
        {
            item.IsSelected = item.Id == point.Id;
        }

        foreach (var fault in RecentFaults)
        {
            fault.IsSelected = fault.PointId == point.Id;
        }

        SelectedPointSummary = new HomePointSummaryState(
            point.Name,
            point.UnitName,
            point.StatusText,
            point.FaultType,
            point.Summary,
            point.ActionHint);
    }
}
