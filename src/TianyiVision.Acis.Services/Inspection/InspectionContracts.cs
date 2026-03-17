using System.Text.Json.Serialization;
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
    OnlineCheckFailed,
    NoStreamAddress,
    PlaybackCheckFailed,
    PlaybackTimeout,
    ProtocolFallbackStillFailed,
    PlaybackSucceeded,
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

public static class InspectionEvidenceValueKeys
{
    public const string CaptureStateNone = "none";
    public const string CaptureStatePending = "pending_capture";
    public const string CaptureStateCompleted = "completed";
    public const string CaptureStateFailed = "capture_failed";

    public const string EvidenceKindPlaybackScreenshot = "playback_screenshot";
    public const string EvidenceKindFailureSnapshot = "failure_snapshot";

    public const string EvidenceSourcePreviewHost = "preview_host";
    public const string EvidenceSourceFailureCard = "failure_card";

    public const string AiAnalysisReserved = "reserved";
    public const string AiAnalysisPending = "pending";
    public const string AiAnalysisCompleted = "completed";
    public const string AiAnalysisFailed = "failed";
}

public static class InspectionDispatchValueKeys
{
    public const string None = "none";
    public const string PendingDispatch = "pending_dispatch";
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

public sealed record InspectionPointEvidenceMetadataModel(
    string TaskId,
    string PointId,
    string DeviceCode,
    DateTime ScreenshotTime,
    string LocalFilePath,
    string EvidenceSummary)
{
    public string EvidenceKind { get; init; } = InspectionEvidenceValueKeys.EvidenceKindPlaybackScreenshot;

    public string EvidenceSource { get; init; } = InspectionEvidenceValueKeys.EvidenceSourcePreviewHost;

    public string AiAnalysisStatus { get; init; } = InspectionEvidenceValueKeys.AiAnalysisReserved;

    public string AiAnalysisSummary { get; init; } = string.Empty;
}

public sealed record InspectionAbnormalFlowEntryModel(
    string PointId,
    string DeviceCode,
    string PointName,
    string AiAnalysisStatus,
    IReadOnlyList<string> AbnormalTags,
    string AiAnalysisSummary = "")
{
    public string PrimaryFaultType { get; init; } = string.Empty;

    public bool DispatchCandidateAccepted { get; init; }

    public bool DispatchUpserted { get; init; }

    public bool DispatchDeduplicated { get; init; }

    public string DispatchStatus { get; init; } = InspectionDispatchValueKeys.None;
}

public sealed record InspectionTaskAbnormalFlowModel(
    IReadOnlyList<InspectionAbnormalFlowEntryModel> ReviewWallPendingEntries,
    IReadOnlyList<InspectionAbnormalFlowEntryModel> DispatchPoolCandidateEntries,
    IReadOnlyList<InspectionAbnormalFlowEntryModel> ManualReviewRequiredEntries)
{
    public static InspectionTaskAbnormalFlowModel Empty { get; } = new([], [], []);

    public int ReviewWallPendingCount => ReviewWallPendingEntries.Count;

    public int DispatchPoolCandidateCount => DispatchPoolCandidateEntries.Count;

    public int ManualReviewCompatibilityCount => ManualReviewRequiredEntries.Count;

    public int DispatchPendingCount => DispatchPoolCandidateEntries.Count(entry =>
        string.Equals(entry.DispatchStatus, InspectionDispatchValueKeys.PendingDispatch, StringComparison.Ordinal));
}

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
    string PolicySnapshotSummary)
{
    public string UnitName { get; init; } = string.Empty;

    public string CurrentHandlingUnit { get; init; } = string.Empty;

    public string ExecutionSummary { get; init; } = string.Empty;

    public string OnlineCheckResult { get; init; } = string.Empty;

    public string StreamUrlAcquireResult { get; init; } = string.Empty;

    public int PlaybackAttemptCount { get; init; }

    public bool ProtocolFallbackUsed { get; init; }

    public string FinalPlaybackResult { get; init; } = string.Empty;

    public string PreviewUrl { get; init; } = string.Empty;

    public string PreviewHostKind { get; init; } = string.Empty;

    public int ScreenshotPlannedCount { get; init; }

    public int ScreenshotIntervalSeconds { get; init; }

    public int ScreenshotSuccessCount { get; init; }

    public string EvidenceCaptureState { get; init; } = InspectionEvidenceValueKeys.CaptureStateNone;

    public string EvidenceSummary { get; init; } = string.Empty;

    public string EvidenceRetentionMode { get; init; } = string.Empty;

    public int EvidenceRetentionDays { get; init; }

    public bool AllowManualSupplementScreenshot { get; init; }

    public string AiAnalysisStatus { get; init; } = InspectionEvidenceValueKeys.AiAnalysisReserved;

    public string AiAnalysisSummary { get; init; } = string.Empty;

    public bool IsAiAbnormalDetected { get; init; }

    public IReadOnlyList<string> AiAbnormalTags { get; init; } = [];

    public double AiConfidence { get; init; }

    public string AiSuggestedAction { get; init; } = string.Empty;

    public string PrimaryFaultType { get; init; } = string.Empty;

    public bool RouteToReviewWallReserved { get; init; }

    public bool RouteToDispatchPoolReserved { get; init; }

    public bool ManualReviewRequiredReserved { get; init; }

    public bool DispatchCandidateAccepted { get; init; }

    public bool DispatchUpserted { get; init; }

    public bool DispatchDeduplicated { get; init; }

    public string DispatchStatus { get; init; } = InspectionDispatchValueKeys.None;

    public IReadOnlyList<InspectionPointEvidenceMetadataModel> EvidenceItems { get; init; } = [];

    [JsonPropertyName("screenshotReserved")]
    public string ScreenshotReserved { get; init; } = "reserved";

    [JsonPropertyName("evidenceReserved")]
    public string EvidenceReserved { get; init; } = "reserved";

    [JsonPropertyName("aiAnalysisReserved")]
    public string AiAnalysisReserved { get; init; } = "reserved";
}

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
    IReadOnlyList<InspectionTaskPointExecutionModel> PointExecutions)
{
    public InspectionTaskAbnormalFlowModel AbnormalFlow { get; init; } = InspectionTaskAbnormalFlowModel.Empty;
}

