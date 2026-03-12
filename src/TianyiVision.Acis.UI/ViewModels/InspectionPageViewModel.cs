using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class InspectionPageViewModel : PageViewModelBase
{
    public InspectionPageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.InspectionTitle),
            textService.Resolve(TextTokens.InspectionDescription))
    {
        TaskPanels =
        [
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.InspectionGroupTitle),
                textService.Resolve(TextTokens.InspectionGroupDescription),
                "设备组合预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.InspectionStrategyTitle),
                textService.Resolve(TextTokens.InspectionStrategyDescription),
                "组级策略预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.InspectionExecutionTitle),
                textService.Resolve(TextTokens.InspectionExecutionDescription),
                "执行链路预留")
        ];

        WorkbenchHighlights =
        [
            new MetricCardState(textService.Resolve(TextTokens.InspectionLegendPending), "--", "等待执行"),
            new MetricCardState(textService.Resolve(TextTokens.InspectionLegendActive), "--", "当前点位高亮"),
            new MetricCardState(textService.Resolve(TextTokens.InspectionLegendFault), "--", "红灯闪烁预留")
        ];

        WorkbenchPanel = new PanelPlaceholderState(
            textService.Resolve(TextTokens.InspectionWorkbenchTitle),
            textService.Resolve(TextTokens.InspectionWorkbenchDescription),
            "中台工作区");

        DetailPanels =
        [
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.InspectionDetailsTitle),
                textService.Resolve(TextTokens.InspectionDetailsDescription),
                "点位详情预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.InspectionPreviewTitle),
                textService.Resolve(TextTokens.InspectionPreviewDescription),
                "视频浮窗预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.InspectionRecordTitle),
                textService.Resolve(TextTokens.InspectionRecordDescription),
                "结果摘要预留")
        ];
    }

    public ObservableCollection<PanelPlaceholderState> TaskPanels { get; }

    public ObservableCollection<MetricCardState> WorkbenchHighlights { get; }

    public PanelPlaceholderState WorkbenchPanel { get; }

    public ObservableCollection<PanelPlaceholderState> DetailPanels { get; }
}
