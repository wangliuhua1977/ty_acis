namespace TianyiVision.Acis.UI.States;

public sealed record HomePointSummaryState(
    string PointName,
    string UnitName,
    string StatusText,
    string FaultType,
    string Summary,
    string ActionHint);