public static class InspectionTaskModelExtensions
{
    public static int GetCompletedPointCount(this InspectionTaskRecordModel task)
        => task.SuccessCount + task.FailureCount + task.SkippedCount;

    public static bool IsPlaceholderTask(this InspectionTaskRecordModel task)
        => task.TotalPointCount == 0
           && (task.PointExecutions?.Count ?? 0) == 0
           && string.Equals(task.TaskId, $"{task.GroupId}-empty", StringComparison.Ordinal);

    public static InspectionTaskPointExecutionModel? FindPointExecution(this InspectionTaskRecordModel task, string? pointId)
    {
        if (string.IsNullOrWhiteSpace(pointId) || task.PointExecutions is null)
        {
            return null;
        }

        return task.PointExecutions.FirstOrDefault(candidate =>
            string.Equals(candidate.PointId, pointId, StringComparison.Ordinal));
    }

    public static string ResolveCurrentPointDisplayName(this InspectionTaskRecordModel task)
    {
        if (!string.IsNullOrWhiteSpace(task.CurrentPointName)
            && !string.Equals(task.CurrentPointName, "--", StringComparison.Ordinal))
        {
            return task.CurrentPointName;
        }

        return task.FindPointExecution(task.CurrentPointId)?.PointName ?? "--";
    }

