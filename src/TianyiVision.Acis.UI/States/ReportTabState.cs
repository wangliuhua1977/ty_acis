using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public enum ReportViewKind
{
    InspectionExecution,
    FaultStatistics,
    DispatchDisposal,
    ResponsibilityOwnership,
    OutstandingFaults
}

public sealed class ReportTabState : ViewModelBase
{
    private bool _isSelected;

    public ReportTabState(ReportViewKind viewKind, string label)
    {
        ViewKind = viewKind;
        Label = label;
    }

    public ReportViewKind ViewKind { get; }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
