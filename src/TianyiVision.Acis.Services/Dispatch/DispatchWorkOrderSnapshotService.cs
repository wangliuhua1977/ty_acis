using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed record DispatchWorkOrderSnapshot(
    IReadOnlyList<DispatchWorkOrderModel> WorkOrders);

public interface IDispatchWorkOrderSnapshotService
{
    DispatchWorkOrderSnapshot Load();

    void Save(IReadOnlyList<DispatchWorkOrderModel> workOrders);

    void Upsert(DispatchWorkOrderModel workOrder);

    DispatchInspectionWorkOrderUpsertResult UpsertInspectionWorkOrder(DispatchInspectionWorkOrderUpsertRequest request);

    void MarkRecovered(
        DispatchNotificationRequestDto request,
        DateTime recoveredAt,
        string recoverySource,
        string recoverySummary);

    void UpdateNotificationAttempt(
        DispatchNotificationRequestDto request,
        string sendType,
        ServiceResponse<DispatchNotificationResult> response);

    void UpdateResponsibility(string pointId, DispatchResponsibilityModel responsibility);
}

public sealed class FileDispatchWorkOrderSnapshotService : IDispatchWorkOrderSnapshotService
{
    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FileDispatchWorkOrderSnapshotService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public DispatchWorkOrderSnapshot Load()
    {
        var snapshot = _documentStore.LoadOrCreate(
            _paths.DispatchWorkOrderSnapshotFile,
            () => new DispatchWorkOrderSnapshot([]));
        return Normalize(snapshot.WorkOrders);
    }

    public void Save(IReadOnlyList<DispatchWorkOrderModel> workOrders)
    {
        _documentStore.Save(_paths.DispatchWorkOrderSnapshotFile, Normalize(workOrders));
    }

    public void Upsert(DispatchWorkOrderModel workOrder)
    {
        var workOrders = Load().WorkOrders.ToDictionary(item => item.WorkOrderId, StringComparer.Ordinal);
        var normalizedWorkOrder = workOrder with
        {
            FaultKey = NormalizeFaultKey(workOrder.FaultKey, workOrder.FaultType)
        };
        workOrders[normalizedWorkOrder.WorkOrderId] = normalizedWorkOrder;
        Save(workOrders.Values.ToList());
    }

    public DispatchInspectionWorkOrderUpsertResult UpsertInspectionWorkOrder(DispatchInspectionWorkOrderUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedFaultKey = NormalizeFaultKey(request.FaultKey, request.FaultType);
        var workOrders = Load().WorkOrders.ToList();
        var activeIndex = FindMatchingIndex(workOrders, request.PointId, normalizedFaultKey, DispatchRecoveryStatusModel.Unrecovered);
        var recoveredIndex = activeIndex >= 0
            ? -1
            : FindMatchingIndex(workOrders, request.PointId, normalizedFaultKey, DispatchRecoveryStatusModel.Recovered);
        var beforeSnapshot = activeIndex >= 0
            ? workOrders[activeIndex]
            : recoveredIndex >= 0
                ? workOrders[recoveredIndex]
                : null;
        var deduplicated = activeIndex >= 0;
        var reopenTriggered = activeIndex < 0 && recoveredIndex >= 0;
        var updatedWorkOrder = deduplicated
            ? MergeInspectionWorkOrder(workOrders[activeIndex], request, normalizedFaultKey)
            : CreateInspectionWorkOrder(request, normalizedFaultKey);

        if (deduplicated)
        {
            workOrders[activeIndex] = updatedWorkOrder;
        }
        else
        {
            workOrders.Add(updatedWorkOrder);
        }

        Save(workOrders);

        var result = new DispatchInspectionWorkOrderUpsertResult(
            updatedWorkOrder.WorkOrderId,
            normalizedFaultKey,
            beforeSnapshot?.WorkOrderStatus,
            updatedWorkOrder.WorkOrderStatus,
            beforeSnapshot?.RecoveryStatus,
            updatedWorkOrder.RecoveryStatus,
            reopenTriggered,
            deduplicated);

        WriteInspectionUpsertLog(request.PointId, request.DeviceCode, result);
        return result;
    }

