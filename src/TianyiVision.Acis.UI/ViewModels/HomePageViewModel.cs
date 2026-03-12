using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class HomePageViewModel : PageViewModelBase
{
    public HomePageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.HomeTitle),
            textService.Resolve(TextTokens.HomeDescription))
    {
        Metrics =
        [
            new MetricCardState(textService.Resolve(TextTokens.HomeMetricTasks), "--", "工程壳层已接入统一导航"),
            new MetricCardState(textService.Resolve(TextTokens.HomeMetricPoints), "--", "后续绑定设备池与点位聚合"),
            new MetricCardState(textService.Resolve(TextTokens.HomeMetricFaults), "--", "后续承接巡检输出与派单池"),
            new MetricCardState(textService.Resolve(TextTokens.HomeMetricRecovered), "--", "后续承接自动或人工恢复确认")
        ];

        TrendPanel = new PanelPlaceholderState(
            textService.Resolve(TextTokens.HomeTrendTitle),
            textService.Resolve(TextTokens.HomeTrendDescription),
            "趋势图预留");
        MapSummaryPanel = new PanelPlaceholderState(
            textService.Resolve(TextTokens.HomeMapSummaryTitle),
            textService.Resolve(TextTokens.HomeMapSummaryDescription),
            "地图摘要预留");
        FocusListPanel = new PanelPlaceholderState(
            textService.Resolve(TextTokens.HomeFocusListTitle),
            textService.Resolve(TextTokens.HomeFocusListDescription),
            "重点清单预留");
    }

    public ObservableCollection<MetricCardState> Metrics { get; }

    public PanelPlaceholderState TrendPanel { get; }

    public PanelPlaceholderState MapSummaryPanel { get; }

    public PanelPlaceholderState FocusListPanel { get; }
}