    public static string ResolvePointInspectionSummary(this InspectionTaskRecordModel task, string? pointId)
    {
        var pointExecution = task.FindPointExecution(pointId);
        if (pointExecution is null)
        {
            return string.IsNullOrWhiteSpace(task.Summary) ? "--" : task.Summary;
        }

        if (!string.IsNullOrWhiteSpace(pointExecution.AiAnalysisSummary))
        {
            return pointExecution.AiAnalysisSummary;
        }

        if (!string.IsNullOrWhiteSpace(pointExecution.ExecutionSummary))
        {
            return pointExecution.ExecutionSummary;
        }

        if (pointExecution.Status == InspectionPointExecutionStatusModel.Failed
            && !string.IsNullOrWhiteSpace(pointExecution.FailureReason))
        {
            return pointExecution.FailureReason;
        }

        if (pointExecution.Status == InspectionPointExecutionStatusModel.Skipped
            && !string.IsNullOrWhiteSpace(pointExecution.SkipReason))
        {
            return pointExecution.SkipReason;
        }

        return pointExecution.Status switch
        {
            InspectionPointExecutionStatusModel.Pending => $"点位“{pointExecution.PointName}”待执行。",
            InspectionPointExecutionStatusModel.Running => $"点位“{pointExecution.PointName}”执行中。",
            InspectionPointExecutionStatusModel.Succeeded => $"点位“{pointExecution.PointName}”已完成巡检。",
            _ => string.IsNullOrWhiteSpace(task.Summary) ? "--" : task.Summary
        };
    }

    public static InspectionTaskAbnormalFlowModel BuildAbnormalFlowSnapshot(
        IReadOnlyList<InspectionTaskPointExecutionModel>? pointExecutions)
    {
        var normalizedExecutions = pointExecutions ?? Array.Empty<InspectionTaskPointExecutionModel>();
        var reviewWallEntries = CreateEntryList(normalizedExecutions, point => point.RouteToReviewWallReserved);
        var dispatchPoolEntries = CreateEntryList(normalizedExecutions, point => point.RouteToDispatchPoolReserved);
        var manualReviewEntries = CreateEntryList(normalizedExecutions, point => point.ManualReviewRequiredReserved);

        return new InspectionTaskAbnormalFlowModel(
            reviewWallEntries,
            dispatchPoolEntries,
            manualReviewEntries);
    }

    public static bool ShouldRouteToReviewWall(this InspectionTaskRecordModel task, string? pointId)
        => ContainsFlowEntry(task.AbnormalFlow.ReviewWallPendingEntries, pointId);

    public static bool ShouldRouteToDispatchPool(this InspectionTaskRecordModel task, string? pointId)
        => ContainsFlowEntry(task.AbnormalFlow.DispatchPoolCandidateEntries, pointId);

    public static bool RequiresManualReview(this InspectionTaskRecordModel task, string? pointId)
        => ContainsFlowEntry(task.AbnormalFlow.ManualReviewRequiredEntries, pointId);

    public static InspectionAbnormalFlowEntryModel? FindReviewWallEntry(this InspectionTaskRecordModel task, string? pointId)
        => FindFlowEntry(task.AbnormalFlow.ReviewWallPendingEntries, pointId);

    public static InspectionAbnormalFlowEntryModel? FindDispatchPoolEntry(this InspectionTaskRecordModel task, string? pointId)
        => FindFlowEntry(task.AbnormalFlow.DispatchPoolCandidateEntries, pointId);

    public static InspectionAbnormalFlowEntryModel? FindManualReviewCompatibilityEntry(this InspectionTaskRecordModel task, string? pointId)
        => FindFlowEntry(task.AbnormalFlow.ManualReviewRequiredEntries, pointId);

