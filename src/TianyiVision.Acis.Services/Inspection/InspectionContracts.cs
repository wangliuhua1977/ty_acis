using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

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

public enum InspectionTaskTypeModel
{
    SinglePoint,
    Batch,
    ScopePlan
}

public enum InspectionTaskTriggerModel
{
    Manual,
    Scheduled
}

public enum InspectionTaskStatusModel
{
    Pending,
    Running,
    Completed,
    PartialFailure,
    Failed,
    Cancelled
}

public enum InspectionPointExecutionStatusModel
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped
}

public enum InspectionPointFailureCategoryModel
{
    None,
    PointNotFound,
    ScopePlanEmpty,
    GroupBusy,
    ConcurrencyReserved,
    OnlineStatusPending,
    DeviceOffline,
    PlaybackCheckFailed,
    ImageAbnormalDetected,
    ReservedForNextRound,
    Unknown
}

public enum InspectionExecutionReservationStepModel
{
    StreamAddress,
    PlaybackReachability,
    ProtocolFallbackRetry,
    AutoScreenshot,
    ManualSupplementScreenshot,
    AiDecision
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
    string? RawLongitude,
    string? RawLatitude,
    PointCoordinateStatus CoordinateStatus,
    string MapSource,
    double X,
    double Y,
    InspectionPointStatusModel Status,
    InspectionPointStatusModel CompletionStatus,
    bool? IsOnline,
    bool IsPlayable,
    bool IsImageAbnormal,
    bool IsPreviewAvailable,
    string FaultSummary,
    string LastFaultTime,
    bool EntersDispatchPool,
    PointBusinessSummaryModel? BusinessSummary = null,
    double? MapLongitude = null,
    double? MapLatitude = null,
    double? RegisteredLongitude = null,
    double? RegisteredLatitude = null,
    CoordinateSystemKind RegisteredCoordinateSystem = CoordinateSystemKind.Unknown,
    CoordinateSystemKind MapCoordinateSystem = CoordinateSystemKind.Unknown)
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
        bool? isOnline,
        bool isPlayable,
        bool isImageAbnormal,
        bool isPreviewAvailable,
        string lastFaultTime,
        bool entersDispatchPool,
        PointBusinessSummaryModel? businessSummary = null)
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
            null,
            null,
            PointCoordinateStatus.Missing,
            "unavailable",
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
            entersDispatchPool,
            businessSummary,
            null,
            null,
            null,
            null,
            CoordinateSystemKind.Unknown,
            CoordinateSystemKind.Unknown)
    {
    }
}

public sealed record InspectionRecentFaultModel(
    string PointId,
    string PointName,
    string FaultType,
    string LatestFaultTime);

public sealed record InspectionTaskPointExecutionModel(
    string PointId,
    string DeviceCode,
    string PointName,
    int Sequence,
    InspectionPointExecutionStatusModel Status,
    string SkipReason,
    InspectionPointFailureCategoryModel FailureCategory,
    string FailureReason,
    bool IsFocusPoint,
    bool UsesOverridePolicy,
    string PolicySnapshotSummary);

public sealed record InspectionReservedStepModel(
    InspectionExecutionReservationStepModel Step,
    string EntryPoint,
    string Summary);

public sealed record InspectionTaskRecordModel(
    string TaskId,
    string GroupId,
    string GroupName,
    string TaskName,
    InspectionTaskTypeModel TaskType,
    InspectionTaskTriggerModel TriggerMode,
    InspectionTaskStatusModel Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? ScopePlanId,
    string ScopePlanName,
    int TotalPointCount,
    int SuccessCount,
    int FailureCount,
    int SkippedCount,
    string? CurrentPointId,
    string CurrentPointName,
    string Summary,
    IReadOnlyList<InspectionTaskPointExecutionModel> PointExecutions);

public sealed record InspectionTaskBoardModel(
    InspectionTaskRecordModel? CurrentTask,
    IReadOnlyList<InspectionTaskRecordModel> RecentTasks);

public sealed record InspectionGroupWorkspaceModel(
    InspectionGroupModel Group,
    InspectionStrategyModel Strategy,
    InspectionExecutionModel Execution,
    InspectionRunSummaryModel RunSummary,
    string TaskFinishedAt,
    InspectionTaskBoardModel TaskBoard,
    IReadOnlyList<InspectionPointModel> Points,
    IReadOnlyList<InspectionRecentFaultModel> RecentFaults);

public sealed record InspectionWorkspaceSnapshot(
    IReadOnlyList<InspectionGroupWorkspaceModel> Groups);

public sealed record InspectionPointCheckRequest(
    string TaskId,
    string GroupId,
    string TaskName,
    InspectionTaskTypeModel TaskType,
    InspectionTaskTriggerModel TriggerMode,
    string? ScopePlanId,
    string ScopePlanName,
    PointWorkspaceItemModel Point,
    InspectionPointPolicySettings Policy,
    InspectionVideoInspectionSettings VideoInspection,
    bool IsFocusPoint,
    bool UsesOverridePolicy,
    string PolicySnapshotSummary);

public sealed record InspectionPointCheckResult(
    InspectionPointExecutionStatusModel Status,
    InspectionPointFailureCategoryModel FailureCategory,
    string ResultSummary,
    IReadOnlyList<InspectionReservedStepModel> ReservedSteps);

public sealed class InspectionTaskBoardChangedEventArgs : EventArgs
{
    public InspectionTaskBoardChangedEventArgs(string groupId)
    {
        GroupId = groupId;
    }

    public string GroupId { get; }
}

public interface IInspectionPointCheckExecutor
{
    Task<InspectionPointCheckResult> ExecuteAsync(InspectionPointCheckRequest request, CancellationToken cancellationToken);
}

public interface IInspectionTaskHistoryStore
{
    IReadOnlyList<InspectionTaskRecordModel> Load();

    void Save(IReadOnlyList<InspectionTaskRecordModel> tasks);
}

public interface IInspectionTaskService
{
    event EventHandler<InspectionTaskBoardChangedEventArgs>? TaskBoardChanged;

    ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace();

    ServiceResponse<InspectionTaskRecordModel> StartSinglePointInspection(string groupId, string pointId);

    ServiceResponse<InspectionTaskRecordModel> StartBatchInspection(string groupId, IReadOnlyList<string> pointIds);

    ServiceResponse<InspectionTaskRecordModel> StartDefaultScopeInspection(string groupId);
}
