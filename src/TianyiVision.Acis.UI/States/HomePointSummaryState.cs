namespace TianyiVision.Acis.UI.States;

public sealed record HomePointSummaryState(
    string PointName,
    string UnitName,
    string StatusText,
    string CoordinateStatusText,
    string FaultType,
    string Summary,
    string ActionHint,
    bool IsCoordinatePending);
