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
    string DeviceCode,
    string Name,
    string UnitName,
    string CurrentHandlingUnit,
    double Longitude,
    double Latitude,
    bool CanRenderOnMap,
    string CoordinateStatusText,
    double X,
    double Y,
    InspectionPointStatusModel Status,
    InspectionPointStatusModel CompletionStatus,
    bool IsOnline,
    bool IsPlayable,
    bool IsImageAbnormal,
    bool IsPreviewAvailable,
    string FaultSummary,
    string LastFaultTime,
    bool EntersDispatchPool)
{
    public InspectionPointModel(
        string id,
        string name,
        string unitName,
        string currentHandlingUnit,
        double x,
        double y,
        InspectionPointStatusModel status,
        InspectionPointStatusModel completionStatus,
        bool isOnline,
        bool isPlayable,
        bool isImageAbnormal,
        bool isPreviewAvailable,
        string lastFaultTime,
        bool entersDispatchPool)
        : this(
            id,
            id,
            name,
            unitName,
            currentHandlingUnit,
            0d,
            0d,
            false,
            "待地图坐标",
            x,
            y,
            status,
            completionStatus,
            isOnline,
            isPlayable,
            isImageAbnormal,
            isPreviewAvailable,
            string.Empty,
            lastFaultTime,
            entersDispatchPool)
    {
    }
}

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