    private static InspectionAbnormalFlowEntryModel CreateAbnormalFlowEntry(InspectionTaskPointExecutionModel point)
    {
        return new InspectionAbnormalFlowEntryModel(
            point.PointId,
            point.DeviceCode,
            point.PointName,
            point.AiAnalysisStatus,
            (point.AiAbnormalTags ?? Array.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            point.AiAnalysisSummary)
        {
            PrimaryFaultType = ResolvePrimaryFaultType(point),
            DispatchCandidateAccepted = point.DispatchCandidateAccepted,
            DispatchUpserted = point.DispatchUpserted,
            DispatchDeduplicated = point.DispatchDeduplicated,
            DispatchStatus = string.IsNullOrWhiteSpace(point.DispatchStatus)
                ? InspectionDispatchValueKeys.None
                : point.DispatchStatus.Trim()
        };
    }

    private static IReadOnlyList<InspectionAbnormalFlowEntryModel> CreateEntryList(
        IEnumerable<InspectionTaskPointExecutionModel> pointExecutions,
        Func<InspectionTaskPointExecutionModel, bool> predicate)
    {
        return pointExecutions
            .Where(predicate)
            .Select(CreateAbnormalFlowEntry)
            .GroupBy(entry => entry.PointId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.PointName, StringComparer.Ordinal)
            .ToList();
    }

    private static bool ContainsFlowEntry(
        IReadOnlyList<InspectionAbnormalFlowEntryModel>? entries,
        string? pointId)
    {
        if (string.IsNullOrWhiteSpace(pointId) || entries is null)
        {
            return false;
        }

        return entries.Any(entry => string.Equals(entry.PointId, pointId, StringComparison.Ordinal));
    }

    private static InspectionAbnormalFlowEntryModel? FindFlowEntry(
        IReadOnlyList<InspectionAbnormalFlowEntryModel>? entries,
        string? pointId)
    {
        if (string.IsNullOrWhiteSpace(pointId) || entries is null)
        {
            return null;
        }

        return entries.FirstOrDefault(entry => string.Equals(entry.PointId, pointId, StringComparison.Ordinal));
    }

    private static string ResolvePrimaryFaultType(InspectionTaskPointExecutionModel point)
    {
        if (!string.IsNullOrWhiteSpace(point.PrimaryFaultType))
        {
            return point.PrimaryFaultType.Trim();
        }

        if (point.AiAbnormalTags is { Count: > 0 })
        {
            var tags = point.AiAbnormalTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (tags.Count > 0)
            {
                return string.Join("|", tags);
            }
        }

        return point.FailureCategory switch
        {
            InspectionPointFailureCategoryModel.DeviceOffline => "offline",
            InspectionPointFailureCategoryModel.NoStreamAddress => "no_stream_address",
            InspectionPointFailureCategoryModel.PlaybackCheckFailed => "playback_check_failed",
            InspectionPointFailureCategoryModel.PlaybackTimeout => "playback_timeout",
            InspectionPointFailureCategoryModel.ProtocolFallbackStillFailed => "protocol_fallback_still_failed",
            InspectionPointFailureCategoryModel.ImageAbnormalDetected => "image_abnormal_detected",
            _ => string.Empty
        };
    }
}

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
    IReadOnlyList<InspectionReservedStepModel> ReservedSteps)
{
    public string OnlineCheckResult { get; init; } = string.Empty;

    public string StreamUrlAcquireResult { get; init; } = string.Empty;

    public int PlaybackAttemptCount { get; init; }

    public bool ProtocolFallbackUsed { get; init; }

    public string FinalPlaybackResult { get; init; } = string.Empty;

    public string FailureReason { get; init; } = string.Empty;

    public string PreviewUrl { get; init; } = string.Empty;

    public string PreviewHostKind { get; init; } = string.Empty;

    public int ScreenshotPlannedCount { get; init; }

    public int ScreenshotIntervalSeconds { get; init; }

    public int ScreenshotSuccessCount { get; init; }

    public string EvidenceCaptureState { get; init; } = InspectionEvidenceValueKeys.CaptureStateNone;

    public string EvidenceSummary { get; init; } = string.Empty;

    public string EvidenceRetentionMode { get; init; } = string.Empty;

    public int EvidenceRetentionDays { get; init; }

    public bool AllowManualSupplementScreenshot { get; init; }

    public string AiAnalysisStatus { get; init; } = InspectionEvidenceValueKeys.AiAnalysisReserved;

    public string AiAnalysisSummary { get; init; } = string.Empty;

    public IReadOnlyList<InspectionPointEvidenceMetadataModel> EvidenceItems { get; init; } = [];

    [JsonPropertyName("screenshotReserved")]
    public string ScreenshotReserved { get; init; } = "reserved";

    [JsonPropertyName("evidenceReserved")]
    public string EvidenceReserved { get; init; } = "reserved";

    [JsonPropertyName("aiAnalysisReserved")]
    public string AiAnalysisReserved { get; init; } = "reserved";
}

