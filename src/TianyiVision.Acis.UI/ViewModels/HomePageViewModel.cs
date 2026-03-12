using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Layout;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class HomePageViewModel : PageViewModelBase
{
    private const double OverlayMargin = 24;
    private const double OverlayGrabWidth = 96;
    private const double OverlayGrabHeight = 56;

    private readonly IHomeOverlayLayoutService _layoutService;
    private readonly ITextService _textService;
    private readonly IReadOnlyDictionary<string, OverlayPanelDefinition> _overlayDefinitions;
    private bool _isInitializingOverlayLayout;
    private bool _overlayLayoutInitialized;
    private Size _overlayViewport;
    private HomePointSummaryState? _selectedPointSummary;

    public HomePageViewModel(ITextService textService, IHomeOverlayLayoutService layoutService)
        : base(
            textService.Resolve(TextTokens.HomeTitle),
            textService.Resolve(TextTokens.HomeDescription))
    {
        _textService = textService;
        _layoutService = layoutService;
        _overlayDefinitions = CreateOverlayDefinitions();

        MapStageBadge = textService.Resolve(TextTokens.HomeMapStageBadge);
        MapStageTitle = textService.Resolve(TextTokens.HomeMapStageTitle);
        MapStageDescription = textService.Resolve(TextTokens.HomeMapStageDescription);
        MapStageHint = textService.Resolve(TextTokens.HomeMapStageHint);
        TaskPanelTitle = textService.Resolve(TextTokens.HomeTaskPanelTitle);
        TaskPanelDescription = textService.Resolve(TextTokens.HomeTaskPanelDescription);
        FaultPanelTitle = textService.Resolve(TextTokens.HomeFaultPanelTitle);
        FaultPanelDescription = textService.Resolve(TextTokens.HomeFaultPanelDescription);
        PointPanelTitle = textService.Resolve(TextTokens.HomePointPanelTitle);
        PointPanelDescription = textService.Resolve(TextTokens.HomePointPanelDescription);
        LegendPanelTitle = textService.Resolve(TextTokens.HomeLegendPanelTitle);
        PanelRestoreHint = textService.Resolve(TextTokens.HomePanelRestoreHint);
        ResetOverlayLayoutText = textService.Resolve(TextTokens.HomeResetOverlayLayoutAction);
        MapSelectionHint = textService.Resolve(TextTokens.HomeMapSelectionHint);
        CurrentGroupLabel = textService.Resolve(TextTokens.HomeCurrentGroupLabel);
        ExecutionProgressLabel = textService.Resolve(TextTokens.HomeExecutionProgressLabel);
        PendingReviewLabel = textService.Resolve(TextTokens.HomePendingReviewLabel);
        PendingDispatchLabel = textService.Resolve(TextTokens.HomePendingDispatchLabel);
        RecentFaultTimeLabel = textService.Resolve(TextTokens.HomeRecentFaultTimeLabel);
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

        OverlayPanels =
        [
            new HomeOverlayPanelState("task-panel", TaskPanelTitle, 24, 24),
            new HomeOverlayPanelState("fault-panel", FaultPanelTitle, 24, 430),
            new HomeOverlayPanelState("point-panel", PointPanelTitle, 1080, 54),
            new HomeOverlayPanelState("legend-panel", LegendPanelTitle, 360, 730)
        ];

        TaskPanel = OverlayPanels.First(panel => panel.Id == "task-panel");
        FaultPanel = OverlayPanels.First(panel => panel.Id == "fault-panel");
        PointPanel = OverlayPanels.First(panel => panel.Id == "point-panel");
        LegendPanel = OverlayPanels.First(panel => panel.Id == "legend-panel");
        HiddenPanels = [];

        foreach (var panel in OverlayPanels)
        {
            panel.PropertyChanged += HandleOverlayPanelChanged;
        }

        MapPoints =
        [
            CreatePoint("home-101", "江滩观景台 1 号位", "沿江运营一中心", HomeMapPointKind.Normal, 140, 210, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeNone), "当前画面和在线状态稳定，适合首页作为正常态示例。", "--", false),
            CreatePoint("home-102", "轮渡码头北口", "沿江运营一中心", HomeMapPointKind.Fault, 320, 150, textService.Resolve(TextTokens.InspectionStatusFault), textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), "当前以播放失败为主，后续应在业务页承接协议切换与重试过程。", "2026-03-12 08:42", true),
            CreatePoint("home-103", "跨江大桥东塔", "桥梁联防中心", HomeMapPointKind.Inspecting, 530, 250, textService.Resolve(TextTokens.InspectionStatusInspecting), textService.Resolve(TextTokens.InspectionFaultTypeNone), "当前点位处于巡检中，用于展示首页态势联动骨架。", "--", false),
            CreatePoint("home-104", "城市阳台主广场", "文旅联合中心", HomeMapPointKind.Key, 790, 130, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), "当前属于重点区域点位，首页优先保留其可视化态势。", "2026-03-12 07:54", true),
            CreatePoint("home-105", "防汛泵站外侧", "防汛保障中心", HomeMapPointKind.Fault, 450, 390, textService.Resolve(TextTokens.InspectionStatusPausedUntilRecovery), textService.Resolve(TextTokens.InspectionFaultTypeOffline), "点位当前离线且处于恢复前暂停巡检状态。", "2026-03-12 07:10", true),
            CreatePoint("home-106", "江心灯塔监看点", "航道监护中心", HomeMapPointKind.Fault, 700, 420, textService.Resolve(TextTokens.InspectionStatusFault), textService.Resolve(TextTokens.InspectionFaultTypeOffline), "该点位在首页维持高亮告警，用于强调地图主舞台的故障态势。", "2026-03-12 06:51", true),
            CreatePoint("home-107", "文化展亭西侧", "文旅联合中心", HomeMapPointKind.Key, 980, 230, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeNone), "重点区域点位，当前状态正常。", "--", false),
            CreatePoint("home-108", "景观桥步道口", "桥梁联防中心", HomeMapPointKind.Normal, 1120, 360, textService.Resolve(TextTokens.InspectionStatusNormal), textService.Resolve(TextTokens.InspectionFaultTypeNone), "桥梁点位当前可稳定显示，用于首页整体态势铺陈。", "--", false)
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
        HideOverlayPanelCommand = new RelayCommand(parameter =>
        {
            if (parameter is HomeOverlayPanelState panel)
            {
                HideOverlayPanel(panel);
            }
        });
        ShowOverlayPanelCommand = new RelayCommand(parameter =>
        {
            if (parameter is HomeOverlayPanelState panel)
            {
                RestoreOverlayPanel(panel);
            }
        });
        ResetOverlayLayoutCommand = new RelayCommand(_ => ResetOverlayLayout());

        RefreshHiddenPanels();
        SelectPoint(MapPoints.First(point => point.Id == "home-102"));
    }

    public string MapStageBadge { get; }
    public string MapStageTitle { get; }
    public string MapStageDescription { get; }
    public string MapStageHint { get; }
    public string TaskPanelTitle { get; }
    public string TaskPanelDescription { get; }
    public string FaultPanelTitle { get; }
    public string FaultPanelDescription { get; }
    public string PointPanelTitle { get; }
    public string PointPanelDescription { get; }
    public string LegendPanelTitle { get; }
    public string PanelRestoreHint { get; }
    public string ResetOverlayLayoutText { get; }
    public string MapSelectionHint { get; }
    public string CurrentGroupLabel { get; }
    public string ExecutionProgressLabel { get; }
    public string PendingReviewLabel { get; }
    public string PendingDispatchLabel { get; }
    public string RecentFaultTimeLabel { get; }
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

    public ObservableCollection<HomeOverlayPanelState> OverlayPanels { get; }
    public ObservableCollection<HomeOverlayPanelState> HiddenPanels { get; }
    public ObservableCollection<HomeMapPointState> MapPoints { get; }
    public ObservableCollection<HomeRecentFaultState> RecentFaults { get; }
    public HomeOverlayPanelState TaskPanel { get; }
    public HomeOverlayPanelState FaultPanel { get; }
    public HomeOverlayPanelState PointPanel { get; }
    public HomeOverlayPanelState LegendPanel { get; }

    public HomePointSummaryState? SelectedPointSummary
    {
        get => _selectedPointSummary;
        private set => SetProperty(ref _selectedPointSummary, value);
    }

    public ICommand SelectMapPointCommand { get; }
    public ICommand SelectRecentFaultCommand { get; }
    public ICommand HideOverlayPanelCommand { get; }
    public ICommand ShowOverlayPanelCommand { get; }
    public ICommand ResetOverlayLayoutCommand { get; }

    public void InitializeOverlayLayout(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        _overlayViewport = new Size(viewportWidth, viewportHeight);

        if (_overlayLayoutInitialized)
        {
            ClampVisiblePanelsToViewport();
            return;
        }

        _isInitializingOverlayLayout = true;

        try
        {
            var snapshot = _layoutService.Load();
            ApplyOverlayLayout(snapshot);

            RefreshHiddenPanels();
            _overlayLayoutInitialized = true;
        }
        finally
        {
            _isInitializingOverlayLayout = false;
        }
    }

    public void UpdateOverlayPanelPosition(string panelId, double x, double y)
    {
        var panel = OverlayPanels.FirstOrDefault(item => item.Id == panelId);
        if (panel is null)
        {
            return;
        }

        var position = ClampPosition(panelId, x, y);
        panel.X = position.X;
        panel.Y = position.Y;
        panel.IsUsingDefaultPositionFallback = false;
    }

    public void CommitOverlayLayout()
    {
        if (_overlayViewport.Width > 0 && _overlayViewport.Height > 0)
        {
            ClampVisiblePanelsToViewport();
        }

        foreach (var panel in OverlayPanels)
        {
            panel.HasPersistedLayout = true;
        }

        SaveOverlayLayout();
    }

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

    private void HandleOverlayPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeOverlayPanelState.IsVisible))
        {
            RefreshHiddenPanels();
            SaveOverlayLayout();
        }
    }

    private void RefreshHiddenPanels()
    {
        HiddenPanels.Clear();
        foreach (var panel in OverlayPanels.Where(item => !item.IsVisible))
        {
            HiddenPanels.Add(panel);
        }
    }

    private void HideOverlayPanel(HomeOverlayPanelState panel)
    {
        panel.IsVisible = false;
        panel.HasPersistedLayout = true;
        panel.IsUsingDefaultPositionFallback = false;
    }

    public void RestoreOverlayPanel(HomeOverlayPanelState panel)
    {
        // Keep restore on a single path so button click, position correction, visibility and persistence stay in sync.
        EnsureOverlayPanelPosition(panel);
        panel.IsVisible = true;
        panel.HasPersistedLayout = true;
        RefreshHiddenPanels();
        SaveOverlayLayout();
    }

    private void ResetOverlayLayout()
    {
        _layoutService.Reset();

        foreach (var panel in OverlayPanels)
        {
            panel.HasPersistedLayout = false;
            panel.IsVisible = true;
            ApplyDefaultPosition(panel);
        }

        RefreshHiddenPanels();
    }

    private void ApplyOverlayLayout(HomeOverlayLayoutSnapshot snapshot)
    {
        var layouts = snapshot.Panels.ToDictionary(panel => panel.PanelId, StringComparer.Ordinal);
        var hasAnyVisiblePanel = layouts.Values.Any(panel => panel.IsVisible);
        var useDefaultLayout = layouts.Count == 0 || !hasAnyVisiblePanel;

        foreach (var panel in OverlayPanels)
        {
            if (!_overlayDefinitions.TryGetValue(panel.Id, out var definition))
            {
                continue;
            }

            var defaultPosition = definition.CreateDefaultPosition(_overlayViewport);
            panel.DefaultX = defaultPosition.X;
            panel.DefaultY = defaultPosition.Y;
            panel.IsDragEnabled = true;

            if (!useDefaultLayout && layouts.TryGetValue(panel.Id, out var layout))
            {
                panel.HasPersistedLayout = true;
                panel.IsVisible = layout.IsVisible;
                ApplyPanelPosition(panel, layout.X, layout.Y, useDefaultFallback: false);
                continue;
            }

            panel.HasPersistedLayout = false;
            panel.IsVisible = true;
            ApplyDefaultPosition(panel);
        }

        CorrectOverlappingPanels();
        EnsureAtLeastOneVisiblePanel();
    }

    private void SaveOverlayLayout()
    {
        if (_isInitializingOverlayLayout)
        {
            return;
        }

        var snapshot = new HomeOverlayLayoutSnapshot(
            OverlayPanels
                .Select(panel => new HomeOverlayPanelLayout(panel.Id, panel.X, panel.Y, panel.IsVisible))
                .ToList());
        _layoutService.Save(snapshot);
    }

    private void ApplyDefaultPosition(HomeOverlayPanelState panel)
    {
        var position = ClampPosition(panel.Id, panel.DefaultX, panel.DefaultY);
        panel.X = position.X;
        panel.Y = position.Y;
        panel.IsUsingDefaultPositionFallback = true;
    }

    private void ApplyPanelPosition(HomeOverlayPanelState panel, double x, double y, bool useDefaultFallback)
    {
        if (useDefaultFallback)
        {
            ApplyDefaultPosition(panel);
            return;
        }

        var position = ClampPosition(panel.Id, x, y);
        panel.X = position.X;
        panel.Y = position.Y;
        panel.IsUsingDefaultPositionFallback = false;
    }

    private void EnsureOverlayPanelPosition(HomeOverlayPanelState panel)
    {
        if (!panel.HasPersistedLayout)
        {
            ApplyDefaultPosition(panel);
            return;
        }

        if (!IsPositionWithinViewport(panel.Id, panel.X, panel.Y))
        {
            ApplyDefaultPosition(panel);
            return;
        }

        ApplyPanelPosition(panel, panel.X, panel.Y, useDefaultFallback: false);
    }

    private void ClampVisiblePanelsToViewport()
    {
        foreach (var panel in OverlayPanels.Where(item => item.IsVisible))
        {
            var position = ClampPosition(panel.Id, panel.X, panel.Y);
            panel.X = position.X;
            panel.Y = position.Y;
        }
    }

    private void EnsureAtLeastOneVisiblePanel()
    {
        if (OverlayPanels.Any(panel => panel.IsVisible))
        {
            return;
        }

        foreach (var panel in OverlayPanels)
        {
            panel.IsVisible = true;
            ApplyDefaultPosition(panel);
        }
    }

    private void CorrectOverlappingPanels()
    {
        var visiblePanels = OverlayPanels.Where(panel => panel.IsVisible).ToList();
        for (var index = 0; index < visiblePanels.Count; index++)
        {
            for (var otherIndex = index + 1; otherIndex < visiblePanels.Count; otherIndex++)
            {
                if (!TryGetPanelRect(visiblePanels[index], out var firstRect)
                    || !TryGetPanelRect(visiblePanels[otherIndex], out var secondRect))
                {
                    continue;
                }

                var intersection = Rect.Intersect(firstRect, secondRect);
                if (intersection.IsEmpty)
                {
                    continue;
                }

                var smallerArea = Math.Min(firstRect.Width * firstRect.Height, secondRect.Width * secondRect.Height);
                if (smallerArea <= 0)
                {
                    continue;
                }

                if ((intersection.Width * intersection.Height) / smallerArea >= 0.45)
                {
                    ApplyDefaultPosition(visiblePanels[otherIndex]);
                }
            }
        }
    }

    private Point ClampPosition(string panelId, double x, double y)
    {
        if (!_overlayDefinitions.TryGetValue(panelId, out var definition)
            || _overlayViewport.Width <= 0
            || _overlayViewport.Height <= 0)
        {
            return new Point(Math.Max(0, x), Math.Max(0, y));
        }

        var minX = Math.Min(OverlayMargin, _overlayViewport.Width - OverlayGrabWidth) - definition.Size.Width;
        var maxX = Math.Max(OverlayMargin, _overlayViewport.Width - OverlayGrabWidth);
        var minY = 0d;
        var maxY = Math.Max(OverlayMargin, _overlayViewport.Height - OverlayGrabHeight);

        return new Point(
            Math.Clamp(x, minX + OverlayGrabWidth, maxX),
            Math.Clamp(y, minY, maxY));
    }

    private bool IsPositionWithinViewport(string panelId, double x, double y)
    {
        if (!_overlayDefinitions.TryGetValue(panelId, out var definition)
            || _overlayViewport.Width <= 0
            || _overlayViewport.Height <= 0)
        {
            return false;
        }

        var allowedRect = new Rect(
            -definition.Size.Width + OverlayGrabWidth,
            0,
            _overlayViewport.Width + definition.Size.Width - OverlayGrabWidth,
            _overlayViewport.Height);
        return allowedRect.Contains(new Point(x, y));
    }

    private bool TryGetPanelRect(HomeOverlayPanelState panel, out Rect rect)
    {
        rect = Rect.Empty;
        if (!_overlayDefinitions.TryGetValue(panel.Id, out var definition))
        {
            return false;
        }

        rect = new Rect(new Point(panel.X, panel.Y), definition.Size);
        return true;
    }

    private static IReadOnlyDictionary<string, OverlayPanelDefinition> CreateOverlayDefinitions()
    {
        return new Dictionary<string, OverlayPanelDefinition>(StringComparer.Ordinal)
        {
            ["task-panel"] = new(
                new Size(300, 292),
                viewport => new Point(OverlayMargin, OverlayMargin)),
            ["fault-panel"] = new(
                new Size(320, 286),
                viewport => new Point(OverlayMargin, Math.Max(250, viewport.Height - 330))),
            ["point-panel"] = new(
                new Size(320, 260),
                viewport => new Point(Math.Max(OverlayMargin, viewport.Width - 344), OverlayMargin)),
            ["legend-panel"] = new(
                new Size(520, 120),
                viewport => new Point(
                    Math.Max(OverlayMargin, (viewport.Width - 520) / 2),
                    Math.Max(OverlayMargin, viewport.Height - 152)))
        };
    }

    private sealed record OverlayPanelDefinition(Size Size, Func<Size, Point> CreateDefaultPosition);
}