    public void MarkRecovered(
        DispatchNotificationRequestDto request,
        DateTime recoveredAt,
        string recoverySource,
        string recoverySummary)
    {
        var workOrders = Load().WorkOrders.ToDictionary(item => item.WorkOrderId, StringComparer.Ordinal);
        var existing = workOrders.TryGetValue(request.WorkOrderId, out var current)
            ? current
            : CreatePlaceholder(request);

        var updatedWorkOrder = existing with
        {
            RecoveryStatus = DispatchRecoveryStatusModel.Recovered,
            Responsibility = existing.Responsibility with
            {
                CurrentHandlingUnit = request.CurrentHandlingUnit,
                MaintainerName = request.MaintainerName,
                MaintainerPhone = request.MaintainerPhone,
                SupervisorName = request.SupervisorName,
                SupervisorPhone = request.SupervisorPhone,
                NotificationChannelId = string.IsNullOrWhiteSpace(request.NotificationChannelId)
                    ? existing.Responsibility.NotificationChannelId
                    : request.NotificationChannelId
            },
            NotificationRecord = existing.NotificationRecord with
            {
                RecoveryConfirmedAt = recoveredAt.ToString("yyyy-MM-dd HH:mm"),
                RecoverySourceTag = string.IsNullOrWhiteSpace(recoverySource) ? "--" : recoverySource.Trim(),
                RecoverySummary = string.IsNullOrWhiteSpace(recoverySummary) ? existing.NotificationRecord.RecoverySummary : recoverySummary.Trim()
            }
        };

        workOrders[request.WorkOrderId] = updatedWorkOrder;
        Save(workOrders.Values.ToList());
    }

    public void UpdateNotificationAttempt(
        DispatchNotificationRequestDto request,
        string sendType,
        ServiceResponse<DispatchNotificationResult> response)
    {
        var workOrders = Load().WorkOrders.ToDictionary(item => item.WorkOrderId, StringComparer.Ordinal);
        var existing = workOrders.TryGetValue(request.WorkOrderId, out var current)
            ? current
            : CreatePlaceholder(request);
        var timelineEntries = existing.NotificationRecord.TimelineEntries.ToList();
        timelineEntries.Insert(
            0,
            new DispatchNotificationTimelineEntryModel(
                sendType,
                response.Data.SentAt,
                response.Data.StatusText,
                response.Data.TimelineActor));

        var updatedNotificationRecord = sendType switch
        {
            ConfigDrivenDispatchNotificationService.FaultSendTypeValue => existing.NotificationRecord with
            {
                FaultNotificationSentAt = response.Data.SentAt,
                FaultNotificationStatus = response.Data.StatusText,
                TimelineEntries = timelineEntries
            },
            ConfigDrivenDispatchNotificationService.RecoverySendTypeValue => existing.NotificationRecord with
            {
                RecoveryNotificationSentAt = response.Data.SentAt,
                RecoveryNotificationStatus = response.Data.StatusText,
                TimelineEntries = timelineEntries
            },
            _ => existing.NotificationRecord with
            {
                TimelineEntries = timelineEntries
            }
        };

        var updatedWorkOrder = existing with
        {
            WorkOrderStatus = sendType == ConfigDrivenDispatchNotificationService.FaultSendTypeValue && response.IsSuccess
                ? DispatchWorkOrderStatusModel.Dispatched
                : existing.WorkOrderStatus,
            Responsibility = existing.Responsibility with
            {
                CurrentHandlingUnit = request.CurrentHandlingUnit,
                MaintainerName = request.MaintainerName,
                MaintainerPhone = request.MaintainerPhone,
                SupervisorName = request.SupervisorName,
                SupervisorPhone = request.SupervisorPhone,
                NotificationChannelId = string.IsNullOrWhiteSpace(request.NotificationChannelId)
                    ? existing.Responsibility.NotificationChannelId
                    : request.NotificationChannelId
            },
            NotificationRecord = updatedNotificationRecord
        };

        workOrders[request.WorkOrderId] = updatedWorkOrder;
        Save(workOrders.Values.ToList());
    }

