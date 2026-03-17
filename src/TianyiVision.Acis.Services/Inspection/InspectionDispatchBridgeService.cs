using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Dispatch;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class InspectionDispatchBridgeService : IInspectionDispatchBridgeService
{
    private const string InspectionGroupName = "AI智能巡检中心";

    private readonly IDispatchWorkOrderSnapshotService _workOrderSnapshotService;
    private readonly IDispatchResponsibilityService _dispatchResponsibilityService;

    public InspectionDispatchBridgeService(
        IDispatchWorkOrderSnapshotService workOrderSnapshotService,
        IDispatchResponsibilityService dispatchResponsibilityService)
    {
        _workOrderSnapshotService = workOrderSnapshotService;
        _dispatchResponsibilityService = dispatchResponsibilityService;
    }

    public InspectionDispatchBridgeBatchResult Bridge(InspectionDispatchBridgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Task);

        var workOrders = _workOrderSnapshotService.Load().WorkOrders;
        var acceptedResults = new List<InspectionDispatchBridgePointResult>();
        var requestedPointIds = NormalizePointIds(request);

        foreach (var pointId in requestedPointIds)
        {
            var pointExecution = request.Task.FindPointExecution(pointId);
            var candidateEntry = request.Task.FindDispatchPoolEntry(pointId);
            var deviceCode = pointExecution?.DeviceCode ?? candidateEntry?.DeviceCode ?? string.Empty;

            if (candidateEntry is null)
            {
                acceptedResults.Add(WriteRejectedResult(pointId, deviceCode));
                continue;
            }

            var faultType = ResolveFaultType(candidateEntry);
            var faultKey = ResolveFaultKey(candidateEntry, faultType);
            var existing = FindLatestWorkOrder(workOrders, candidateEntry.PointId, faultKey);
            var responsibility = ResolveResponsibility(
                pointExecution,
                candidateEntry.PointId,
                candidateEntry.PointName,
                existing);
            var detectedAt = ResolveDetectedAt(request.Task);
            var upsertResult = _workOrderSnapshotService.UpsertInspectionWorkOrder(
                new DispatchInspectionWorkOrderUpsertRequest(
                    candidateEntry.PointId,
                    candidateEntry.DeviceCode,
                    candidateEntry.PointName,
                    faultType,
                    faultKey,
                    InspectionGroupName,
                    ResolveCurrentHandlingUnit(pointExecution, existing),
                    BuildScreenshotTitle(request.Source),
                    BuildScreenshotSubtitle(request.Source),
                    ResolveFaultSummary(candidateEntry, pointExecution),
                    BuildInspectionConclusion(request.Source),
                    true,
                    DispatchMethodModel.Manual,
                    responsibility,
                    detectedAt,
                    request.Task.TaskId));

            acceptedResults.Add(new InspectionDispatchBridgePointResult(
                candidateEntry.PointId,
                candidateEntry.DeviceCode,
                upsertResult.FaultKey,
                true,
                true,
                upsertResult.Deduplicated,
                upsertResult.ReopenTriggered,
                MapDispatchStatus(upsertResult.DispatchStatusBefore),
                MapDispatchStatus(upsertResult.DispatchStatusAfter),
                MapRecoveryStatus(upsertResult.RecoveryStatusBefore),
                MapRecoveryStatus(upsertResult.RecoveryStatusAfter)));
        }

        return new InspectionDispatchBridgeBatchResult(acceptedResults);
    }

    private static IReadOnlyList<string> NormalizePointIds(InspectionDispatchBridgeRequest request)
    {
        var requested = (request.PointIds ?? Array.Empty<string>())
            .Where(pointId => !string.IsNullOrWhiteSpace(pointId))
            .Select(pointId => pointId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (requested.Count > 0)
        {
            return requested;
        }

        return request.Task.AbnormalFlow.DispatchPoolCandidateEntries
            .Select(entry => entry.PointId)
            .Where(pointId => !string.IsNullOrWhiteSpace(pointId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private DispatchResponsibilityModel ResolveResponsibility(
        InspectionTaskPointExecutionModel? pointExecution,
        string pointId,
        string pointName,
        DispatchWorkOrderModel? existing)
    {
        var currentHandlingUnit = ResolveCurrentHandlingUnit(pointExecution, existing);
        var response = _dispatchResponsibilityService.Resolve(new DispatchResponsibilityQueryDto(
            pointId,
            pointName,
            currentHandlingUnit));
        if (response.IsSuccess)
        {
            return response.Data;
        }

        if (existing is not null)
        {
            return existing.Responsibility;
        }

        return new DispatchResponsibilityModel(
            currentHandlingUnit,
            "待分配维护人",
            "--",
            "待分配负责人",
            "--",
            "default",
            "InspectionDispatchBridge");
    }

    private static DispatchWorkOrderModel? FindLatestWorkOrder(
        IReadOnlyList<DispatchWorkOrderModel> workOrders,
        string pointId,
        string faultKey)
    {
        return workOrders
            .Where(workOrder =>
                string.Equals(workOrder.PointId, pointId, StringComparison.Ordinal)
                && string.Equals(ResolveFaultKey(workOrder), faultKey, StringComparison.Ordinal))
            .OrderByDescending(workOrder => ParseOrDefault(workOrder.RepeatFault.LatestFaultTime, DateTime.MinValue))
            .FirstOrDefault();
    }

    private static string ResolveCurrentHandlingUnit(
        InspectionTaskPointExecutionModel? pointExecution,
        DispatchWorkOrderModel? existing)
    {
        if (!string.IsNullOrWhiteSpace(pointExecution?.CurrentHandlingUnit))
        {
            return pointExecution.CurrentHandlingUnit.Trim();
        }

        if (!string.IsNullOrWhiteSpace(pointExecution?.UnitName))
        {
            return pointExecution.UnitName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(existing?.Responsibility.CurrentHandlingUnit))
        {
            return existing.Responsibility.CurrentHandlingUnit.Trim();
        }

        return "待分派处理单位";
    }

    private static string ResolveFaultType(InspectionAbnormalFlowEntryModel candidateEntry)
    {
        if (!string.IsNullOrWhiteSpace(candidateEntry.PrimaryFaultType))
        {
            return candidateEntry.PrimaryFaultType.Trim();
        }

        var tags = (candidateEntry.AbnormalTags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return tags.Count > 0 ? string.Join("|", tags) : "inspection_abnormal";
    }

    private static string ResolveFaultKey(
        InspectionAbnormalFlowEntryModel candidateEntry,
        string faultType)
    {
        if (!string.IsNullOrWhiteSpace(candidateEntry.FaultKey))
        {
            return NormalizeFaultKey(candidateEntry.FaultKey);
        }

        if (!string.IsNullOrWhiteSpace(candidateEntry.PrimaryFaultType))
        {
            return NormalizeFaultKey(candidateEntry.PrimaryFaultType);
        }

        var tags = (candidateEntry.AbnormalTags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return tags.Count > 0
            ? NormalizeFaultKey(string.Join("|", tags))
            : NormalizeFaultKey(faultType);
    }

    private static string ResolveFaultKey(DispatchWorkOrderModel workOrder)
    {
        return NormalizeFaultKey(string.IsNullOrWhiteSpace(workOrder.FaultKey) ? workOrder.FaultType : workOrder.FaultKey);
    }

    private static string NormalizeFaultKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "inspection_abnormal"
            : value.Trim().Replace(" ", "_", StringComparison.Ordinal);
    }

    private static string ResolveFaultSummary(
        InspectionAbnormalFlowEntryModel candidateEntry,
        InspectionTaskPointExecutionModel? pointExecution)
    {
        if (!string.IsNullOrWhiteSpace(candidateEntry.AiAnalysisSummary))
        {
            return candidateEntry.AiAnalysisSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(pointExecution?.FailureReason))
        {
            return pointExecution.FailureReason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(pointExecution?.ExecutionSummary))
        {
            return pointExecution.ExecutionSummary.Trim();
        }

        return "AI智能巡检中心已识别异常，待派单处置。";
    }

    private static string BuildScreenshotTitle(InspectionDispatchBridgeSource source)
    {
        return source == InspectionDispatchBridgeSource.ReviewWallConfirmed
            ? "AI智能巡检中心复核截图"
            : "AI智能巡检中心异常截图";
    }

    private static string BuildScreenshotSubtitle(InspectionDispatchBridgeSource source)
    {
        return source == InspectionDispatchBridgeSource.ReviewWallConfirmed
            ? "复核确认后进入待派单快照"
            : "AI巡检结果直入待派单";
    }

    private static string BuildInspectionConclusion(InspectionDispatchBridgeSource source)
    {
        return source == InspectionDispatchBridgeSource.ReviewWallConfirmed
            ? "AI智能巡检中心复核确认后已桥接到待派单快照。"
            : "AI智能巡检中心异常已桥接到待派单快照。";
    }

    private static DateTime ResolveDetectedAt(InspectionTaskRecordModel task)
    {
        return task.FinishedAt ?? task.StartedAt ?? task.CreatedAt;
    }

    private static DateTime ParseOrDefault(string value, DateTime fallback)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string MapDispatchStatus(DispatchWorkOrderStatusModel? status)
    {
        return status switch
        {
            DispatchWorkOrderStatusModel.PendingDispatch => InspectionDispatchValueKeys.PendingDispatch,
            DispatchWorkOrderStatusModel.Dispatched => InspectionDispatchValueKeys.Dispatched,
            _ => InspectionDispatchValueKeys.None
        };
    }

    private static string MapRecoveryStatus(DispatchRecoveryStatusModel? status)
    {
        return status switch
        {
            DispatchRecoveryStatusModel.Unrecovered => InspectionRecoveryValueKeys.Unrecovered,
            DispatchRecoveryStatusModel.Recovered => InspectionRecoveryValueKeys.Recovered,
            _ => InspectionRecoveryValueKeys.None
        };
    }

    private static InspectionDispatchBridgePointResult WriteRejectedResult(string pointId, string deviceCode)
    {
        MapPointSourceDiagnostics.Write(
            "InspectionDispatchBridge",
            $"pointId={pointId}, deviceCode={deviceCode}, faultKey=none, dispatchStatusBefore={InspectionDispatchValueKeys.None}, dispatchStatusAfter={InspectionDispatchValueKeys.None}, recoveryStatusBefore={InspectionRecoveryValueKeys.None}, recoveryStatusAfter={InspectionRecoveryValueKeys.None}, reopenTriggered=false, deduplicated=false");
        return new InspectionDispatchBridgePointResult(
            pointId,
            deviceCode,
            string.Empty,
            false,
            false,
            false,
            false,
            InspectionDispatchValueKeys.None,
            InspectionDispatchValueKeys.None,
            InspectionRecoveryValueKeys.None,
            InspectionRecoveryValueKeys.None);
    }
}
