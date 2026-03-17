using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionPointState : ViewModelBase
{
    private InspectionPointStatus _status;
    private string _statusText;
    private string _onlineStatus;
    private string _playbackStatus;
    private string _imageStatus;
    private string _faultType;
    private string _faultDescription;
    private string _lastFaultTime;
    private string _dispatchPoolEntry;
    private string _lastInspectionConclusion;
    private bool _isPreviewAvailable;
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
        string statusText,
        InspectionPointStatus completionStatus,
        string onlineStatus,
        string playbackStatus,
        string imageStatus,
        string faultType,
        string faultDescription,
        string lastFaultTime,
        string dispatchPoolEntry,
        string lastInspectionConclusion,
        bool isInDefaultScope,
        string scopeDecisionSummary,
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
        _statusText = statusText;
        CompletionStatus = completionStatus;
        _onlineStatus = onlineStatus;
        _playbackStatus = playbackStatus;
        _imageStatus = imageStatus;
        _faultType = faultType;
        _faultDescription = faultDescription;
        _lastFaultTime = lastFaultTime;
        _dispatchPoolEntry = dispatchPoolEntry;
        _lastInspectionConclusion = lastInspectionConclusion;
        IsInDefaultScope = isInDefaultScope;
        ScopeDecisionSummary = scopeDecisionSummary;
        _isPreviewAvailable = isPreviewAvailable;
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

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public InspectionPointStatus CompletionStatus { get; }

    public string OnlineStatus
    {
        get => _onlineStatus;
        set => SetProperty(ref _onlineStatus, value);
    }

    public string PlaybackStatus
    {
        get => _playbackStatus;
        set => SetProperty(ref _playbackStatus, value);
    }

    public string ImageStatus
    {
        get => _imageStatus;
        set => SetProperty(ref _imageStatus, value);
    }

    public string FaultType
    {
        get => _faultType;
        set => SetProperty(ref _faultType, value);
    }

    public string FaultDescription
    {
        get => _faultDescription;
        set => SetProperty(ref _faultDescription, value);
    }

    public string LastFaultTime
    {
        get => _lastFaultTime;
        set => SetProperty(ref _lastFaultTime, value);
    }

    public string DispatchPoolEntry
    {
        get => _dispatchPoolEntry;
        set => SetProperty(ref _dispatchPoolEntry, value);
    }

    public string LastInspectionConclusion
    {
        get => _lastInspectionConclusion;
        set => SetProperty(ref _lastInspectionConclusion, value);
    }

    public bool IsInDefaultScope { get; }

    public string ScopeDecisionSummary { get; }

    public bool IsPreviewAvailable
    {
        get => _isPreviewAvailable;
        set => SetProperty(ref _isPreviewAvailable, value);
    }

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

    public InspectionPointState CreateScopeSnapshot(bool isInScope, string scopeDecisionSummary)
    {
        return new InspectionPointState(
            Id,
            DeviceCode,
            Name,
            UnitName,
            CurrentHandlingUnit,
            MapLongitude,
            MapLatitude,
            RegisteredLongitude,
            RegisteredLatitude,
            RegisteredCoordinateSystem,
            MapCoordinateSystem,
            CanRenderOnMap,
            CoordinateStatusText,
            RawLongitude,
            RawLatitude,
            CoordinateStatus,
            MapSource,
            X,
            Y,
            Status,
            StatusText,
            CompletionStatus,
            OnlineStatus,
            PlaybackStatus,
            ImageStatus,
            FaultType,
            FaultDescription,
            LastFaultTime,
            DispatchPoolEntry,
            LastInspectionConclusion,
            isInScope,
            scopeDecisionSummary,
            IsPreviewAvailable,
            BusinessSummary)
        {
            IsCurrent = IsCurrent,
            IsSelected = IsSelected
        };
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
