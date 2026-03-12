using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Inspection;

public enum InspectionPointStatusModel
{
    Pending,
    Inspecting,
    Normal,
    Fault,
    Silent,
    PausedUntilRecovery
}

public sealed record InspectionGroupModel(
    string Id,
    string Name,
    string Summary,
    bool IsEnabled);

public sealed record InspectionStrategyModel(
    string FirstRunTime,
    string DailyExecutionCount,
    string Interval,
    string ResultMode,
    string DispatchMode);

public sealed record InspectionExecutionModel(
    string ExecutedToday,
    string CurrentTaskStatus,
    string NextRunTime,
    string SimulationNote,
    bool IsEnabled);

public sealed record InspectionRunSummaryModel(
    string GroupName,
    string StartedAt);

public sealed record InspectionPointModel(
    string Id,
    string Name,
    string UnitName,
    string CurrentHandlingUnit,
    double X,
    double Y,
    InspectionPointStatusModel Status,
    InspectionPointStatusModel CompletionStatus,
    bool IsOnline,
    bool IsPlayable,
    bool IsImageAbnormal,
    bool IsPreviewAvailable,
    string LastFaultTime,
    bool EntersDispatchPool);

public sealed record InspectionRecentFaultModel(
    string PointId,
    string PointName,
    string FaultType,
    string LatestFaultTime);

public sealed record InspectionGroupWorkspaceModel(
    InspectionGroupModel Group,
    InspectionStrategyModel Strategy,
    InspectionExecutionModel Execution,
    InspectionRunSummaryModel RunSummary,
    string TaskFinishedAt,
    IReadOnlyList<InspectionPointModel> Points,
    IReadOnlyList<InspectionRecentFaultModel> RecentFaults);

public sealed record InspectionWorkspaceSnapshot(
    IReadOnlyList<InspectionGroupWorkspaceModel> Groups);

public interface IInspectionTaskService
{
    ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace();
}
