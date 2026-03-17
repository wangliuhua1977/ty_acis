using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class FileInspectionTaskHistoryStore : IInspectionTaskHistoryStore
{
    private const string DefaultScopePlanName = "当前组默认范围";

    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FileInspectionTaskHistoryStore(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public IReadOnlyList<InspectionTaskRecordModel> Load()
    {
        var snapshot = _documentStore.LoadOrCreate(_paths.InspectionTaskHistoryFile, () => new InspectionTaskHistoryDocument([]));
        return NormalizeTasks(snapshot.Tasks ?? Array.Empty<InspectionTaskRecordModel>());
    }

    public void Save(IReadOnlyList<InspectionTaskRecordModel> tasks)
    {
        var normalizedTasks = NormalizeTasks(tasks ?? Array.Empty<InspectionTaskRecordModel>())
            .Take(40)
            .ToList();

        _documentStore.Save(
            _paths.InspectionTaskHistoryFile,
            new InspectionTaskHistoryDocument(normalizedTasks));
    }

    private static IReadOnlyList<InspectionTaskRecordModel> NormalizeTasks(IEnumerable<InspectionTaskRecordModel> tasks)
    {
        return tasks
            .Where(task => task is not null)
            .Select(NormalizeTask)
            .OrderByDescending(task => task.CreatedAt)
            .GroupBy(task => task.TaskId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static InspectionTaskRecordModel NormalizeTask(InspectionTaskRecordModel task)
    {
        var pointExecutions = (task.PointExecutions ?? Array.Empty<InspectionTaskPointExecutionModel>())
            .Where(point => point is not null)
            .Select((point, index) => NormalizePointExecution(point, index + 1))
            .OrderBy(point => point.Sequence)
            .ToList();

        var successCount = pointExecutions.Count == 0
            ? Math.Max(0, task.SuccessCount)
            : pointExecutions.Count(point => point.Status == InspectionPointExecutionStatusModel.Succeeded);
        var failureCount = pointExecutions.Count == 0
            ? Math.Max(0, task.FailureCount)
            : pointExecutions.Count(point => point.Status == InspectionPointExecutionStatusModel.Failed);
        var skippedCount = pointExecutions.Count == 0
            ? Math.Max(0, task.SkippedCount)
            : pointExecutions.Count(point => point.Status == InspectionPointExecutionStatusModel.Skipped);
        var totalPointCount = Math.Max(Math.Max(0, task.TotalPointCount), pointExecutions.Count);
        var currentPoint = ResolveCurrentPoint(task, pointExecutions);
        var summary = string.IsNullOrWhiteSpace(task.Summary)
            ? BuildFallbackSummary(task.Status, totalPointCount, successCount, failureCount, skippedCount, task.ScopePlanName, currentPoint.PointName)
            : task.Summary.Trim();

        return task with
        {
            ScopePlanName = string.IsNullOrWhiteSpace(task.ScopePlanName) ? DefaultScopePlanName : task.ScopePlanName.Trim(),
            TotalPointCount = totalPointCount,
            SuccessCount = successCount,
            FailureCount = failureCount,
            SkippedCount = skippedCount,
            CurrentPointId = currentPoint.PointId,
            CurrentPointName = currentPoint.PointName,
            Summary = summary,
            AbnormalFlow = InspectionTaskModelExtensions.BuildAbnormalFlowSnapshot(pointExecutions),
            PointExecutions = pointExecutions
        };
    }

    private static InspectionTaskPointExecutionModel NormalizePointExecution(InspectionTaskPointExecutionModel point, int fallbackSequence)
    {
        var evidenceItems = (point.EvidenceItems ?? Array.Empty<InspectionPointEvidenceMetadataModel>())
            .Where(item => item is not null)
            .OrderBy(item => item.ScreenshotTime)
            .ToList();

        return point with
        {
            DeviceCode = string.IsNullOrWhiteSpace(point.DeviceCode) ? point.PointId : point.DeviceCode.Trim(),
            PointName = string.IsNullOrWhiteSpace(point.PointName) ? point.PointId : point.PointName.Trim(),
            Sequence = point.Sequence > 0 ? point.Sequence : fallbackSequence,
            UnitName = point.UnitName?.Trim() ?? string.Empty,
            CurrentHandlingUnit = point.CurrentHandlingUnit?.Trim() ?? string.Empty,
            SkipReason = point.SkipReason?.Trim() ?? string.Empty,
            FailureReason = point.FailureReason?.Trim() ?? string.Empty,
            PolicySnapshotSummary = point.PolicySnapshotSummary?.Trim() ?? string.Empty,
            ScreenshotPlannedCount = Math.Max(0, point.ScreenshotPlannedCount),
            ScreenshotIntervalSeconds = Math.Max(0, point.ScreenshotIntervalSeconds),
            ScreenshotSuccessCount = Math.Max(0, point.ScreenshotSuccessCount),
            EvidenceCaptureState = string.IsNullOrWhiteSpace(point.EvidenceCaptureState)
                ? InspectionEvidenceValueKeys.CaptureStateNone
                : point.EvidenceCaptureState.Trim(),
            EvidenceSummary = point.EvidenceSummary?.Trim() ?? string.Empty,
            EvidenceRetentionMode = point.EvidenceRetentionMode?.Trim() ?? string.Empty,
            EvidenceRetentionDays = Math.Max(0, point.EvidenceRetentionDays),
            AiAnalysisStatus = string.IsNullOrWhiteSpace(point.AiAnalysisStatus)
                ? InspectionEvidenceValueKeys.AiAnalysisReserved
                : point.AiAnalysisStatus.Trim(),
            AiAnalysisSummary = point.AiAnalysisSummary?.Trim() ?? string.Empty,
            IsAiAbnormalDetected = point.IsAiAbnormalDetected,
            AiAbnormalTags = (point.AiAbnormalTags ?? Array.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            AiConfidence = Math.Clamp(point.AiConfidence, 0d, 1d),
            AiSuggestedAction = point.AiSuggestedAction?.Trim() ?? string.Empty,
            PrimaryFaultType = point.PrimaryFaultType?.Trim() ?? string.Empty,
            RouteToReviewWallReserved = point.RouteToReviewWallReserved,
            RouteToDispatchPoolReserved = point.RouteToDispatchPoolReserved,
            ManualReviewRequiredReserved = point.ManualReviewRequiredReserved,
            DispatchCandidateAccepted = point.DispatchCandidateAccepted,
            DispatchUpserted = point.DispatchUpserted,
            DispatchDeduplicated = point.DispatchDeduplicated,
            DispatchStatus = string.IsNullOrWhiteSpace(point.DispatchStatus)
                ? InspectionDispatchValueKeys.None
                : point.DispatchStatus.Trim(),
            EvidenceItems = evidenceItems
        };
    }

    private static (string? PointId, string PointName) ResolveCurrentPoint(
        InspectionTaskRecordModel task,
        IReadOnlyList<InspectionTaskPointExecutionModel> pointExecutions)
    {
        if (task.Status is not InspectionTaskStatusModel.Pending and not InspectionTaskStatusModel.Running)
        {
            return (null, "--");
        }

        var currentPoint = task.FindPointExecution(task.CurrentPointId)
            ?? pointExecutions.FirstOrDefault(point => point.Status == InspectionPointExecutionStatusModel.Running)
            ?? pointExecutions.FirstOrDefault(point => point.Status == InspectionPointExecutionStatusModel.Pending);

        if (currentPoint is null)
        {
            return (null, "--");
        }

        return (currentPoint.PointId, currentPoint.PointName);
    }

    private static string BuildFallbackSummary(
        InspectionTaskStatusModel status,
        int total,
        int success,
        int failure,
        int skipped,
        string scopePlanName,
        string currentPointName)
    {
        var effectiveScopePlanName = string.IsNullOrWhiteSpace(scopePlanName)
            ? DefaultScopePlanName
            : scopePlanName.Trim();

        return status switch
        {
            InspectionTaskStatusModel.Pending => $"任务已生成，命中范围“{effectiveScopePlanName}”，共 {total} 个点位待执行。",
            InspectionTaskStatusModel.Running => $"任务执行中，已完成 {success + failure + skipped} / {total} 个点位，当前推进“{currentPointName}”。",
            InspectionTaskStatusModel.Completed => $"任务已完成，共 {total} 个点位，成功 {success} 个。",
            InspectionTaskStatusModel.PartialFailure => $"任务已完成，共 {total} 个点位，成功 {success} 个，失败 {failure} 个，跳过 {skipped} 个。",
            InspectionTaskStatusModel.Failed => $"任务执行失败，共 {total} 个点位，失败 {failure} 个，跳过 {skipped} 个。",
            InspectionTaskStatusModel.Cancelled => "任务已取消，本轮未进入点位执行。",
            _ => "任务状态待更新。"
        };
    }

    private sealed record InspectionTaskHistoryDocument(IReadOnlyList<InspectionTaskRecordModel> Tasks);
}
