using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Dispatch;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class InspectionDispatchBridgeService : IInspectionDispatchBridgeService
{
    private const string PendingNotificationStatus = "待发送";

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

        var snapshot = _workOrderSnapshotService.Load();
        var workOrders = snapshot.WorkOrders.ToList();
        var acceptedResults = new List<InspectionDispatchBridgePointResult>();
        var requestedPointIds = NormalizePointIds(request);

        foreach (var pointId in requestedPointIds)
        {
            var pointExecution = request.Task.FindPointExecution(pointId);
            var candidateEntry = request.Task.FindDispatchPoolEntry(pointId);
            var deviceCode = pointExecution?.DeviceCode ?? candidateEntry?.DeviceCode ?? string.Empty;

            if (candidateEntry is null)
            {
                acceptedResults.Add(WriteBridgeLog(
                    pointId,
                    deviceCode,
                    dispatchCandidateAccepted: false,
                    dispatchUpserted: false,
                    dispatchDeduplicated: false,
                    dispatchStatus: InspectionDispatchValueKeys.None));
                continue;
            }

            var faultKey = BuildFaultKey(candidateEntry);
            var existingIndex = FindExistingIndex(workOrders, candidateEntry.PointId, faultKey);
            var detectedAt = ResolveDetectedAt(request.Task);
            var responsibility = ResolveResponsibility(pointExecution, candidateEntry.PointId, candidateEntry.PointName, existingIndex >= 0 ? workOrders[existingIndex] : null);
            var candidateWorkOrder = existingIndex >= 0
                ? MergeExisting(
                    workOrders[existingIndex],
                    candidateEntry,
                    pointExecution,
                    responsibility,
                    detectedAt,
                    request.Source)
                : CreateNew(
                    request.Task,
                    candidateEntry,
                    pointExecution,
                    responsibility,
                    detectedAt,
                    faultKey,
                    request.Source);

            if (existingIndex >= 0)
            {
                workOrders[existingIndex] = candidateWorkOrder;
            }
            else
            {
                workOrders.Add(candidateWorkOrder);
            }

            acceptedResults.Add(WriteBridgeLog(
                candidateEntry.PointId,
                candidateEntry.DeviceCode,
                dispatchCandidateAccepted: true,
                dispatchUpserted: true,
                dispatchDeduplicated: existingIndex >= 0,
                dispatchStatus: InspectionDispatchValueKeys.PendingDispatch));
        }

        if (acceptedResults.Any(item => item.DispatchUpserted))
        {
            _workOrderSnapshotService.Save(workOrders);
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

    private static int FindExistingIndex(
        IReadOnlyList<DispatchWorkOrderModel> workOrders,
        string pointId,
        string faultKey)
    {
        for (var index = 0; index < workOrders.Count; index++)
        {
            var current = workOrders[index];
            if (!string.Equals(current.PointId, pointId, StringComparison.Ordinal))
            {
                continue;
            }

            if (current.RecoveryStatus != DispatchRecoveryStatusModel.Unrecovered)
            {
                continue;
            }

            if (string.Equals(BuildFaultKey(current.FaultType), faultKey, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
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

    private static DispatchWorkOrderModel MergeExisting(
        DispatchWorkOrderModel existing,
        InspectionAbnormalFlowEntryModel candidateEntry,
        InspectionTaskPointExecutionModel? pointExecution,
        DispatchResponsibilityModel responsibility,
        DateTime detectedAt,
        InspectionDispatchBridgeSource source)
    {
        var latestDetectedAt = ParseOrDefault(existing.RepeatFault.LatestFaultTime, detectedAt);
        var effectiveDetectedAt = latestDetectedAt > detectedAt ? latestDetectedAt : detectedAt;

        return existing with
        {
            PointName = candidateEntry.PointName,
            FaultType = ResolveFaultType(candidateEntry),
            InspectionGroupName = "AI智能巡检中心",
            MapLocationPlaceholder = ResolveCurrentHandlingUnit(pointExecution, existing),
            ScreenshotTitle = BuildScreenshotTitle(source),
            ScreenshotSubtitle = BuildScreenshotSubtitle(source),
            FaultSummary = ResolveFaultSummary(candidateEntry, pointExecution),
            LatestInspectionConclusion = BuildInspectionConclusion(source),
            EntersDispatchPool = true,
            IsTodayNew = effectiveDetectedAt.Date == DateTime.Today,
            WorkOrderStatus = existing.WorkOrderStatus == DispatchWorkOrderStatusModel.Dispatched
                ? DispatchWorkOrderStatusModel.Dispatched
                : DispatchWorkOrderStatusModel.PendingDispatch,
            RecoveryStatus = DispatchRecoveryStatusModel.Unrecovered,
            Responsibility = responsibility,
            RepeatFault = existing.RepeatFault with
            {
                LatestFaultTime = effectiveDetectedAt.ToString("yyyy-MM-dd HH:mm")
            }
        };
    }

    private static DispatchWorkOrderModel CreateNew(
        InspectionTaskRecordModel task,
        InspectionAbnormalFlowEntryModel candidateEntry,
        InspectionTaskPointExecutionModel? pointExecution,
        DispatchResponsibilityModel responsibility,
        DateTime detectedAt,
        string faultKey,
        InspectionDispatchBridgeSource source)
    {
        var detectedAtText = detectedAt.ToString("yyyy-MM-dd HH:mm");

        return new DispatchWorkOrderModel(
            BuildWorkOrderId(candidateEntry.PointId, faultKey),
            candidateEntry.PointId,
            candidateEntry.PointName,
            ResolveFaultType(candidateEntry),
            "AI智能巡检中心",
            ResolveCurrentHandlingUnit(pointExecution, null),
            BuildScreenshotTitle(source),
            BuildScreenshotSubtitle(source),
            ResolveFaultSummary(candidateEntry, pointExecution),
            BuildInspectionConclusion(source),
            true,
            detectedAt.Date == DateTime.Today,
            DispatchMethodModel.Manual,
            DispatchWorkOrderStatusModel.PendingDispatch,
            DispatchRecoveryStatusModel.Unrecovered,
            responsibility,
            new DispatchNotificationRecordModel("--", PendingNotificationStatus, "--", "--", "--", PendingNotificationStatus, []),
            new DispatchRepeatFaultModel(detectedAtText, detectedAtText, 1));
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

        return "AI智能巡检中心已识别异常，待派单处理。";
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
            ? "复核墙确认后进入待派单"
            : "AI巡检结果直入待派单";
    }

    private static string BuildInspectionConclusion(InspectionDispatchBridgeSource source)
    {
        return source == InspectionDispatchBridgeSource.ReviewWallConfirmed
            ? "AI智能巡检中心复核确认后已桥接到待派单快照。"
            : "AI智能巡检中心异常已桥接到待派单快照。";
    }

    private static string BuildWorkOrderId(string pointId, string faultKey)
    {
        return $"inspection:{pointId}:{faultKey}";
    }

    private static string BuildFaultKey(InspectionAbnormalFlowEntryModel candidateEntry)
    {
        return BuildFaultKey(ResolveFaultType(candidateEntry));
    }

    private static string BuildFaultKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "inspection_abnormal"
            : value.Trim().Replace(" ", "_", StringComparison.Ordinal);
    }

    private static DateTime ResolveDetectedAt(InspectionTaskRecordModel task)
    {
        return task.FinishedAt ?? task.StartedAt ?? task.CreatedAt;
    }

    private static DateTime ParseOrDefault(string value, DateTime fallback)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static InspectionDispatchBridgePointResult WriteBridgeLog(
        string pointId,
        string deviceCode,
        bool dispatchCandidateAccepted,
        bool dispatchUpserted,
        bool dispatchDeduplicated,
        string dispatchStatus)
    {
        var result = new InspectionDispatchBridgePointResult(
            pointId,
            deviceCode,
            dispatchCandidateAccepted,
            dispatchUpserted,
            dispatchDeduplicated,
            dispatchStatus);
        MapPointSourceDiagnostics.Write(
            "InspectionDispatchBridge",
            $"pointId={pointId}, deviceCode={deviceCode}, dispatchCandidateAccepted={dispatchCandidateAccepted}, dispatchUpserted={dispatchUpserted}, dispatchDeduplicated={dispatchDeduplicated}, dispatchStatus={dispatchStatus}");
        return result;
    }
}
