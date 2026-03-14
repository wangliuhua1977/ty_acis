namespace TianyiVision.Acis.UI.States;

public sealed record InspectionPointDetailState(
    string PointName,
    string UnitName,
    string CurrentHandlingUnit,
    string CurrentStatus,
    string CoordinateStatusText,
    string OnlineStatus,
    string PlaybackStatus,
    string ImageStatus,
    string FaultType,
    string FaultDescription,
    bool IsPreviewAvailable,
    bool IsCoordinatePending,
    string LastFaultTime,
    string DispatchPoolEntry,
    string LastInspectionConclusion);
