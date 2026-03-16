using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionPointState : ViewModelBase
{
    private InspectionPointStatus _status;
    private bool _isSelected;
    private bool _isCurrent;

    public InspectionPointState(
        string id,
        string deviceCode,
        string name,
        string unitName,
        string currentHandlingUnit,
        double? mapLongitude,
        double? mapLatitude,
        double? registeredLongitude,
        double? registeredLatitude,
        CoordinateSystemKind registeredCoordinateSystem,
        CoordinateSystemKind mapCoordinateSystem,
        bool canRenderOnMap,
        string coordinateStatusText,
        string? rawLongitude,
        string? rawLatitude,
        PointCoordinateStatus coordinateStatus,
        string mapSource,
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
        bool isPreviewAvailable,
        PointBusinessSummaryState businessSummary)
    {
        Id = id;
        DeviceCode = deviceCode;
        Name = name;
        UnitName = unitName;
        CurrentHandlingUnit = currentHandlingUnit;
        MapLongitude = mapLongitude;
        MapLatitude = mapLatitude;
        RegisteredLongitude = registeredLongitude;
        RegisteredLatitude = registeredLatitude;
        RegisteredCoordinateSystem = registeredCoordinateSystem;
        MapCoordinateSystem = mapCoordinateSystem;
        CanRenderOnMap = canRenderOnMap;
        CoordinateStatusText = coordinateStatusText;
        RawLongitude = rawLongitude;
        RawLatitude = rawLatitude;
        CoordinateStatus = coordinateStatus;
        MapSource = mapSource;
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
        BusinessSummary = businessSummary;
    }

    public string Id { get; }

    public string DeviceCode { get; }

    public string Name { get; }

    public string UnitName { get; }

    public string CurrentHandlingUnit { get; }

    public double Longitude => MapLongitude ?? RegisteredLongitude ?? 0d;

    public double Latitude => MapLatitude ?? RegisteredLatitude ?? 0d;

    public double? MapLongitude { get; }

    public double? MapLatitude { get; }

    public double? RegisteredLongitude { get; }

    public double? RegisteredLatitude { get; }

    public CoordinateSystemKind RegisteredCoordinateSystem { get; }

    public CoordinateSystemKind MapCoordinateSystem { get; }

    public bool CanRenderOnMap { get; }

    public string CoordinateStatusText { get; }

    public string? RawLongitude { get; }

    public string? RawLatitude { get; }

    public PointCoordinateStatus CoordinateStatus { get; }

    public string MapSource { get; }

    public double X { get; }

    public double Y { get; }

    public InspectionPointStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(MapColorCategory));
            }
        }
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

    public PointBusinessSummaryState BusinessSummary { get; }

    public MapPointColorCategory MapColorCategory
        => ResolveMapColorCategory(Status, CompletionStatus);

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

    public static MapPointColorCategory ResolveMapColorCategory(
        InspectionPointStatus status,
        InspectionPointStatus completionStatus)
    {
        _ = completionStatus;

        return status switch
        {
            InspectionPointStatus.Fault or InspectionPointStatus.PausedUntilRecovery => MapPointColorCategory.Fault,
            InspectionPointStatus.Pending or InspectionPointStatus.Inspecting => MapPointColorCategory.Warning,
            InspectionPointStatus.Silent => MapPointColorCategory.Neutral,
            _ => MapPointColorCategory.Online
        };
    }
}
