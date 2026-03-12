using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Reports;

public sealed record InspectionExecutionReportModel(
    DateOnly ReportDate,
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string FaultType,
    int DailyTaskRuns,
    int TotalPoints,
    int NormalPoints,
    int FaultPoints,
    string CompletionRateText);

public sealed record FaultStatisticsReportModel(
    DateOnly ReportDate,
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string FaultType,
    int FaultTotal,
    int OfflineFaults,
    int PlaybackFailedFaults,
    int ImageAbnormalFaults,
    int NewFaults,
    int RepeatedFaults);

public sealed record DispatchDisposalReportModel(
    DateOnly ReportDate,
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string FaultType,
    int PendingDispatchCount,
    int DispatchedCount,
    int RecoveredCount,
    int UnrecoveredCount,
    int AutomaticDispatchCount,
    int ManualDispatchCount);

public sealed record ResponsibilityOwnershipReportModel(
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string MaintainerName,
    string SupervisorName,
    string FaultType,
    int FaultCount,
    int RecoveredCount,
    int UnrecoveredCount);

public sealed record OutstandingFaultReportModel(
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

public sealed record ReportsWorkspaceSnapshot(
    IReadOnlyList<InspectionExecutionReportModel> InspectionExecutionRows,
    IReadOnlyList<FaultStatisticsReportModel> FaultStatisticsRows,
    IReadOnlyList<DispatchDisposalReportModel> DispatchDisposalRows,
    IReadOnlyList<ResponsibilityOwnershipReportModel> ResponsibilityOwnershipRows,
    IReadOnlyList<OutstandingFaultReportModel> OutstandingFaultRows);

public interface IReportDataService
{
    ServiceResponse<ReportsWorkspaceSnapshot> GetWorkspace(ReportQueryDto query);
}
