namespace TianyiVision.Acis.UI.States;

public sealed record OutstandingFaultReportRowState(
    DateOnly ReportDate,
    string InspectionGroupName,
    string PointName,
    string CurrentHandlingUnit,
    string MaintainerName,
    string SupervisorName,
    string FaultType,
    string FirstFaultTime,
    string LatestFaultTime,
    string CurrentStatus);
