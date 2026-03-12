namespace TianyiVision.Acis.UI.States;

public sealed record InspectionStrategySummaryState(
    string FirstRunTime,
    string DailyExecutionCount,
    string Interval,
    string ResultMode,
    string DispatchMode);
