using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Dispatch;

public enum DispatchMethodModel
{
    Automatic,
    Manual
}

public enum DispatchWorkOrderStatusModel
{
    PendingDispatch,
    Dispatched
}

public enum DispatchRecoveryStatusModel
{
    Unrecovered,
    Recovered
}

public sealed record DispatchResponsibilityModel(
    string CurrentHandlingUnit,
    string MaintainerName,
    string MaintainerPhone,
    string SupervisorName,
    string SupervisorPhone,
    string NotificationChannelId,
    string SourceTag);

public sealed record DispatchNotificationTimelineEntryModel(
    string SendType,
    string SentAt,
    string StatusText,
    string TimelineActor);

public sealed record DispatchNotificationRecordModel(
    string FaultNotificationSentAt,
    string FaultNotificationStatus,
    string RecoveryConfirmedAt,
    string RecoverySourceTag,
    string RecoveryNotificationSentAt,
    string RecoveryNotificationStatus,
    IReadOnlyList<DispatchNotificationTimelineEntryModel> TimelineEntries,
    string RecoverySummary = "");

public sealed record DispatchRepeatFaultModel(
    string FirstFaultTime,
    string LatestFaultTime,
    int RepeatCount);

public sealed record DispatchWorkOrderModel(
    string WorkOrderId,
    string PointId,
    string PointName,
    string FaultType,
    string InspectionGroupName,
    string MapLocationPlaceholder,
    string ScreenshotTitle,
    string ScreenshotSubtitle,
    string FaultSummary,
    string LatestInspectionConclusion,
    bool EntersDispatchPool,
    bool IsTodayNew,
    DispatchMethodModel DispatchMethod,
    DispatchWorkOrderStatusModel WorkOrderStatus,
    DispatchRecoveryStatusModel RecoveryStatus,
    DispatchResponsibilityModel Responsibility,
    DispatchNotificationRecordModel NotificationRecord,
    DispatchRepeatFaultModel RepeatFault,
    string InspectionTaskId = "",
    string DeviceCode = "")
{
    public string FaultKey { get; init; } = string.Empty;
}

public sealed record DispatchInspectionWorkOrderUpsertRequest(
    string PointId,
    string DeviceCode,
    string PointName,
    string FaultType,
    string FaultKey,
    string InspectionGroupName,
    string MapLocationPlaceholder,
    string ScreenshotTitle,
    string ScreenshotSubtitle,
    string FaultSummary,
    string LatestInspectionConclusion,
    bool EntersDispatchPool,
    DispatchMethodModel DispatchMethod,
    DispatchResponsibilityModel Responsibility,
    DateTime DetectedAt,
    string InspectionTaskId);

public sealed record DispatchInspectionWorkOrderUpsertResult(
    string WorkOrderId,
    string FaultKey,
    DispatchWorkOrderStatusModel? DispatchStatusBefore,
    DispatchWorkOrderStatusModel DispatchStatusAfter,
    DispatchRecoveryStatusModel? RecoveryStatusBefore,
    DispatchRecoveryStatusModel RecoveryStatusAfter,
    bool ReopenTriggered,
    bool Deduplicated);

public sealed record DispatchResponsibilityQueryDto(
    string PointId,
    string PointName,
    string CurrentHandlingUnit);

public sealed record DispatchResponsibilityUpdateDto(
    string PointId,
    string PointName,
    string CurrentHandlingUnit,
    string MaintainerName,
    string MaintainerPhone,
    string SupervisorName,
    string SupervisorPhone,
    string NotificationChannelId);

public sealed record DispatchNotificationResult(
    string SentAt,
    string StatusText,
    string TimelineActor);

public interface IDispatchNotificationService
{
    ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>> GetWorkOrders();

    ServiceResponse<DispatchNotificationResult> SendFaultNotification(DispatchNotificationRequestDto request);

    ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request);
}

public interface IDispatchResponsibilityService
{
    ServiceResponse<DispatchResponsibilityModel> Resolve(DispatchResponsibilityQueryDto request);

    ServiceResponse<DispatchResponsibilityModel> Save(DispatchResponsibilityUpdateDto request);
}
