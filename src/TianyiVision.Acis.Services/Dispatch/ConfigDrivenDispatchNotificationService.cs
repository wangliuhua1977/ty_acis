using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed class ConfigDrivenDispatchNotificationService : IDispatchNotificationService
{
    private readonly IFaultPoolService _faultPoolService;
    private readonly IDispatchNotificationSender? _primarySender;
    private readonly IDispatchNotificationService _notificationFallback;
    private readonly bool _enableFallback;

    public ConfigDrivenDispatchNotificationService(
        IFaultPoolService faultPoolService,
        IDispatchNotificationSender? primarySender,
        IDispatchNotificationService notificationFallback,
        bool enableFallback)
    {
        _faultPoolService = faultPoolService;
        _primarySender = primarySender;
        _notificationFallback = notificationFallback;
        _enableFallback = enableFallback;
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
        return Send(
            request,
            primary => primary.SendFaultNotification(request),
            fallback => fallback.SendFaultNotification(request));
    }

    public ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request)
    {
        return Send(
            request,
            primary => primary.SendRecoveryNotification(request),
            fallback => fallback.SendRecoveryNotification(request));
    }

    private ServiceResponse<DispatchNotificationResult> Send(
        DispatchNotificationRequestDto request,
        Func<IDispatchNotificationSender, ServiceResponse<DispatchNotificationResult>> primaryFactory,
        Func<IDispatchNotificationService, ServiceResponse<DispatchNotificationResult>> fallbackFactory)
    {
        if (_primarySender is null)
        {
            return fallbackFactory(_notificationFallback);
        }

        ServiceResponse<DispatchNotificationResult> primary;
        try
        {
            primary = primaryFactory(_primarySender);
        }
        catch (Exception ex)
        {
            primary = ServiceResponse<DispatchNotificationResult>.Failure(
                new DispatchNotificationResult(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    $"Dispatch notification sender failed: {ex.Message}",
                    request.CurrentHandlingUnit),
                $"Dispatch notification sender failed: {ex.Message}");
        }

        if (primary.IsSuccess || !_enableFallback)
        {
            return primary;
        }

        var fallback = fallbackFactory(_notificationFallback);
        return fallback.IsSuccess
            ? ServiceResponse<DispatchNotificationResult>.Success(
                new DispatchNotificationResult(
                    fallback.Data.SentAt,
                    $"{primary.Data.StatusText} Fallback succeeded with demo sending.",
                    fallback.Data.TimelineActor),
                primary.Message)
            : primary;
    }
}
