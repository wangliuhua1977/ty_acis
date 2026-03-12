namespace TianyiVision.Acis.UI.States;

public sealed record FaultStatisticsReportRowState(
    DateOnly ReportDate,
    string ReportDateText,
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string FaultType,
    int FaultTotal,
    int OfflineFaults,
    int PlaybackFailedFaults,
    int ImageAbnormalFaults,
    int NewFaults,
    int RepeatedFaults);
