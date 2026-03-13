using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed record FaultPoolItemModel(
    string FaultKey,
    string PointId,
    string PointName,
    string FaultType,
    string InspectionGroupName,
    string CurrentHandlingUnit,
    string MapLocationText,
    string FaultSummary,
    string LatestInspectionConclusion,
    string ScreenshotTitle,
    string ScreenshotSubtitle,
    bool EntersDispatchPool,
    bool IsTodayNew,
    DispatchMethodModel DispatchMethod,
    DispatchWorkOrderStatusModel WorkOrderStatus,
    DispatchRecoveryStatusModel RecoveryStatus,
    DispatchResponsibilityModel Responsibility,
    DispatchNotificationRecordModel NotificationRecord,
    DateTime FirstDetectedAt,
    DateTime LatestDetectedAt,
    int RepeatCount,
    IReadOnlyList<string> AlertSources);

public interface IFaultPoolService
{
    ServiceResponse<IReadOnlyList<FaultPoolItemModel>> GetFaultPool();
}
