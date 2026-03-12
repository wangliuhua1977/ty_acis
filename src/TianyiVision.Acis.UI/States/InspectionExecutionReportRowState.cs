namespace TianyiVision.Acis.UI.States;

public sealed record InspectionExecutionReportRowState(
    DateOnly ReportDate,
    string ReportDateText,
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string FaultType,
    int DailyTaskRuns,
    int TotalPoints,
    int NormalPoints,
    int FaultPoints,
    string CompletionRateText);
