using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class ReportsPageViewModel : PageViewModelBase
{
    public ReportsPageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.ReportsTitle),
            textService.Resolve(TextTokens.ReportsDescription))
    {
        Metrics =
        [
            new MetricCardState(textService.Resolve(TextTokens.ReportsMetricInspection), "--", "巡检执行预留"),
            new MetricCardState(textService.Resolve(TextTokens.ReportsMetricFault), "--", "故障结构预留"),
            new MetricCardState(textService.Resolve(TextTokens.ReportsMetricDispatch), "--", "派单闭环预留"),
            new MetricCardState(textService.Resolve(TextTokens.ReportsMetricOutstanding), "--", "重点清单预留")
        ];

        FilterPanel = new PanelPlaceholderState(
            textService.Resolve(TextTokens.ReportsFilterTitle),
            textService.Resolve(TextTokens.ReportsFilterDescription),
            "查询条件预留");

        ChartPanels =
        [
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.ReportsChartTrendTitle),
                textService.Resolve(TextTokens.ReportsChartTrendDescription),
                "趋势图预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.ReportsChartTypeTitle),
                textService.Resolve(TextTokens.ReportsChartTypeDescription),
                "占比图预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.ReportsChartRecoveryTitle),
                textService.Resolve(TextTokens.ReportsChartRecoveryDescription),
                "恢复图预留")
        ];

        TablePanel = new PanelPlaceholderState(
            textService.Resolve(TextTokens.ReportsTableTitle),
            textService.Resolve(TextTokens.ReportsTableDescription),
            "明细表预留");
    }

    public ObservableCollection<MetricCardState> Metrics { get; }

    public PanelPlaceholderState FilterPanel { get; }

    public ObservableCollection<PanelPlaceholderState> ChartPanels { get; }

    public PanelPlaceholderState TablePanel { get; }
}
