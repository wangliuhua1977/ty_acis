using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed class ConfigDrivenDispatchNotificationService : IDispatchNotificationService
{
    private readonly IFaultPoolService _faultPoolService;
    private readonly IDispatchNotificationService _notificationFallback;

    public ConfigDrivenDispatchNotificationService(
        IFaultPoolService faultPoolService,
        IDispatchNotificationService notificationFallback)
    {
        _faultPoolService = faultPoolService;
        _notificationFallback = notificationFallback;
    }

    public ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>> GetWorkOrders()
    {
        var response = _faultPoolService.GetFaultPool();
        if (!response.IsSuccess || response.Data.Count == 0)
        {
            return _notificationFallback.GetWorkOrders();
        }

        var workOrders = response.Data
            .Select(item => new DispatchWorkOrderModel(
                item.FaultKey,
                item.PointId,
                item.PointName,
                item.FaultType,
                item.InspectionGroupName,
                item.MapLocationText,
                item.ScreenshotTitle,
                item.ScreenshotSubtitle,
                item.FaultSummary,
                item.LatestInspectionConclusion,
                item.EntersDispatchPool,
                item.IsTodayNew,
                item.DispatchMethod,
                item.WorkOrderStatus,
                item.RecoveryStatus,
                item.Responsibility,
                item.NotificationRecord,
                new DispatchRepeatFaultModel(
                    item.FirstDetectedAt.ToString("yyyy-MM-dd HH:mm"),
                    item.LatestDetectedAt.ToString("yyyy-MM-dd HH:mm"),
                    item.RepeatCount)))
            .ToList();

        return ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>>.Success(workOrders, response.Message);
    }

    public ServiceResponse<DispatchNotificationResult> SendFaultNotification(DispatchNotificationRequestDto request)
    {
        return _notificationFallback.SendFaultNotification(request);
    }

    public ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request)
    {
        return _notificationFallback.SendRecoveryNotification(request);
    }
}