    public void UpdateResponsibility(string pointId, DispatchResponsibilityModel responsibility)
    {
        var workOrders = Load().WorkOrders.ToList();
        var hasChanges = false;

        for (var index = 0; index < workOrders.Count; index++)
        {
            if (!string.Equals(workOrders[index].PointId, pointId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            workOrders[index] = workOrders[index] with { Responsibility = responsibility };
            hasChanges = true;
        }

        if (hasChanges)
        {
            Save(workOrders);
        }
    }

    private static DispatchWorkOrderSnapshot Normalize(IEnumerable<DispatchWorkOrderModel>? workOrders)
    {
        var normalized = (workOrders ?? Array.Empty<DispatchWorkOrderModel>())
            .Where(item => !string.IsNullOrWhiteSpace(item.WorkOrderId))
            .Select(item => item with
            {
                FaultKey = NormalizeFaultKey(item.FaultKey, item.FaultType)
            })
            .GroupBy(item => item.WorkOrderId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(item => item.WorkOrderStatus == DispatchWorkOrderStatusModel.PendingDispatch ? 0 : 1)
            .ThenBy(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered ? 0 : 1)
            .ThenByDescending(item => ParseTime(item.RepeatFault.LatestFaultTime))
            .ToList();

        return new DispatchWorkOrderSnapshot(normalized);
    }

    private static int FindMatchingIndex(
        IReadOnlyList<DispatchWorkOrderModel> workOrders,
        string pointId,
        string faultKey,
        DispatchRecoveryStatusModel recoveryStatus)
    {
        var matchedIndex = -1;
        var latestDetectedAt = DateTime.MinValue;

        for (var index = 0; index < workOrders.Count; index++)
        {
            var current = workOrders[index];
            if (!string.Equals(current.PointId, pointId, StringComparison.Ordinal))
            {
                continue;
            }

            if (current.RecoveryStatus != recoveryStatus)
            {
                continue;
            }

            if (!string.Equals(ResolveFaultKey(current), faultKey, StringComparison.Ordinal))
            {
                continue;
            }

            var currentLatestDetectedAt = ParseTime(current.RepeatFault.LatestFaultTime);
            if (matchedIndex >= 0 && currentLatestDetectedAt < latestDetectedAt)
            {
                continue;
            }

            matchedIndex = index;
            latestDetectedAt = currentLatestDetectedAt;
        }

        return matchedIndex;
    }

    private static DateTime ParseTime(string rawValue)
    {
        return DateTime.TryParse(rawValue, out var parsed) ? parsed : DateTime.MinValue;
    }

    private static DispatchWorkOrderModel CreateInspectionWorkOrder(
        DispatchInspectionWorkOrderUpsertRequest request,
        string faultKey)
    {
        var detectedAtText = request.DetectedAt.ToString("yyyy-MM-dd HH:mm");
        return new DispatchWorkOrderModel(
            BuildInspectionWorkOrderId(request.PointId, faultKey, request.DetectedAt),
            request.PointId,
            request.PointName,
            request.FaultType,
            request.InspectionGroupName,
            request.MapLocationPlaceholder,
            request.ScreenshotTitle,
            request.ScreenshotSubtitle,
            request.FaultSummary,
            request.LatestInspectionConclusion,
            request.EntersDispatchPool,
            request.DetectedAt.Date == DateTime.Today,
            request.DispatchMethod,
            DispatchWorkOrderStatusModel.PendingDispatch,
            DispatchRecoveryStatusModel.Unrecovered,
            request.Responsibility,
            new DispatchNotificationRecordModel("--", "待发送", "--", "--", "--", "待发送", [], string.Empty),
            new DispatchRepeatFaultModel(detectedAtText, detectedAtText, 1),
            request.InspectionTaskId,
            request.DeviceCode)
        {
            FaultKey = faultKey
        };
    }

    private static DispatchWorkOrderModel MergeInspectionWorkOrder(
        DispatchWorkOrderModel existing,
        DispatchInspectionWorkOrderUpsertRequest request,
        string faultKey)
    {
        var latestDetectedAt = ParseTime(existing.RepeatFault.LatestFaultTime);
        var effectiveDetectedAt = latestDetectedAt > request.DetectedAt ? latestDetectedAt : request.DetectedAt;
        var repeatCount = request.DetectedAt > latestDetectedAt
            ? Math.Max(1, existing.RepeatFault.RepeatCount) + 1
            : Math.Max(1, existing.RepeatFault.RepeatCount);

        return existing with
        {
            PointName = request.PointName,
            FaultType = request.FaultType,
            InspectionGroupName = request.InspectionGroupName,
            MapLocationPlaceholder = request.MapLocationPlaceholder,
            ScreenshotTitle = request.ScreenshotTitle,
            ScreenshotSubtitle = request.ScreenshotSubtitle,
            FaultSummary = request.FaultSummary,
            LatestInspectionConclusion = request.LatestInspectionConclusion,
            EntersDispatchPool = request.EntersDispatchPool,
            IsTodayNew = effectiveDetectedAt.Date == DateTime.Today,
            DispatchMethod = request.DispatchMethod,
            WorkOrderStatus = existing.WorkOrderStatus == DispatchWorkOrderStatusModel.Dispatched
                ? DispatchWorkOrderStatusModel.Dispatched
                : DispatchWorkOrderStatusModel.PendingDispatch,
            RecoveryStatus = DispatchRecoveryStatusModel.Unrecovered,
            Responsibility = request.Responsibility,
            RepeatFault = existing.RepeatFault with
            {
                LatestFaultTime = effectiveDetectedAt.ToString("yyyy-MM-dd HH:mm"),
                RepeatCount = repeatCount
            },
            InspectionTaskId = request.InspectionTaskId,
            DeviceCode = request.DeviceCode,
            FaultKey = faultKey
        };
    }

    private static DispatchWorkOrderModel CreatePlaceholder(DispatchNotificationRequestDto request)
    {
        var detectedAt = request.FaultDetectedAt.ToString("yyyy-MM-dd HH:mm");
        return new DispatchWorkOrderModel(
            request.WorkOrderId,
            request.PointId,
            request.PointName,
            request.FaultType,
            "Local Snapshot",
            request.CurrentHandlingUnit,
            string.IsNullOrWhiteSpace(request.ScreenshotTitle) ? "Snapshot pending" : request.ScreenshotTitle.Trim(),
            "Local snapshot record",
            $"{request.FaultType} local snapshot record",
            "Fault",
            true,
            request.FaultDetectedAt.Date == DateTime.Today,
            DispatchMethodModel.Manual,
            DispatchWorkOrderStatusModel.PendingDispatch,
            DispatchRecoveryStatusModel.Unrecovered,
            new DispatchResponsibilityModel(
                request.CurrentHandlingUnit,
                request.MaintainerName,
                request.MaintainerPhone,
                request.SupervisorName,
                request.SupervisorPhone,
                string.IsNullOrWhiteSpace(request.NotificationChannelId) ? "default" : request.NotificationChannelId,
                "LocalSnapshot"),
            new DispatchNotificationRecordModel("--", "待发送", "--", "--", "--", "待发送", [], string.Empty),
            new DispatchRepeatFaultModel(detectedAt, detectedAt, 1))
        {
            FaultKey = NormalizeFaultKey(request.FaultType, request.FaultType)
        };
    }

    private static string BuildInspectionWorkOrderId(string pointId, string faultKey, DateTime detectedAt)
    {
        return $"inspection:{pointId}:{faultKey}:{detectedAt:yyyyMMddHHmmssfff}";
    }

    private static string ResolveFaultKey(DispatchWorkOrderModel workOrder)
    {
        return NormalizeFaultKey(workOrder.FaultKey, workOrder.FaultType);
    }

    private static string NormalizeFaultKey(string? faultKey, string? fallbackFaultType)
    {
        var candidate = !string.IsNullOrWhiteSpace(faultKey)
            ? faultKey.Trim()
            : !string.IsNullOrWhiteSpace(fallbackFaultType)
                ? fallbackFaultType.Trim()
                : "inspection_abnormal";
        return candidate.Replace(" ", "_", StringComparison.Ordinal);
    }

    private static void WriteInspectionUpsertLog(
        string pointId,
        string deviceCode,
        DispatchInspectionWorkOrderUpsertResult result)
    {
        MapPointSourceDiagnostics.Write(
            "InspectionDispatchBridge",
            $"pointId={pointId}, deviceCode={deviceCode}, faultKey={result.FaultKey}, dispatchStatusBefore={ResolveDispatchStatusText(result.DispatchStatusBefore)}, dispatchStatusAfter={ResolveDispatchStatusText(result.DispatchStatusAfter)}, recoveryStatusBefore={ResolveRecoveryStatusText(result.RecoveryStatusBefore)}, recoveryStatusAfter={ResolveRecoveryStatusText(result.RecoveryStatusAfter)}, reopenTriggered={result.ReopenTriggered}, deduplicated={result.Deduplicated}");
    }

    private static string ResolveDispatchStatusText(DispatchWorkOrderStatusModel? status)
    {
        return status?.ToString() ?? "None";
    }

    private static string ResolveRecoveryStatusText(DispatchRecoveryStatusModel? status)
    {
        return status?.ToString() ?? "None";
    }
}