public sealed class InspectionTaskBoardChangedEventArgs : EventArgs
{
    public InspectionTaskBoardChangedEventArgs(string groupId)
    {
        GroupId = groupId;
    }

    public string GroupId { get; }
}

public sealed record InspectionPointEvidenceWriteRequest(
    string TaskId,
    string PointId,
    string DeviceCode,
    int ScreenshotPlannedCount,
    int ScreenshotSuccessCount,
    string EvidenceCaptureState,
    string EvidenceSummary,
    string EvidenceRetentionMode,
    int EvidenceRetentionDays,
    bool AllowManualSupplementScreenshot,
    IReadOnlyList<InspectionPointEvidenceMetadataModel> EvidenceItems)
{
    public string AiAnalysisStatus { get; init; } = InspectionEvidenceValueKeys.AiAnalysisPending;

    public string AiAnalysisSummary { get; init; } = string.Empty;
}

public sealed record InspectionPointAiAnalysisRequest(
    string TaskId,
    string PointId,
    string DeviceCode,
    string CurrentPointContextSummary,
    IReadOnlyList<InspectionPointEvidenceMetadataModel> EvidenceItems)
{
    public string EvidenceCaptureState { get; init; } = InspectionEvidenceValueKeys.CaptureStateNone;

    public string EvidenceSummary { get; init; } = string.Empty;
}

public sealed record InspectionPointAiAnalysisResult(
    string AiAnalysisStatus,
    string AiAnalysisSummary,
    bool IsAiAbnormalDetected,
    IReadOnlyList<string> AbnormalTags,
    double Confidence,
    string SuggestedAction,
    bool AiAnalysisInvoked,
    bool RouteToReviewWallReserved,
    bool RouteToDispatchPoolReserved,
    bool ManualReviewRequiredReserved);

public enum InspectionDispatchBridgeSource
{
    AiResultDirect,
    ReviewWallConfirmed
}

public sealed record InspectionDispatchBridgeRequest(
    InspectionTaskRecordModel Task,
    InspectionDispatchBridgeSource Source,
    IReadOnlyList<string> PointIds);

public sealed record InspectionDispatchBridgePointResult(
    string PointId,
    string DeviceCode,
    bool DispatchCandidateAccepted,
    bool DispatchUpserted,
    bool DispatchDeduplicated,
    string DispatchStatus);

public sealed record InspectionDispatchBridgeBatchResult(
    IReadOnlyList<InspectionDispatchBridgePointResult> Points)
{
    public static InspectionDispatchBridgeBatchResult Empty { get; } = new([]);
}

public sealed record InspectionReviewDispatchRequest(
    string TaskId,
    IReadOnlyList<string> PointIds);

public interface IInspectionPointCheckExecutor
{
    Task<InspectionPointCheckResult> ExecuteAsync(InspectionPointCheckRequest request, CancellationToken cancellationToken);
}

public interface IInspectionEvidenceAiAnalysisService
{
    InspectionPointAiAnalysisResult Analyze(InspectionPointAiAnalysisRequest request);
}

public interface IInspectionDispatchBridgeService
{
    InspectionDispatchBridgeBatchResult Bridge(InspectionDispatchBridgeRequest request);
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

    ServiceResponse<InspectionTaskRecordModel> WritePointEvidence(InspectionPointEvidenceWriteRequest request);

    ServiceResponse<InspectionTaskRecordModel> ConfirmReviewDispatch(InspectionReviewDispatchRequest request);
}
