using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class RecentFaultSummaryState : ViewModelBase
{
    private bool _isSelected;

    public RecentFaultSummaryState(string pointId, string pointName, string faultType, string latestFaultTime)
    {
        PointId = pointId;
        PointName = pointName;
        FaultType = faultType;
        LatestFaultTime = latestFaultTime;
    }

    public string PointId { get; }

    public string PointName { get; }

    public string FaultType { get; }

    public string LatestFaultTime { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
