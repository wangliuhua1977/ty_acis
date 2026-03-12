using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class DispatchPageViewModel : PageViewModelBase
{
    public DispatchPageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.DispatchTitle),
            textService.Resolve(TextTokens.DispatchDescription))
    {
        FilterPanels =
        [
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.DispatchFilterTitle),
                textService.Resolve(TextTokens.DispatchFilterDescription),
                "筛选面板预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.DispatchStatusTitle),
                textService.Resolve(TextTokens.DispatchStatusDescription),
                "快捷筛选预留")
        ];

        ListPanels =
        [
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.DispatchListTitle),
                textService.Resolve(TextTokens.DispatchListDescription),
                "工单主表预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.DispatchMergeTitle),
                textService.Resolve(TextTokens.DispatchMergeDescription),
                "合并规则预留")
        ];

        DetailPanels =
        [
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.DispatchDetailTitle),
                textService.Resolve(TextTokens.DispatchDetailDescription),
                "详情抽屉预留"),
            new PanelPlaceholderState(
                textService.Resolve(TextTokens.DispatchEvidenceTitle),
                textService.Resolve(TextTokens.DispatchEvidenceDescription),
                "证据区预留")
        ];
    }

    public ObservableCollection<PanelPlaceholderState> FilterPanels { get; }

    public ObservableCollection<PanelPlaceholderState> ListPanels { get; }

    public ObservableCollection<PanelPlaceholderState> DetailPanels { get; }
}
