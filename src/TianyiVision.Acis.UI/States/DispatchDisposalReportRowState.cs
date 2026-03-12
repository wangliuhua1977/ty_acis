namespace TianyiVision.Acis.UI.States;

public sealed record DispatchDisposalReportRowState(
    DateOnly ReportDate,
    string ReportDateText,
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string FaultType,
    int PendingDispatchCount,
    int DispatchedCount,
    int RecoveredCount,
    int UnrecoveredCount,
    int AutomaticDispatchCount,
    int ManualDispatchCount);
