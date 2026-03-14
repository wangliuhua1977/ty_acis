using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class MapPointState : ViewModelBase
{
    private MapPointVisualKind _visualKind;
    private string _statusText;
    private string _faultType;
    private string _summary;
    private string _latestFaultTime;
    private bool _isSelected;
    private bool _isCurrent;

    public MapPointState(
        string pointId,
        string deviceCode,
        string pointName,
        string unitName,
        string currentHandlingUnit,
        double longitude,
        double latitude,
        bool canRenderOnMap,
        string coordinateStatusText,
        double x,
        double y,
        MapPointVisualKind visualKind,
        string statusText,
        string faultType,
        string summary,
        string latestFaultTime,
        bool isPreviewAvailable)
    {
        PointId = pointId;
        DeviceCode = deviceCode;
        PointName = pointName;
        UnitName = unitName;
        CurrentHandlingUnit = currentHandlingUnit;
        Longitude = longitude;
        Latitude = latitude;
        CanRenderOnMap = canRenderOnMap;
        CoordinateStatusText = coordinateStatusText;
        X = x;
        Y = y;
        _visualKind = visualKind;
        _statusText = statusText;
        _faultType = faultType;
        _summary = summary;
        _latestFaultTime = latestFaultTime;
        IsPreviewAvailable = isPreviewAvailable;
    }

    public string PointId { get; }

    public string DeviceCode { get; }

    public string PointName { get; }

    public string UnitName { get; }

    public string CurrentHandlingUnit { get; }

    public double Longitude { get; }

    public double Latitude { get; }

    public bool CanRenderOnMap { get; }

    public string CoordinateStatusText { get; }

    public double X { get; }

    public double Y { get; }

    public bool IsPreviewAvailable { get; }

    public MapPointVisualKind VisualKind
    {
        get => _visualKind;
        set => SetProperty(ref _visualKind, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string FaultType
    {
        get => _faultType;
        set => SetProperty(ref _faultType, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string LatestFaultTime
    {
        get => _latestFaultTime;
        set => SetProperty(ref _latestFaultTime, value);
    }

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
