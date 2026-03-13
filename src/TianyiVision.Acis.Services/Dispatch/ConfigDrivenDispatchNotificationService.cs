using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed class ConfigDrivenDispatchNotificationService : IDispatchNotificationService
{
    private const string FaultSendType = "FaultNotification";
    private const string RecoverySendType = "RecoveryNotification";

    private readonly IFaultPoolService _faultPoolService;
    private readonly IDispatchNotificationSender? _primarySender;
    private readonly IDispatchNotificationHistoryService _historyService;
    private readonly IDispatchNotificationService _notificationFallback;
    private readonly bool _enableFallback;

    public ConfigDrivenDispatchNotificationService(
        IFaultPoolService faultPoolService,
        IDispatchNotificationSender? primarySender,
        IDispatchNotificationHistoryService historyService,
        IDispatchNotificationService notificationFallback,
        bool enableFallback)
    {
        _faultPoolService = faultPoolService;
        _primarySender = primarySender;
        _historyService = historyService;
        _notificationFallback = notificationFallback;
        _enableFallback = enableFallback;
    }

    public ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>> GetWorkOrders()
    {
        var historySnapshot = _historyService.Load();
        var latestFaultNotifications = historySnapshot.Entries
            .Where(item => string.Equals(item.SendType, FaultSendType, StringComparison.Ordinal))
            .GroupBy(item => item.WorkOrderId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.SentAt).First(), StringComparer.Ordinal);
        var latestRecoveryNotifications = historySnapshot.Entries
            .Where(item => string.Equals(item.SendType, RecoverySendType, StringComparison.Ordinal))
            .GroupBy(item => item.WorkOrderId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.SentAt).First(), StringComparer.Ordinal);

        var response = _faultPoolService.GetFaultPool();
        if (!response.IsSuccess || response.Data.Count == 0)
        {
            var fallback = _notificationFallback.GetWorkOrders();
            var fallbackItems = fallback.Data
                .Select(item => ApplyHistory(item, latestFaultNotifications, latestRecoveryNotifications))
                .ToList();
            return fallback.IsSuccess
                ? ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>>.Success(fallbackItems, fallback.Message)
                : fallback;
        }

        var workOrders = response.Data
            .Select(item => MapWorkOrder(item, latestFaultNotifications, latestRecoveryNotifications))
            .ToList();

        return ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>>.Success(workOrders, response.Message);
    }

    public ServiceResponse<DispatchNotificationResult> SendFaultNotification(DispatchNotificationRequestDto request)
    {
        return Send(
            request,
            FaultSendType,
            primary => primary.SendFaultNotification(request),
            fallback => fallback.SendFaultNotification(request));
    }

    public ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request)
    {
        return Send(
            request,
            RecoverySendType,
            primary => primary.SendRecoveryNotification(request),
            fallback => fallback.SendRecoveryNotification(request));
    }

    private DispatchWorkOrderModel MapWorkOrder(
        FaultPoolItemModel item,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestFaultNotifications,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestRecoveryNotifications)
    {
        latestFaultNotifications.TryGetValue(item.FaultKey, out var latestFaultNotification);
        latestRecoveryNotifications.TryGetValue(item.FaultKey, out var latestRecoveryNotification);

        var workOrderStatus = latestFaultNotification?.IsSuccess == true
            ? DispatchWorkOrderStatusModel.Dispatched
            : item.WorkOrderStatus;
        var recoveryStatus = latestRecoveryNotification?.IsSuccess == true
            ? DispatchRecoveryStatusModel.Recovered
            : item.RecoveryStatus;
        var notificationRecord = new DispatchNotificationRecordModel(
            latestFaultNotification?.SentAt.ToString("yyyy-MM-dd HH:mm") ?? item.NotificationRecord.FaultNotificationSentAt,
            latestFaultNotification?.ResultText ?? item.NotificationRecord.FaultNotificationStatus,
            latestRecoveryNotification?.SentAt.ToString("yyyy-MM-dd HH:mm") ?? item.NotificationRecord.RecoveryNotificationSentAt,
            latestRecoveryNotification?.ResultText ?? item.NotificationRecord.RecoveryNotificationStatus);

        return new DispatchWorkOrderModel(
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
            workOrderStatus,
            recoveryStatus,
            item.Responsibility,
            notificationRecord,
            new DispatchRepeatFaultModel(
                item.FirstDetectedAt.ToString("yyyy-MM-dd HH:mm"),
                item.LatestDetectedAt.ToString("yyyy-MM-dd HH:mm"),
                item.RepeatCount));
    }

    private static DispatchWorkOrderModel ApplyHistory(
        DispatchWorkOrderModel item,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestFaultNotifications,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestRecoveryNotifications)
    {
        latestFaultNotifications.TryGetValue(item.WorkOrderId, out var latestFaultNotification);
        latestRecoveryNotifications.TryGetValue(item.WorkOrderId, out var latestRecoveryNotification);

        var workOrderStatus = latestFaultNotification?.IsSuccess == true
            ? DispatchWorkOrderStatusModel.Dispatched
            : item.WorkOrderStatus;
        var recoveryStatus = latestRecoveryNotification?.IsSuccess == true
            ? DispatchRecoveryStatusModel.Recovered
            : item.RecoveryStatus;
        var notificationRecord = new DispatchNotificationRecordModel(
            latestFaultNotification?.SentAt.ToString("yyyy-MM-dd HH:mm") ?? item.NotificationRecord.FaultNotificationSentAt,
            latestFaultNotification?.ResultText ?? item.NotificationRecord.FaultNotificationStatus,
            latestRecoveryNotification?.SentAt.ToString("yyyy-MM-dd HH:mm") ?? item.NotificationRecord.RecoveryNotificationSentAt,
            latestRecoveryNotification?.ResultText ?? item.NotificationRecord.RecoveryNotificationStatus);

        return item with
        {
            WorkOrderStatus = workOrderStatus,
            RecoveryStatus = recoveryStatus,
            NotificationRecord = notificationRecord
        };
    }

    private ServiceResponse<DispatchNotificationResult> Send(
        DispatchNotificationRequestDto request,
        string sendType,
        Func<IDispatchNotificationSender, ServiceResponse<DispatchNotificationResult>> primaryFactory,
        Func<IDispatchNotificationService, ServiceResponse<DispatchNotificationResult>> fallbackFactory)
    {
        if (_primarySender is null)
        {
            var demoResponse = fallbackFactory(_notificationFallback);
            PersistHistory(request, sendType, demoResponse, false, true);
            return demoResponse;
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

        if (primary.IsSuccess)
        {
            PersistHistory(request, sendType, primary, true, false);
            return primary;
        }

        if (!_enableFallback)
        {
            PersistHistory(request, sendType, primary, false, false);
            return primary;
        }

        var fallback = fallbackFactory(_notificationFallback);
        if (fallback.IsSuccess)
        {
            var merged = ServiceResponse<DispatchNotificationResult>.Success(
                new DispatchNotificationResult(
                    fallback.Data.SentAt,
                    $"{fallback.Data.StatusText} 已记录真实链路失败并回退 demo。",
                    fallback.Data.TimelineActor),
                primary.Message);
            PersistHistory(request, sendType, merged, false, true);
            return merged;
        }

        PersistHistory(request, sendType, primary, false, false);
        return primary;
    }

    private void PersistHistory(
        DispatchNotificationRequestDto request,
        string sendType,
        ServiceResponse<DispatchNotificationResult> response,
        bool wasRealSend,
        bool usedDemoFallback)
    {
        var sentAt = DateTime.TryParse(response.Data.SentAt, out var parsed)
            ? parsed
            : DateTime.Now;
        _historyService.Append(new DispatchNotificationHistoryEntry(
            request.WorkOrderId,
            request.PointId,
            request.FaultType,
            sendType,
            sentAt,
            request.NotificationChannelId,
            response.IsSuccess,
            response.Data.StatusText,
            string.IsNullOrWhiteSpace(response.Message) ? response.Data.StatusText : response.Message,
            wasRealSend,
            usedDemoFallback));
    }
}
