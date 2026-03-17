using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Inspection;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed class ConfigDrivenDispatchNotificationService : IDispatchNotificationService
{
    public const string FaultSendTypeValue = "FaultNotification";
    public const string RecoverySendTypeValue = "RecoveryNotification";

    private const string ManualRecoverySourceTag = "DispatchManualRecovery";
    private const string AutoRecoverySourceTag = "CTYunRecoveryReconciliation";

    private readonly IFaultPoolService _faultPoolService;
    private readonly IDispatchNotificationSender? _primarySender;
    private readonly IDispatchNotificationHistoryService _historyService;
    private readonly IDispatchWorkOrderSnapshotService _workOrderSnapshotService;
    private readonly IDispatchNotificationService _notificationFallback;
    private readonly bool _enableFallback;
    private readonly IInspectionTaskService? _inspectionTaskService;

    public ConfigDrivenDispatchNotificationService(
        IFaultPoolService faultPoolService,
        IDispatchNotificationSender? primarySender,
        IDispatchNotificationHistoryService historyService,
        IDispatchWorkOrderSnapshotService workOrderSnapshotService,
        IDispatchNotificationService notificationFallback,
        bool enableFallback,
        IInspectionTaskService? inspectionTaskService = null)
    {
        _faultPoolService = faultPoolService;
        _primarySender = primarySender;
        _historyService = historyService;
        _workOrderSnapshotService = workOrderSnapshotService;
        _notificationFallback = notificationFallback;
        _enableFallback = enableFallback;
        _inspectionTaskService = inspectionTaskService;
    }

    public ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>> GetWorkOrders()
    {
        var historySnapshot = _historyService.Load();
        var historyByWorkOrder = historySnapshot.Entries
            .GroupBy(item => item.WorkOrderId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DispatchNotificationHistoryEntry>)group.OrderByDescending(item => item.SentAt).ToList(),
                StringComparer.Ordinal);
        var latestFaultNotifications = CreateLatestHistoryLookup(historySnapshot.Entries, FaultSendTypeValue);
        var latestRecoveryNotifications = CreateLatestHistoryLookup(historySnapshot.Entries, RecoverySendTypeValue);
        var persistedSnapshot = _workOrderSnapshotService.Load();

        var response = _faultPoolService.GetFaultPool();
        if (response.IsSuccess)
        {
            ReconcileRecoveredWorkOrders(response.Data, persistedSnapshot.WorkOrders);

            historySnapshot = _historyService.Load();
            historyByWorkOrder = historySnapshot.Entries
                .GroupBy(item => item.WorkOrderId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<DispatchNotificationHistoryEntry>)group.OrderByDescending(item => item.SentAt).ToList(),
                    StringComparer.Ordinal);
            latestFaultNotifications = CreateLatestHistoryLookup(historySnapshot.Entries, FaultSendTypeValue);
            latestRecoveryNotifications = CreateLatestHistoryLookup(historySnapshot.Entries, RecoverySendTypeValue);
            persistedSnapshot = _workOrderSnapshotService.Load();
            var persistedById = persistedSnapshot.WorkOrders.ToDictionary(item => item.WorkOrderId, StringComparer.Ordinal);
            var liveWorkOrders = response.Data
                .Select(item => MapWorkOrder(
                    item,
                    persistedById.TryGetValue(item.FaultKey, out var persisted) ? persisted : null,
                    latestFaultNotifications,
                    latestRecoveryNotifications,
                    historyByWorkOrder))
                .ToList();

            var liveIds = liveWorkOrders.Select(item => item.WorkOrderId).ToHashSet(StringComparer.Ordinal);
            var snapshotOnlyWorkOrders = persistedSnapshot.WorkOrders
                .Where(item => !liveIds.Contains(item.WorkOrderId))
                .Select(item => ApplyHistory(item, latestFaultNotifications, latestRecoveryNotifications, historyByWorkOrder))
                .ToList();

            var merged = OrderWorkOrders(liveWorkOrders.Concat(snapshotOnlyWorkOrders)).ToList();
            _workOrderSnapshotService.Save(merged);
            return ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>>.Success(merged, response.Message);
        }

        var recoveredFromLocal = OrderWorkOrders(
                persistedSnapshot.WorkOrders.Select(item =>
                    ApplyHistory(item, latestFaultNotifications, latestRecoveryNotifications, historyByWorkOrder)))
            .ToList();
        if (recoveredFromLocal.Count > 0)
        {
            var message = string.IsNullOrWhiteSpace(response.Message)
                ? "Real fault pool was unavailable, restored dispatch work orders from the local snapshot."
                : $"{response.Message} Restored dispatch work orders from the local snapshot.";
            return ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>>.Success(recoveredFromLocal, message);
        }

        var fallback = _notificationFallback.GetWorkOrders();
        var fallbackItems = OrderWorkOrders(
                fallback.Data.Select(item =>
                    ApplyHistory(item, latestFaultNotifications, latestRecoveryNotifications, historyByWorkOrder)))
            .ToList();
        return fallback.IsSuccess
            ? ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>>.Success(fallbackItems, fallback.Message)
            : fallback;
    }

    public ServiceResponse<DispatchNotificationResult> SendFaultNotification(DispatchNotificationRequestDto request)
    {
        return Send(
            request,
            FaultSendTypeValue,
            primary => primary.SendFaultNotification(request),
            fallback => fallback.SendFaultNotification(request));
    }

    public ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request)
    {
        return ConfirmRecoveredAndSend(request, ManualRecoverySourceTag);
    }

    private DispatchWorkOrderModel MapWorkOrder(
        FaultPoolItemModel item,
        DispatchWorkOrderModel? persisted,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestFaultNotifications,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestRecoveryNotifications,
        IReadOnlyDictionary<string, IReadOnlyList<DispatchNotificationHistoryEntry>> historyByWorkOrder)
    {
        var current = new DispatchWorkOrderModel(
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
                item.RepeatCount),
            persisted?.InspectionTaskId ?? string.Empty,
            persisted?.DeviceCode ?? string.Empty);

        if (persisted is not null)
        {
            var recoveredAt = ParseTime(persisted.NotificationRecord.RecoveryConfirmedAt);
            var faultReopened = recoveredAt.HasValue && item.LatestDetectedAt > recoveredAt.Value;
            current = current with
            {
                WorkOrderStatus = persisted.WorkOrderStatus == DispatchWorkOrderStatusModel.Dispatched
                    ? DispatchWorkOrderStatusModel.Dispatched
                    : current.WorkOrderStatus,
                RecoveryStatus = persisted.RecoveryStatus == DispatchRecoveryStatusModel.Recovered && !faultReopened
                    ? DispatchRecoveryStatusModel.Recovered
                    : current.RecoveryStatus,
                InspectionTaskId = string.IsNullOrWhiteSpace(persisted.InspectionTaskId)
                    ? current.InspectionTaskId
                    : persisted.InspectionTaskId,
                DeviceCode = string.IsNullOrWhiteSpace(persisted.DeviceCode)
                    ? current.DeviceCode
                    : persisted.DeviceCode,
                NotificationRecord = faultReopened
                    ? ClearRecoveryNotification(persisted.NotificationRecord)
                    : persisted.NotificationRecord
            };
        }

        return ApplyHistory(current, latestFaultNotifications, latestRecoveryNotifications, historyByWorkOrder);
    }

    private static DispatchWorkOrderModel ApplyHistory(
        DispatchWorkOrderModel item,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestFaultNotifications,
        IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> latestRecoveryNotifications,
        IReadOnlyDictionary<string, IReadOnlyList<DispatchNotificationHistoryEntry>> historyByWorkOrder)
    {
        latestFaultNotifications.TryGetValue(item.WorkOrderId, out var latestFaultNotification);
        latestRecoveryNotifications.TryGetValue(item.WorkOrderId, out var latestRecoveryNotification);
        historyByWorkOrder.TryGetValue(item.WorkOrderId, out var historyEntries);

        var workOrderStatus = latestFaultNotification?.IsSuccess == true
            ? DispatchWorkOrderStatusModel.Dispatched
            : item.WorkOrderStatus;
        var recoveryStatus = item.RecoveryStatus == DispatchRecoveryStatusModel.Recovered || latestRecoveryNotification?.IsSuccess == true
            ? DispatchRecoveryStatusModel.Recovered
            : item.RecoveryStatus;
        var notificationRecord = new DispatchNotificationRecordModel(
            latestFaultNotification?.SentAt.ToString("yyyy-MM-dd HH:mm") ?? item.NotificationRecord.FaultNotificationSentAt,
            latestFaultNotification?.ResultText ?? item.NotificationRecord.FaultNotificationStatus,
            ResolveRecoveryConfirmedAt(item.NotificationRecord, latestRecoveryNotification),
            ResolveRecoverySource(item.NotificationRecord, latestRecoveryNotification),
            latestRecoveryNotification?.SentAt.ToString("yyyy-MM-dd HH:mm") ?? item.NotificationRecord.RecoveryNotificationSentAt,
            latestRecoveryNotification?.ResultText ?? item.NotificationRecord.RecoveryNotificationStatus,
            BuildTimelineEntries(historyEntries, item.NotificationRecord.TimelineEntries),
            item.NotificationRecord.RecoverySummary);

        return item with
        {
            WorkOrderStatus = workOrderStatus,
            RecoveryStatus = recoveryStatus,
            NotificationRecord = notificationRecord
        };
    }

    private ServiceResponse<DispatchNotificationResult> ConfirmRecoveredAndSend(
        DispatchNotificationRequestDto request,
        string recoverySource)
    {
        var persisted = _workOrderSnapshotService.Load().WorkOrders
            .FirstOrDefault(item => string.Equals(item.WorkOrderId, request.WorkOrderId, StringComparison.Ordinal));
        if (persisted?.RecoveryStatus == DispatchRecoveryStatusModel.Recovered)
        {
            var confirmedAt = string.IsNullOrWhiteSpace(persisted.NotificationRecord.RecoveryConfirmedAt)
                ? DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                : persisted.NotificationRecord.RecoveryConfirmedAt;
            WriteRecoveryLog(
                persisted.PointId,
                persisted.DeviceCode,
                ResolveDispatchStatus(persisted),
                persisted.RecoveryStatus,
                persisted.RecoveryStatus,
                recoveryConfirmed: true);
            return ServiceResponse<DispatchNotificationResult>.Success(
                new DispatchNotificationResult(
                    confirmedAt,
                    "工单已确认恢复，无需重复写回。",
                    persisted.Responsibility.CurrentHandlingUnit),
                "工单已确认恢复。");
        }

        var recoveredAt = DateTime.Now;
        var recoverySummary = BuildRecoverySummary(persisted, recoverySource, recoveredAt);
        var inspectionRecoveryResponse = ConfirmInspectionRecovery(persisted, recoveredAt, recoverySummary);
        if (!inspectionRecoveryResponse.IsSuccess)
        {
            return ServiceResponse<DispatchNotificationResult>.Failure(
                new DispatchNotificationResult(
                    recoveredAt.ToString("yyyy-MM-dd HH:mm"),
                    inspectionRecoveryResponse.Message,
                    persisted?.Responsibility.CurrentHandlingUnit ?? request.CurrentHandlingUnit),
                inspectionRecoveryResponse.Message);
        }

        _workOrderSnapshotService.MarkRecovered(request, recoveredAt, recoverySource, recoverySummary);

        WriteRecoveryLog(
            persisted?.PointId ?? request.PointId,
            persisted?.DeviceCode ?? string.Empty,
            ResolveDispatchStatus(persisted),
            persisted?.RecoveryStatus ?? DispatchRecoveryStatusModel.Unrecovered,
            DispatchRecoveryStatusModel.Recovered,
            recoveryConfirmed: true);

        return Send(
            request,
            RecoverySendTypeValue,
            primary => primary.SendRecoveryNotification(request),
            fallback => fallback.SendRecoveryNotification(request));
    }

    private void ReconcileRecoveredWorkOrders(
        IReadOnlyList<FaultPoolItemModel> activeFaults,
        IReadOnlyList<DispatchWorkOrderModel> persistedWorkOrders)
    {
        var activeIds = activeFaults
            .Select(item => item.FaultKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var workOrder in persistedWorkOrders.Where(item =>
                     item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered &&
                     !string.Equals(item.Responsibility.SourceTag, "Demo", StringComparison.OrdinalIgnoreCase) &&
                     !activeIds.Contains(item.WorkOrderId)))
        {
            ConfirmRecoveredAndSend(CreateNotificationRequest(workOrder), AutoRecoverySourceTag);
        }
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
                    $"{fallback.Data.StatusText} Real send failed and demo fallback was recorded.",
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
            request.PointName,
            request.FaultType,
            request.CurrentHandlingUnit,
            request.MaintainerName,
            request.MaintainerPhone,
            request.SupervisorName,
            request.SupervisorPhone,
            request.FaultDetectedAt,
            request.ScreenshotTitle ?? string.Empty,
            sendType,
            sentAt,
            request.NotificationChannelId,
            response.Data.TimelineActor,
            response.IsSuccess,
            response.Data.StatusText,
            string.IsNullOrWhiteSpace(response.Message) ? response.Data.StatusText : response.Message,
            wasRealSend,
            usedDemoFallback));
        _workOrderSnapshotService.UpdateNotificationAttempt(request, sendType, response);
    }

    private static IReadOnlyDictionary<string, DispatchNotificationHistoryEntry> CreateLatestHistoryLookup(
        IEnumerable<DispatchNotificationHistoryEntry> entries,
        string sendType)
    {
        return entries
            .Where(item => string.Equals(item.SendType, sendType, StringComparison.Ordinal))
            .GroupBy(item => item.WorkOrderId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.SentAt).First(), StringComparer.Ordinal);
    }

    private static IReadOnlyList<DispatchNotificationTimelineEntryModel> BuildTimelineEntries(
        IReadOnlyList<DispatchNotificationHistoryEntry>? historyEntries,
        IReadOnlyList<DispatchNotificationTimelineEntryModel> fallbackEntries)
    {
        if (historyEntries is null || historyEntries.Count == 0)
        {
            return fallbackEntries;
        }

        return historyEntries
            .OrderByDescending(item => item.SentAt)
            .Select(item => new DispatchNotificationTimelineEntryModel(
                item.SendType,
                item.SentAt.ToString("yyyy-MM-dd HH:mm"),
                item.ResultText,
                item.TimelineActor))
            .ToList();
    }

    private static IEnumerable<DispatchWorkOrderModel> OrderWorkOrders(IEnumerable<DispatchWorkOrderModel> workOrders)
    {
        return workOrders
            .OrderBy(item => item.WorkOrderStatus == DispatchWorkOrderStatusModel.PendingDispatch ? 0 : 1)
            .ThenBy(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered ? 0 : 1)
            .ThenByDescending(item => item.RepeatFault.RepeatCount)
            .ThenByDescending(item => ParseTime(item.RepeatFault.LatestFaultTime) ?? DateTime.MinValue);
    }

    private static DispatchNotificationRequestDto CreateNotificationRequest(DispatchWorkOrderModel workOrder)
    {
        return new DispatchNotificationRequestDto(
            workOrder.WorkOrderId,
            workOrder.PointId,
            workOrder.Responsibility.CurrentHandlingUnit,
            workOrder.Responsibility.MaintainerName,
            workOrder.Responsibility.MaintainerPhone,
            workOrder.Responsibility.SupervisorName,
            workOrder.Responsibility.SupervisorPhone,
            workOrder.PointName,
            workOrder.FaultType,
            ParseTime(workOrder.RepeatFault.LatestFaultTime) ?? DateTime.Now,
            workOrder.ScreenshotTitle,
            workOrder.Responsibility.NotificationChannelId);
    }

    private static DispatchNotificationRecordModel ClearRecoveryNotification(DispatchNotificationRecordModel notificationRecord)
    {
        return notificationRecord with
        {
            RecoveryConfirmedAt = "--",
            RecoverySourceTag = "--",
            RecoverySummary = string.Empty,
            RecoveryNotificationSentAt = "--",
            RecoveryNotificationStatus = "待发送"
        };
    }

    private static string ResolveRecoveryConfirmedAt(
        DispatchNotificationRecordModel notificationRecord,
        DispatchNotificationHistoryEntry? latestRecoveryNotification)
    {
        if (!string.IsNullOrWhiteSpace(notificationRecord.RecoveryConfirmedAt) &&
            !string.Equals(notificationRecord.RecoveryConfirmedAt, "--", StringComparison.Ordinal))
        {
            return notificationRecord.RecoveryConfirmedAt;
        }

        return latestRecoveryNotification?.IsSuccess == true
            ? latestRecoveryNotification.SentAt.ToString("yyyy-MM-dd HH:mm")
            : notificationRecord.RecoveryConfirmedAt;
    }

    private static string ResolveRecoverySource(
        DispatchNotificationRecordModel notificationRecord,
        DispatchNotificationHistoryEntry? latestRecoveryNotification)
    {
        if (!string.IsNullOrWhiteSpace(notificationRecord.RecoverySourceTag) &&
            !string.Equals(notificationRecord.RecoverySourceTag, "--", StringComparison.Ordinal))
        {
            return notificationRecord.RecoverySourceTag;
        }

        return latestRecoveryNotification?.IsSuccess == true
            ? latestRecoveryNotification.TimelineActor
            : notificationRecord.RecoverySourceTag;
    }

    private ServiceResponse<bool> ConfirmInspectionRecovery(
        DispatchWorkOrderModel? workOrder,
        DateTime recoveredAt,
        string recoverySummary)
    {
        if (_inspectionTaskService is null || workOrder is null)
        {
            return ServiceResponse<bool>.Success(true);
        }

        var looksLikeInspectionWorkOrder = string.Equals(workOrder.InspectionGroupName, "AI智能巡检中心", StringComparison.Ordinal);
        if (!looksLikeInspectionWorkOrder)
        {
            return ServiceResponse<bool>.Success(true);
        }

        if (string.IsNullOrWhiteSpace(workOrder.InspectionTaskId))
        {
            return ServiceResponse<bool>.Failure(false, $"点位 {workOrder.PointId} 缺少巡检任务关联，无法回写恢复确认。");
        }

        var response = _inspectionTaskService.ConfirmDispatchRecovery(new InspectionDispatchRecoveryWritebackRequest(
            workOrder.InspectionTaskId,
            workOrder.PointId,
            workOrder.DeviceCode,
            ResolveDispatchStatus(workOrder),
            recoveredAt.ToString("yyyy-MM-dd HH:mm"),
            recoverySummary,
            true));

        return response.IsSuccess
            ? ServiceResponse<bool>.Success(true)
            : ServiceResponse<bool>.Failure(false, response.Message);
    }

    private static string BuildRecoverySummary(
        DispatchWorkOrderModel? workOrder,
        string recoverySource,
        DateTime recoveredAt)
    {
        var action = string.Equals(recoverySource, ManualRecoverySourceTag, StringComparison.Ordinal)
            ? "派单侧人工确认恢复"
            : "派单侧自动确认恢复";
        var pointName = string.IsNullOrWhiteSpace(workOrder?.PointName) ? "当前点位" : workOrder.PointName.Trim();
        return $"{recoveredAt:yyyy-MM-dd HH:mm} {pointName}{action}。";
    }

    private static string ResolveDispatchStatus(DispatchWorkOrderModel? workOrder)
    {
        if (workOrder is null)
        {
            return InspectionDispatchValueKeys.PendingDispatch;
        }

        return workOrder.WorkOrderStatus == DispatchWorkOrderStatusModel.Dispatched
            ? InspectionDispatchValueKeys.Dispatched
            : InspectionDispatchValueKeys.PendingDispatch;
    }

    private static void WriteRecoveryLog(
        string pointId,
        string deviceCode,
        string dispatchStatus,
        DispatchRecoveryStatusModel recoveryStatusBefore,
        DispatchRecoveryStatusModel recoveryStatusAfter,
        bool recoveryConfirmed)
    {
        MapPointSourceDiagnostics.Write(
            "InspectionDispatchRecovery",
            $"pointId={pointId}, deviceCode={deviceCode}, dispatchStatus={dispatchStatus}, recoveryStatusBefore={recoveryStatusBefore}, recoveryStatusAfter={recoveryStatusAfter}, recoveryConfirmed={recoveryConfirmed}");
    }

    private static DateTime? ParseTime(string rawValue)
    {
        return DateTime.TryParse(rawValue, out var parsed) ? parsed : null;
    }
}
