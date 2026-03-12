namespace TianyiVision.Acis.UI.States;

public sealed record ResponsibilityOwnershipReportRowState(
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string MaintainerName,
    string SupervisorName,
    string FaultType,
    int FaultCount,
    int RecoveredCount,
    int UnrecoveredCount);
