using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.Services.Devices;

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
        string businessSummaryCoordinateStatus,
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
        BusinessSummaryCoordinateStatus = businessSummaryCoordinateStatus;
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

    public string BusinessSummaryCoordinateStatus { get; }

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
