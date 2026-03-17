using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed record DispatchWorkOrderSnapshot(
    IReadOnlyList<DispatchWorkOrderModel> WorkOrders);

public interface IDispatchWorkOrderSnapshotService
{
    DispatchWorkOrderSnapshot Load();

    void Save(IReadOnlyList<DispatchWorkOrderModel> workOrders);

    void Upsert(DispatchWorkOrderModel workOrder);

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
        workOrders[workOrder.WorkOrderId] = workOrder;
        Save(workOrders.Values.ToList());
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
            .GroupBy(item => item.WorkOrderId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(item => item.WorkOrderStatus == DispatchWorkOrderStatusModel.PendingDispatch ? 0 : 1)
            .ThenBy(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered ? 0 : 1)
            .ThenByDescending(item => ParseTime(item.RepeatFault.LatestFaultTime))
            .ToList();

        return new DispatchWorkOrderSnapshot(normalized);
    }

    private static DateTime ParseTime(string rawValue)
    {
        return DateTime.TryParse(rawValue, out var parsed) ? parsed : DateTime.MinValue;
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
            new DispatchRepeatFaultModel(detectedAt, detectedAt, 1));
    }
}
