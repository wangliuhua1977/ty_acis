using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionPointState : ViewModelBase
{
    private InspectionPointStatus _status;
    private bool _isSelected;
    private bool _isCurrent;

    public InspectionPointState(
        string id,
        string name,
        string unitName,
        string currentHandlingUnit,
        double x,
        double y,
        InspectionPointStatus status,
        InspectionPointStatus completionStatus,
        string onlineStatus,
        string playbackStatus,
        string imageStatus,
        string faultType,
        string faultDescription,
        string lastFaultTime,
        string dispatchPoolEntry,
        string lastInspectionConclusion,
        bool isPreviewAvailable)
    {
        Id = id;
        Name = name;
        UnitName = unitName;
        CurrentHandlingUnit = currentHandlingUnit;
        X = x;
        Y = y;
        _status = status;
        CompletionStatus = completionStatus;
        OnlineStatus = onlineStatus;
        PlaybackStatus = playbackStatus;
        ImageStatus = imageStatus;
        FaultType = faultType;
        FaultDescription = faultDescription;
        LastFaultTime = lastFaultTime;
        DispatchPoolEntry = dispatchPoolEntry;
        LastInspectionConclusion = lastInspectionConclusion;
        IsPreviewAvailable = isPreviewAvailable;
    }

    public string Id { get; }

    public string Name { get; }

    public string UnitName { get; }

    public string CurrentHandlingUnit { get; }

    public double X { get; }

    public double Y { get; }

    public InspectionPointStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public InspectionPointStatus CompletionStatus { get; }

    public string OnlineStatus { get; }

    public string PlaybackStatus { get; }

    public string ImageStatus { get; }

    public string FaultType { get; }

    public string FaultDescription { get; }

    public string LastFaultTime { get; }

    public string DispatchPoolEntry { get; }

    public string LastInspectionConclusion { get; }

    public bool IsPreviewAvailable { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }
}
