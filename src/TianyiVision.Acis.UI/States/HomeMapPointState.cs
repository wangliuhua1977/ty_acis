using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class HomeMapPointState : ViewModelBase
{
    private bool _isSelected;

    public HomeMapPointState(
        string id,
        string name,
        string unitName,
        HomeMapPointKind kind,
        double x,
        double y,
        string statusText,
        string faultType,
        string summary,
        string actionHint,
        string latestFaultTime,
        bool isInRecentFaultList)
    {
        Id = id;
        Name = name;
        UnitName = unitName;
        Kind = kind;
        X = x;
        Y = y;
        StatusText = statusText;
        FaultType = faultType;
        Summary = summary;
        ActionHint = actionHint;
        LatestFaultTime = latestFaultTime;
        IsInRecentFaultList = isInRecentFaultList;
    }

    public string Id { get; }

    public string Name { get; }

    public string UnitName { get; }

    public HomeMapPointKind Kind { get; }

    public double X { get; }

    public double Y { get; }

    public string StatusText { get; }

    public string FaultType { get; }

    public string Summary { get; }

    public string ActionHint { get; }

    public string LatestFaultTime { get; }

    public bool IsInRecentFaultList { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
