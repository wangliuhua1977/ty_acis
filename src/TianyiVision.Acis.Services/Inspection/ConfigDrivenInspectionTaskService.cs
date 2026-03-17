using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Diagnostics;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class ConfigDrivenInspectionTaskService : IInspectionTaskService
{
    private const string DefaultGroupId = "inspection-live-group";
    private const string DefaultGroupName = "实时点位巡检组";
    private const string DefaultScopePlanName = "当前组默认范围";
    private const int TaskHistoryLimit = 20;

    private static readonly PointStageLayoutPreset InspectionStagePreset = new(
        90,
        60,
        980,
        330,
        860,
        340,
        3,
        110,
        100);

    private readonly object _syncRoot = new();
    private readonly IPointWorkspaceService _pointWorkspaceService;
    private readonly IInspectionSettingsService _inspectionSettingsService;
    private readonly IInspectionTaskHistoryStore _taskHistoryStore;
    private readonly IInspectionPointCheckExecutor _pointCheckExecutor;
    private readonly IInspectionEvidenceAiAnalysisService _inspectionEvidenceAiAnalysisService;
    private readonly List<InspectionTaskRecordModel> _taskHistory;
    private readonly Dictionary<string, InspectionTaskRecordModel> _latestTaskByGroupId;

    public ConfigDrivenInspectionTaskService(
        IPointWorkspaceService pointWorkspaceService,
        IInspectionSettingsService inspectionSettingsService,
        IInspectionTaskHistoryStore taskHistoryStore,
        IInspectionPointCheckExecutor pointCheckExecutor,
        IInspectionEvidenceAiAnalysisService inspectionEvidenceAiAnalysisService)
    {
        _pointWorkspaceService = pointWorkspaceService;
        _inspectionSettingsService = inspectionSettingsService;
        _taskHistoryStore = taskHistoryStore;
        _pointCheckExecutor = pointCheckExecutor;
        _inspectionEvidenceAiAnalysisService = inspectionEvidenceAiAnalysisService;

        _taskHistory = _taskHistoryStore.Load()
            .OrderByDescending(task => task.CreatedAt)
            .Take(TaskHistoryLimit)
            .ToList();
        _latestTaskByGroupId = _taskHistory
            .GroupBy(task => task.GroupId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public event EventHandler<InspectionTaskBoardChangedEventArgs>? TaskBoardChanged;

    public ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace()
    {
        var inspectionSettings = _inspectionSettingsService.Load();
        var pointCollectionResponse = _pointWorkspaceService.GetPointCollection();
        var groupName = BuildGroupName();
        var taskBoard = BuildTaskBoard(DefaultGroupId, groupName);

        if (!pointCollectionResponse.IsSuccess || pointCollectionResponse.Data.Count == 0)
        {
            MapPointSourceDiagnostics.WriteLines("InspectionTask", [
                "inspection workspace = pending",
                $"reason = {NormalizeReason(pointCollectionResponse.Message, "点位工作区暂未返回可巡检点位。")}",
                $"taskBoard = {DescribeBoard(taskBoard)}"
            ]);

            return ServiceResponse<InspectionWorkspaceSnapshot>.Success(
                CreatePendingWorkspace(groupName, taskBoard, inspectionSettings),
                pointCollectionResponse.Message);
        }

        var points = pointCollectionResponse.Data.ToList();
        var stagePlacements = PointStageProjection.Project(points, InspectionStagePreset);

        var inspectionPoints = points
            .Select((point, index) =>
            {
                var taskPoint = taskBoard.CurrentTask?.PointExecutions
                    .FirstOrDefault(candidate => string.Equals(candidate.PointId, point.PointId, StringComparison.Ordinal));
                return CreatePoint(point, stagePlacements[point.PointId], index, taskPoint);
            })
            .ToList();

        var recentFaults = points
            .Where(point => point.HasFault && point.LatestFaultTime.HasValue)
            .OrderByDescending(point => point.LatestFaultTime)
            .Take(5)
            .Select(point => new InspectionRecentFaultModel(
                point.PointId,
                point.PointName,
                point.CurrentFaultType,
                point.LatestFaultTime?.ToString("yyyy-MM-dd HH:mm") ?? "--"))
            .ToList();

        var group = new InspectionGroupWorkspaceModel(
            new InspectionGroupModel(
                DefaultGroupId,
                groupName,
                BuildGroupSummary(points.Count, inspectionSettings),
                true),
            BuildStrategyModel(inspectionSettings),
            BuildExecutionModel(inspectionSettings, taskBoard),
            BuildRunSummary(taskBoard, groupName),
            taskBoard.CurrentTask?.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--",
            taskBoard,
            inspectionPoints,
            recentFaults);

        var renderablePoints = points.Where(point => point.Coordinate.CanRenderOnMap).ToList();
        var sourceBreakdown = points
            .GroupBy(point => MapPointSourceDiagnostics.ClassifySourceTag(point.SourceTag), StringComparer.Ordinal)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Count(), StringComparer.Ordinal);

        MapPointSourceDiagnostics.WriteLines("InspectionTask", [
            $"inspection workspace points = {points.Count}",
            $"inspection workspace renderablePoints = {renderablePoints.Count}",
            $"inspection workspace sources = {MapPointSourceDiagnostics.SummarizeCounts(sourceBreakdown)}",
            $"inspection workspace board = {DescribeBoard(taskBoard)}"
        ]);

        return ServiceResponse<InspectionWorkspaceSnapshot>.Success(new InspectionWorkspaceSnapshot([group]), pointCollectionResponse.Message);
    }

    public ServiceResponse<InspectionTaskRecordModel> StartSinglePointInspection(string groupId, string pointId)
    {
        var settings = _inspectionSettingsService.Load();
        if (!settings.TaskExecution.EnableSinglePointInspection)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    pointId,
                    InspectionTaskTypeModel.SinglePoint,
                    InspectionTaskStatusModel.Cancelled,
                    "单点巡检已在设置中心关闭。"),
                "单点巡检已关闭。");
        }

        var pointResponse = _pointWorkspaceService.GetPoint(pointId);
        if (!pointResponse.IsSuccess)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    pointId,
                    InspectionTaskTypeModel.SinglePoint,
                    InspectionTaskStatusModel.Failed,
                    NormalizeReason(pointResponse.Message, "未找到目标点位。")),
                pointResponse.Message);
        }

        return TryStartTask(
            groupId,
            BuildGroupName(),
            InspectionTaskTypeModel.SinglePoint,
            InspectionTaskTriggerModel.Manual,
            settings,
            pointResponse.Data.PointId,
            null,
            [pointResponse.Data],
            []);
    }

    public ServiceResponse<InspectionTaskRecordModel> StartBatchInspection(string groupId, IReadOnlyList<string> pointIds)
    {
        var settings = _inspectionSettingsService.Load();
        if (!settings.TaskExecution.EnableBatchInspection)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    "批量点位巡检",
                    InspectionTaskTypeModel.Batch,
                    InspectionTaskStatusModel.Cancelled,
                    "批量巡检已在设置中心关闭。"),
                "批量巡检已关闭。");
        }

        var pointCollectionResponse = _pointWorkspaceService.GetPointCollection();
        if (!pointCollectionResponse.IsSuccess || pointCollectionResponse.Data.Count == 0)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    "批量点位巡检",
                    InspectionTaskTypeModel.Batch,
                    InspectionTaskStatusModel.Failed,
                    NormalizeReason(pointCollectionResponse.Message, "当前没有可执行的点位。")),
                pointCollectionResponse.Message);
        }

        var requestedIds = (pointIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (requestedIds.Count == 0)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    "批量点位巡检",
                    InspectionTaskTypeModel.Batch,
                    InspectionTaskStatusModel.Cancelled,
                    "未选择需要执行的点位。"),
                "未选择需要执行的点位。");
        }

        var pointsById = pointCollectionResponse.Data
            .ToDictionary(point => point.PointId, point => point, StringComparer.Ordinal);
        var matchedPoints = requestedIds
            .Where(pointsById.ContainsKey)
            .Select(id => pointsById[id])
            .ToList();

        if (matchedPoints.Count == 0)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    "批量点位巡检",
                    InspectionTaskTypeModel.Batch,
                    InspectionTaskStatusModel.Failed,
                    "所选点位未命中当前巡检组。"),
                "所选点位未命中当前巡检组。");
        }

        return TryStartTask(
            groupId,
            BuildGroupName(),
            InspectionTaskTypeModel.Batch,
            InspectionTaskTriggerModel.Manual,
            settings,
            "批量点位巡检",
            null,
            matchedPoints,
            requestedIds.Except(pointsById.Keys, StringComparer.Ordinal));
    }

    public ServiceResponse<InspectionTaskRecordModel> StartDefaultScopeInspection(string groupId)
    {
        var settings = _inspectionSettingsService.Load();
        if (!settings.TaskExecution.EnableBatchInspection)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    "默认范围巡检",
                    InspectionTaskTypeModel.ScopePlan,
                    InspectionTaskStatusModel.Cancelled,
                    "范围巡检已在设置中心关闭。"),
                "范围巡检已关闭。");
        }

        var pointCollectionResponse = _pointWorkspaceService.GetPointCollection();
        if (!pointCollectionResponse.IsSuccess || pointCollectionResponse.Data.Count == 0)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    "默认范围巡检",
                    InspectionTaskTypeModel.ScopePlan,
                    InspectionTaskStatusModel.Failed,
                    NormalizeReason(pointCollectionResponse.Message, "当前没有可执行的点位。")),
                pointCollectionResponse.Message);
        }

        var scopePlan = ResolveDefaultScopePlan(settings.ScopePlans);
        var selection = SelectScopePoints(scopePlan, pointCollectionResponse.Data);
        if (selection.Points.Count == 0)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    BuildGroupName(),
                    scopePlan?.PlanName ?? DefaultScopePlanName,
                    InspectionTaskTypeModel.ScopePlan,
                    InspectionTaskStatusModel.Failed,
                    "默认范围方案未命中可执行点位。"),
                "默认范围方案未命中可执行点位。");
        }

        return TryStartTask(
            groupId,
            BuildGroupName(),
            InspectionTaskTypeModel.ScopePlan,
            InspectionTaskTriggerModel.Manual,
            settings,
            selection.TaskName,
            scopePlan,
            selection.Points,
            []);
    }

    public ServiceResponse<InspectionTaskRecordModel> WritePointEvidence(InspectionPointEvidenceWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskId) || string.IsNullOrWhiteSpace(request.PointId))
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    DefaultGroupId,
                    BuildGroupName(),
                    "截图取证",
                    InspectionTaskTypeModel.SinglePoint,
                    InspectionTaskStatusModel.Failed,
                    "证据写回请求缺少任务或点位标识。"),
                "证据写回请求无效。");
        }

        InspectionTaskRecordModel? existingTask;
        lock (_syncRoot)
        {
            existingTask = _taskHistory.FirstOrDefault(task => string.Equals(task.TaskId, request.TaskId, StringComparison.Ordinal));
        }

        if (existingTask is null)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    DefaultGroupId,
                    BuildGroupName(),
                    request.PointId,
                    InspectionTaskTypeModel.SinglePoint,
                    InspectionTaskStatusModel.Failed,
                    "未找到需要写回证据的巡检任务。"),
                "未找到需要写回证据的巡检任务。");
        }

        if (existingTask.FindPointExecution(request.PointId) is null)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    existingTask.GroupId,
                    existingTask.GroupName,
                    request.PointId,
                    existingTask.TaskType,
                    InspectionTaskStatusModel.Failed,
                    "未找到需要写回证据的点位记录。"),
                "未找到需要写回证据的点位记录。");
        }

        var pointExecution = existingTask.FindPointExecution(request.PointId)!;
        var normalizedItems = NormalizeEvidenceItems(request);
        var evidenceCaptureState = NormalizeEvidenceCaptureState(request.EvidenceCaptureState, normalizedItems.Count);
        var evidenceSummary = NormalizeEvidenceSummary(request, evidenceCaptureState, normalizedItems.Count);
        var aiAnalysis = _inspectionEvidenceAiAnalysisService.Analyze(
            new InspectionPointAiAnalysisRequest(
                request.TaskId,
                request.PointId,
                request.DeviceCode,
                BuildPointContextSummary(existingTask, pointExecution, evidenceSummary),
                normalizedItems)
            {
                EvidenceCaptureState = evidenceCaptureState,
                EvidenceSummary = evidenceSummary
            });
        var normalizedItemsWithAi = ApplyAiAnalysisToEvidenceItems(normalizedItems, aiAnalysis);

        var updatedTask = UpdateTask(request.TaskId, current =>
        {
            var queue = current.PointExecutions
                .Select(point => string.Equals(point.PointId, request.PointId, StringComparison.Ordinal)
                    ? point with
                    {
                        ScreenshotPlannedCount = Math.Max(1, request.ScreenshotPlannedCount),
                        ScreenshotSuccessCount = Math.Max(0, request.ScreenshotSuccessCount),
                        EvidenceCaptureState = evidenceCaptureState,
                        EvidenceSummary = evidenceSummary,
                        EvidenceRetentionMode = NormalizeEvidenceRetentionMode(request.EvidenceRetentionMode),
                        EvidenceRetentionDays = Math.Max(0, request.EvidenceRetentionDays),
                        AllowManualSupplementScreenshot = request.AllowManualSupplementScreenshot,
                        AiAnalysisStatus = aiAnalysis.AiAnalysisStatus,
                        AiAnalysisSummary = aiAnalysis.AiAnalysisSummary,
                        IsAiAbnormalDetected = aiAnalysis.IsAiAbnormalDetected,
                        AiAbnormalTags = aiAnalysis.AbnormalTags,
                        AiConfidence = aiAnalysis.Confidence,
                        AiSuggestedAction = aiAnalysis.SuggestedAction,
                        RouteToReviewWallReserved = aiAnalysis.RouteToReviewWallReserved,
                        RouteToDispatchPoolReserved = aiAnalysis.RouteToDispatchPoolReserved,
                        ManualReviewRequiredReserved = aiAnalysis.ManualReviewRequiredReserved,
                        EvidenceItems = normalizedItemsWithAi
                    }
                    : point)
                .ToList();

            var updatedPoint = queue.First(point => string.Equals(point.PointId, request.PointId, StringComparison.Ordinal));
            var abnormalFlow = InspectionTaskModelExtensions.BuildAbnormalFlowSnapshot(queue);
            var baseSummary = BuildTaskSummary(
                current.Status,
                current.TotalPointCount,
                current.SuccessCount,
                current.FailureCount,
                current.SkippedCount,
                current.ScopePlanName,
                current.CurrentPointName);

            return current with
            {
                PointExecutions = queue,
                AbnormalFlow = abnormalFlow,
                Summary = BuildTaskSummaryWithAi(baseSummary, updatedPoint, abnormalFlow)
            };
        });

        ApplyEvidenceRetention(
            request.TaskId,
            request.PointId,
            NormalizeEvidenceRetentionMode(request.EvidenceRetentionMode),
            request.EvidenceRetentionDays);

        MapPointSourceDiagnostics.Write(
            "InspectionEvidence",
            $"pointId={request.PointId}, deviceCode={request.DeviceCode}, screenshotPlannedCount={Math.Max(1, request.ScreenshotPlannedCount)}, screenshotSuccessCount={Math.Max(0, request.ScreenshotSuccessCount)}, evidenceWritten={normalizedItems.Count > 0}, evidencePath={BuildEvidencePathLog(normalizedItems)}, evidenceRetentionMode={NormalizeEvidenceRetentionMode(request.EvidenceRetentionMode)}, evidenceRetentionDays={Math.Max(0, request.EvidenceRetentionDays)}");
        MapPointSourceDiagnostics.Write(
            "InspectionAi",
            $"pointId={request.PointId}, deviceCode={request.DeviceCode}, aiAnalysisStatus={aiAnalysis.AiAnalysisStatus}, abnormalTags={string.Join('|', aiAnalysis.AbnormalTags)}, routeToReviewWall={aiAnalysis.RouteToReviewWallReserved}, routeToDispatchPool={aiAnalysis.RouteToDispatchPoolReserved}, manualReviewRequired={aiAnalysis.ManualReviewRequiredReserved}, evidenceItemCount={normalizedItemsWithAi.Count}, aiAnalysisInvoked={aiAnalysis.AiAnalysisInvoked}, confidence={aiAnalysis.Confidence:0.00}");

        return ServiceResponse<InspectionTaskRecordModel>.Success(updatedTask);
    }

    private ServiceResponse<InspectionTaskRecordModel> TryStartTask(
        string groupId,
        string groupName,
        InspectionTaskTypeModel taskType,
        InspectionTaskTriggerModel triggerMode,
        InspectionSettingsSnapshot settings,
        string taskNameSeed,
        InspectionScopePlanSettings? scopePlan,
        IReadOnlyList<PointWorkspaceItemModel> points,
        IEnumerable<string> missingPointIds)
    {
        var validationFailure = ValidateStart(groupId, groupName, taskType, taskNameSeed, settings.TaskExecution);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var taskId = $"aitc-{DateTime.Now:yyyyMMddHHmmssfff}-{ResolveTaskTypeCode(taskType)}";
        var taskName = BuildTaskName(
            settings.TaskExecution.DefaultTaskNamePattern,
            taskType,
            groupId,
            points.FirstOrDefault()?.PointId ?? "all",
            points.FirstOrDefault()?.DeviceCode ?? "all",
            scopePlan?.PlanId);

        var queuePoints = BuildExecutionQueue(points, settings, scopePlan);
        queuePoints.AddRange(BuildMissingPointQueue(missingPointIds, queuePoints.Count));
        if (queuePoints.Count == 0)
        {
            return ServiceResponse<InspectionTaskRecordModel>.Failure(
                CreateRejectedTask(
                    groupId,
                    groupName,
                    taskNameSeed,
                    taskType,
                    InspectionTaskStatusModel.Failed,
                    "任务未生成点位执行队列。"),
                "任务未生成点位执行队列。");
        }

        var createdTask = new InspectionTaskRecordModel(
            taskId,
            groupId,
            groupName,
            string.IsNullOrWhiteSpace(taskName) ? taskNameSeed : taskName,
            taskType,
            triggerMode,
            InspectionTaskStatusModel.Pending,
            DateTime.Now,
            null,
            null,
            scopePlan?.PlanId,
            scopePlan?.PlanName ?? DefaultScopePlanName,
            queuePoints.Count,
            0,
            queuePoints.Count(candidate => candidate.Status == InspectionPointExecutionStatusModel.Failed),
            queuePoints.Count(candidate => candidate.Status == InspectionPointExecutionStatusModel.Skipped),
            null,
            "--",
            BuildTaskSummary(
                InspectionTaskStatusModel.Pending,
                queuePoints.Count,
                0,
                queuePoints.Count(candidate => candidate.Status == InspectionPointExecutionStatusModel.Failed),
                queuePoints.Count(candidate => candidate.Status == InspectionPointExecutionStatusModel.Skipped),
                scopePlan?.PlanName ?? DefaultScopePlanName,
                "--"),
            queuePoints);

        SaveTask(createdTask);
        _ = ExecuteTaskAsync(createdTask, points, settings);

        MapPointSourceDiagnostics.Write(
            "InspectionTask",
            $"task created: taskId={createdTask.TaskId}, taskName={createdTask.TaskName}, type={createdTask.TaskType}, trigger={createdTask.TriggerMode}, groupId={groupId}, points={createdTask.TotalPointCount}");

        return ServiceResponse<InspectionTaskRecordModel>.Success(createdTask);
    }

    private ServiceResponse<InspectionTaskRecordModel>? ValidateStart(
        string groupId,
        string groupName,
        InspectionTaskTypeModel taskType,
        string taskNameSeed,
        InspectionTaskExecutionSettings taskExecution)
    {
        lock (_syncRoot)
        {
            if (taskExecution.EnforceGroupSerialExecution
                && _latestTaskByGroupId.TryGetValue(groupId, out var latestTask)
                && IsTaskRunning(latestTask.Status))
            {
                return ServiceResponse<InspectionTaskRecordModel>.Failure(
                    CreateRejectedTask(
                        groupId,
                        groupName,
                        taskNameSeed,
                        taskType,
                        InspectionTaskStatusModel.Cancelled,
                        "当前巡检组已有任务执行中，本轮未重复启动。"),
                    "当前巡检组已有任务执行中。");
            }

            var runningTaskCount = _latestTaskByGroupId.Values.Count(task => IsTaskRunning(task.Status));
            if (runningTaskCount >= taskExecution.ReservedMaxConcurrency)
            {
                return ServiceResponse<InspectionTaskRecordModel>.Failure(
                    CreateRejectedTask(
                        groupId,
                        groupName,
                        taskNameSeed,
                        taskType,
                        InspectionTaskStatusModel.Cancelled,
                        $"当前已达到并发预留上限 {taskExecution.ReservedMaxConcurrency}，请等待上一轮任务完成。"),
                    "当前已达到并发预留上限。");
            }
        }

        return null;
    }

    private async Task ExecuteTaskAsync(
        InspectionTaskRecordModel task,
        IReadOnlyList<PointWorkspaceItemModel> points,
        InspectionSettingsSnapshot settings)
    {
        try
        {
            var startedTask = UpdateTask(task.TaskId, current =>
            {
                var firstRunnable = current.PointExecutions
                    .FirstOrDefault(candidate => candidate.Status == InspectionPointExecutionStatusModel.Pending);
                return current with
                {
                    Status = InspectionTaskStatusModel.Running,
                    StartedAt = DateTime.Now,
                    CurrentPointId = firstRunnable?.PointId,
                    CurrentPointName = firstRunnable?.PointName ?? "--",
                    Summary = BuildTaskSummary(
                        InspectionTaskStatusModel.Running,
                        current.TotalPointCount,
                        current.SuccessCount,
                        current.FailureCount,
                        current.SkippedCount,
                        current.ScopePlanName,
                        firstRunnable?.PointName ?? "--")
                };
            });

            var pointsById = points.ToDictionary(point => point.PointId, point => point, StringComparer.Ordinal);
            var currentTask = startedTask;

            foreach (var pointExecution in startedTask.PointExecutions.OrderBy(candidate => candidate.Sequence))
            {
                if (pointExecution.Status is InspectionPointExecutionStatusModel.Failed or InspectionPointExecutionStatusModel.Skipped)
                {
                    continue;
                }

                currentTask = UpdateTask(currentTask.TaskId, current =>
                    SetPointRunning(current, pointExecution.PointId));

                if (!pointsById.TryGetValue(pointExecution.PointId, out var point))
                {
                    currentTask = UpdateTask(currentTask.TaskId, current =>
                        ApplyPointResult(
                            current,
                            pointExecution.PointId,
                            InspectionPointExecutionStatusModel.Failed,
                            InspectionPointFailureCategoryModel.PointNotFound,
                            string.Empty,
                            "点位数据已失效，本轮未继续推进。"));
                    continue;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(180)).ConfigureAwait(false);

                InspectionPointCheckResult result;
                try
                {
                    var effectivePolicy = ResolveEffectivePolicy(
                        point,
                        settings,
                        pointExecution.IsFocusPoint,
                        pointExecution.UsesOverridePolicy,
                        pointExecution.PolicySnapshotSummary);
                    result = await _pointCheckExecutor.ExecuteAsync(
                            new InspectionPointCheckRequest(
                                currentTask.TaskId,
                                currentTask.GroupId,
                                currentTask.TaskName,
                                currentTask.TaskType,
                                currentTask.TriggerMode,
                                currentTask.ScopePlanId,
                                currentTask.ScopePlanName,
                                point,
                                effectivePolicy.Policy,
                                settings.VideoInspection,
                                effectivePolicy.IsFocusPoint,
                                effectivePolicy.UsesOverridePolicy,
                                effectivePolicy.PolicySnapshotSummary),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    MapPointSourceDiagnostics.Write(
                        "InspectionTask",
                        $"point execution failed unexpectedly: taskId={currentTask.TaskId}, pointId={pointExecution.PointId}, reason={NormalizeReason(ex.Message, "点位巡检执行异常。")}");
                    result = new InspectionPointCheckResult(
                        InspectionPointExecutionStatusModel.Failed,
                        InspectionPointFailureCategoryModel.Unknown,
                        NormalizeReason(ex.Message, "点位巡检执行异常。"),
                        []);
                }

                currentTask = UpdateTask(currentTask.TaskId, current => ApplyPointResult(current, point.PointId, result));
            }

            FinalizeTask(currentTask.TaskId);
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionTask",
                $"task execution aborted unexpectedly: taskId={task.TaskId}, reason={NormalizeReason(ex.Message, "任务执行异常中断。")}");
            TryFinalizeAbortedTask(task.TaskId, ex);
        }
    }

    private void FinalizeTask(string taskId)
    {
        UpdateTask(taskId, current =>
        {
            var finalStatus = ResolveFinalTaskStatus(current);
            return current with
            {
                Status = finalStatus,
                FinishedAt = DateTime.Now,
                CurrentPointId = null,
                CurrentPointName = "--",
                Summary = BuildTaskSummary(
                    finalStatus,
                    current.TotalPointCount,
                    current.SuccessCount,
                    current.FailureCount,
                    current.SkippedCount,
                    current.ScopePlanName,
                    "--")
            };
        });
    }

    private void TryFinalizeAbortedTask(string taskId, Exception exception)
    {
        try
        {
            UpdateTask(taskId, current =>
            {
                var faultedTask = current;
                if (!string.IsNullOrWhiteSpace(current.CurrentPointId))
                {
                    faultedTask = ApplyPointResult(
                        current,
                        current.CurrentPointId,
                        InspectionPointExecutionStatusModel.Failed,
                        InspectionPointFailureCategoryModel.Unknown,
                        string.Empty,
                        NormalizeReason(exception.Message, "任务执行异常中断。"));
                }

                var finalStatus = ResolveFinalTaskStatus(faultedTask);
                return faultedTask with
                {
                    Status = finalStatus,
                    FinishedAt = DateTime.Now,
                    CurrentPointId = null,
                    CurrentPointName = "--",
                    Summary = $"任务执行异常中断：{NormalizeReason(exception.Message, "请检查点位巡检执行器。")}"
                };
            });
        }
        catch (Exception finalizeException)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionTask",
                $"task abort finalize failed: taskId={taskId}, reason={NormalizeReason(finalizeException.Message, "任务异常收口失败。")}");
        }
    }

    private InspectionTaskRecordModel UpdateTask(
        string taskId,
        Func<InspectionTaskRecordModel, InspectionTaskRecordModel> update)
    {
        InspectionTaskRecordModel updatedTask;
        lock (_syncRoot)
        {
            var index = _taskHistory.FindIndex(task => string.Equals(task.TaskId, taskId, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new InvalidOperationException($"Inspection task not found: {taskId}");
            }

            updatedTask = update(_taskHistory[index]);
            _taskHistory[index] = updatedTask;
            _latestTaskByGroupId[updatedTask.GroupId] = updatedTask;
            PersistTaskHistory();
        }

        RaiseTaskBoardChanged(updatedTask.GroupId);
        MapPointSourceDiagnostics.Write(
            "InspectionTask",
            $"task updated: taskId={updatedTask.TaskId}, status={updatedTask.Status}, success={updatedTask.SuccessCount}, failed={updatedTask.FailureCount}, skipped={updatedTask.SkippedCount}, currentPoint={updatedTask.CurrentPointName}");
        return updatedTask;
    }

    private void SaveTask(InspectionTaskRecordModel task)
    {
        lock (_syncRoot)
        {
            _taskHistory.RemoveAll(candidate => string.Equals(candidate.TaskId, task.TaskId, StringComparison.Ordinal));
            _taskHistory.Insert(0, task);
            if (_taskHistory.Count > TaskHistoryLimit)
            {
                _taskHistory.RemoveRange(TaskHistoryLimit, _taskHistory.Count - TaskHistoryLimit);
            }

            _latestTaskByGroupId[task.GroupId] = task;
            PersistTaskHistory();
        }

        RaiseTaskBoardChanged(task.GroupId);
    }

    private void PersistTaskHistory()
    {
        _taskHistoryStore.Save(_taskHistory);
    }

    private void RaiseTaskBoardChanged(string groupId)
    {
        TaskBoardChanged?.Invoke(this, new InspectionTaskBoardChangedEventArgs(groupId));
    }

    private static InspectionTaskRecordModel SetPointRunning(InspectionTaskRecordModel task, string pointId)
    {
        var queue = task.PointExecutions
            .Select(point => string.Equals(point.PointId, pointId, StringComparison.Ordinal)
                ? point with { Status = InspectionPointExecutionStatusModel.Running, SkipReason = string.Empty, FailureReason = string.Empty }
                : point)
            .ToList();
        var currentPoint = queue.First(point => string.Equals(point.PointId, pointId, StringComparison.Ordinal));
        return task with
        {
            PointExecutions = queue,
            CurrentPointId = currentPoint.PointId,
            CurrentPointName = currentPoint.PointName,
            Summary = BuildTaskSummary(
                InspectionTaskStatusModel.Running,
                task.TotalPointCount,
                task.SuccessCount,
                task.FailureCount,
                task.SkippedCount,
                task.ScopePlanName,
                currentPoint.PointName)
        };
    }

    private static InspectionTaskRecordModel ApplyPointResult(
        InspectionTaskRecordModel task,
        string pointId,
        InspectionPointCheckResult result)
    {
        var queue = task.PointExecutions
            .Select(point => string.Equals(point.PointId, pointId, StringComparison.Ordinal)
                ? point with
                {
                    Status = result.Status,
                    SkipReason = result.Status == InspectionPointExecutionStatusModel.Skipped ? result.ResultSummary : string.Empty,
                    FailureCategory = result.FailureCategory,
                    FailureReason = result.Status == InspectionPointExecutionStatusModel.Failed ? result.ResultSummary : string.Empty,
                    ExecutionSummary = result.ResultSummary,
                    OnlineCheckResult = result.OnlineCheckResult,
                    StreamUrlAcquireResult = result.StreamUrlAcquireResult,
                    PlaybackAttemptCount = result.PlaybackAttemptCount,
                    ProtocolFallbackUsed = result.ProtocolFallbackUsed,
                    FinalPlaybackResult = string.IsNullOrWhiteSpace(result.FinalPlaybackResult)
                        ? result.ResultSummary
                        : result.FinalPlaybackResult,
                     PreviewUrl = result.PreviewUrl ?? string.Empty,
                     PreviewHostKind = result.PreviewHostKind ?? string.Empty,
                     EvidenceCaptureState = ResolvePendingEvidenceCaptureState(point, result),
                     EvidenceSummary = ResolvePendingEvidenceSummary(point, result),
                     AiAnalysisStatus = InspectionEvidenceValueKeys.AiAnalysisReserved,
                     AiAnalysisSummary = string.Empty,
                     IsAiAbnormalDetected = false,
                     AiAbnormalTags = [],
                     AiConfidence = 0d,
                     AiSuggestedAction = string.Empty,
                     RouteToReviewWallReserved = false,
                     RouteToDispatchPoolReserved = false,
                     ManualReviewRequiredReserved = false
                 }
                 : point)
             .ToList();

        var successCount = queue.Count(point => point.Status == InspectionPointExecutionStatusModel.Succeeded);
        var failureCount = queue.Count(point => point.Status == InspectionPointExecutionStatusModel.Failed);
        var skippedCount = queue.Count(point => point.Status == InspectionPointExecutionStatusModel.Skipped);
        var nextPendingPoint = queue.FirstOrDefault(point => point.Status == InspectionPointExecutionStatusModel.Pending);

        return task with
        {
            PointExecutions = queue,
            SuccessCount = successCount,
            FailureCount = failureCount,
            SkippedCount = skippedCount,
            CurrentPointId = nextPendingPoint?.PointId,
            CurrentPointName = nextPendingPoint?.PointName ?? "--",
            Summary = BuildTaskSummary(
                InspectionTaskStatusModel.Running,
                task.TotalPointCount,
                successCount,
                failureCount,
                skippedCount,
                task.ScopePlanName,
                nextPendingPoint?.PointName ?? "--")
        };
    }

    private static InspectionTaskRecordModel ApplyPointResult(
        InspectionTaskRecordModel task,
        string pointId,
        InspectionPointExecutionStatusModel status,
        InspectionPointFailureCategoryModel failureCategory,
        string skipReason,
        string failureReason)
    {
        var queue = task.PointExecutions
            .Select(point => string.Equals(point.PointId, pointId, StringComparison.Ordinal)
                ? point with
                {
                    Status = status,
                    SkipReason = skipReason,
                    FailureCategory = failureCategory,
                    FailureReason = failureReason
                }
                : point)
            .ToList();

        var successCount = queue.Count(point => point.Status == InspectionPointExecutionStatusModel.Succeeded);
        var failureCount = queue.Count(point => point.Status == InspectionPointExecutionStatusModel.Failed);
        var skippedCount = queue.Count(point => point.Status == InspectionPointExecutionStatusModel.Skipped);
        var nextPendingPoint = queue.FirstOrDefault(point => point.Status == InspectionPointExecutionStatusModel.Pending);

        return task with
        {
            PointExecutions = queue,
            SuccessCount = successCount,
            FailureCount = failureCount,
            SkippedCount = skippedCount,
            CurrentPointId = nextPendingPoint?.PointId,
            CurrentPointName = nextPendingPoint?.PointName ?? "--",
            Summary = BuildTaskSummary(
                InspectionTaskStatusModel.Running,
                task.TotalPointCount,
                successCount,
                failureCount,
                skippedCount,
                task.ScopePlanName,
                nextPendingPoint?.PointName ?? "--")
        };
    }

    private static InspectionTaskStatusModel ResolveFinalTaskStatus(InspectionTaskRecordModel task)
    {
        if (task.TotalPointCount == 0)
        {
            return InspectionTaskStatusModel.Failed;
        }

        if (task.SuccessCount == task.TotalPointCount)
        {
            return InspectionTaskStatusModel.Completed;
        }

        if (task.FailureCount == task.TotalPointCount)
        {
            return InspectionTaskStatusModel.Failed;
        }

        return task.FailureCount > 0 || task.SkippedCount > 0
            ? InspectionTaskStatusModel.PartialFailure
            : InspectionTaskStatusModel.Completed;
    }

    private static string BuildTaskSummary(
        InspectionTaskStatusModel status,
        int total,
        int success,
        int failure,
        int skipped,
        string scopePlanName,
        string currentPointName)
    {
        return status switch
        {
            InspectionTaskStatusModel.Pending => $"任务已生成，命中范围“{scopePlanName}”，共 {total} 个点位待执行。",
            InspectionTaskStatusModel.Running => $"任务执行中，已完成 {success + failure + skipped} / {total} 个点位，当前推进“{currentPointName}”。",
            InspectionTaskStatusModel.Completed => $"任务已完成，共 {total} 个点位，成功 {success} 个。",
            InspectionTaskStatusModel.PartialFailure => $"任务已完成，共 {total} 个点位，成功 {success} 个，失败 {failure} 个，跳过 {skipped} 个。",
            InspectionTaskStatusModel.Failed => $"任务执行失败，共 {total} 个点位，失败 {failure} 个，跳过 {skipped} 个。",
            InspectionTaskStatusModel.Cancelled => "任务已取消，本轮未进入点位执行。",
            _ => "任务状态待更新。"
        };
    }

    private static string BuildTaskSummaryWithAi(string baseSummary, InspectionTaskPointExecutionModel pointExecution)
    {
        if (string.IsNullOrWhiteSpace(pointExecution.AiAnalysisSummary))
        {
            return baseSummary;
        }

        return $"{baseSummary} 最新AI结论：{pointExecution.PointName} - {pointExecution.AiAnalysisSummary}";
    }

    private static string BuildTaskSummaryWithAi(
        string baseSummary,
        InspectionTaskPointExecutionModel pointExecution,
        InspectionTaskAbnormalFlowModel abnormalFlow)
    {
        var summary = BuildTaskSummaryWithAi(baseSummary, pointExecution);
        var abnormalFlowSummary = BuildAbnormalFlowSummary(abnormalFlow);
        return string.IsNullOrWhiteSpace(abnormalFlowSummary)
            ? summary
            : $"{summary} {abnormalFlowSummary}";
    }

    private static string BuildAbnormalFlowSummary(InspectionTaskAbnormalFlowModel abnormalFlow)
    {
        var reviewWallCount = abnormalFlow.ReviewWallPendingCount;
        var dispatchPoolCount = abnormalFlow.DispatchPoolCandidateCount;
        var manualReviewCount = abnormalFlow.ManualReviewCompatibilityCount;
        if (reviewWallCount == 0 && dispatchPoolCount == 0 && manualReviewCount == 0)
        {
            return string.Empty;
        }

        var segments = new List<string>();
        if (reviewWallCount > 0)
        {
            segments.Add($"复核墙暂存 {reviewWallCount} 个");
        }

        if (dispatchPoolCount > 0)
        {
            segments.Add($"派单池候选 {dispatchPoolCount} 个");
        }

        if (manualReviewCount > 0)
        {
            segments.Add($"人工补图兼容标记 {manualReviewCount} 个");
        }

        return $"异常流转入口：{string.Join("，", segments)}。";
    }

    private static IReadOnlyList<InspectionPointEvidenceMetadataModel> ApplyAiAnalysisToEvidenceItems(
        IReadOnlyList<InspectionPointEvidenceMetadataModel> evidenceItems,
        InspectionPointAiAnalysisResult aiAnalysis)
    {
        return evidenceItems
            .Select(item => item with
            {
                AiAnalysisStatus = aiAnalysis.AiAnalysisStatus,
                AiAnalysisSummary = aiAnalysis.AiAnalysisSummary
            })
            .ToList();
    }

    private static string BuildPointContextSummary(
        InspectionTaskRecordModel task,
        InspectionTaskPointExecutionModel pointExecution,
        string evidenceSummary)
    {
        var segments = new[]
        {
            $"task={task.TaskName}",
            $"scope={task.ScopePlanName}",
            $"point={pointExecution.PointName}",
            $"execution={pointExecution.ExecutionSummary}",
            $"failure={pointExecution.FailureReason}",
            $"playback={pointExecution.FinalPlaybackResult}",
            $"evidence={evidenceSummary}"
        };

        return string.Join(
            " | ",
            segments.Where(segment =>
                !segment.EndsWith("=", StringComparison.Ordinal)
                && !segment.EndsWith("=--", StringComparison.Ordinal)));
    }

    private InspectionTaskBoardModel BuildTaskBoard(string groupId, string groupName)
    {
        lock (_syncRoot)
        {
            var groupTasks = _taskHistory
                .Where(task => string.Equals(task.GroupId, groupId, StringComparison.Ordinal))
                .OrderByDescending(task => task.CreatedAt)
                .ToList();

            InspectionTaskRecordModel? currentTask = null;
            if (_latestTaskByGroupId.TryGetValue(groupId, out var latestTask))
            {
                currentTask = latestTask;
            }
            else if (groupTasks.Count > 0)
            {
                currentTask = groupTasks[0];
            }

            currentTask ??= CreateEmptyTask(groupId, groupName);
            var recentTasks = groupTasks
                .Where(task => currentTask.IsPlaceholderTask()
                    || !string.Equals(task.TaskId, currentTask.TaskId, StringComparison.Ordinal))
                .Take(6)
                .ToList();

            return new InspectionTaskBoardModel(currentTask, recentTasks);
        }
    }

    private static InspectionTaskRecordModel CreateEmptyTask(string groupId, string groupName)
    {
        return new InspectionTaskRecordModel(
            $"{groupId}-empty",
            groupId,
            groupName,
            "待发起巡检任务",
            InspectionTaskTypeModel.ScopePlan,
            InspectionTaskTriggerModel.Manual,
            InspectionTaskStatusModel.Pending,
            DateTime.Now,
            null,
            null,
            null,
            DefaultScopePlanName,
            0,
            0,
            0,
            0,
            null,
            "--",
            "尚未发起巡检任务，可从当前组直接启动范围巡检或单点巡检。",
            []);
    }

    private static InspectionWorkspaceSnapshot CreatePendingWorkspace(
        string groupName,
        InspectionTaskBoardModel taskBoard,
        InspectionSettingsSnapshot settings)
    {
        return new InspectionWorkspaceSnapshot([
            new InspectionGroupWorkspaceModel(
                new InspectionGroupModel(DefaultGroupId, groupName, "点位状态待接入", true),
                BuildStrategyModel(settings),
                BuildExecutionModel(settings, taskBoard),
                BuildRunSummary(taskBoard, groupName),
                taskBoard.CurrentTask?.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--",
                taskBoard,
                [],
                [])
        ]);
    }

    private static InspectionStrategyModel BuildStrategyModel(InspectionSettingsSnapshot settings)
    {
        var defaultScopePlan = ResolveDefaultScopePlan(settings.ScopePlans);
        var resultMode = settings.AlertStrategy.GlobalPolicy.RouteAbnormalPointsToReviewWall
            ? "异常先进入上墙复核"
            : "异常直接进入派单池";
        var dispatchMode = string.Equals(settings.DispatchStrategy.GlobalDispatchMode, InspectionSettingsValueKeys.DispatchModeAuto, StringComparison.OrdinalIgnoreCase)
            ? "自动派单"
            : "人工确认派单";
        var dailyRuns = settings.TaskExecution.ReserveScheduledTasks
            ? "预留定时接入"
            : "本轮仅手工触发";

        return new InspectionStrategyModel(
            defaultScopePlan is null ? "待设置默认范围方案" : "按默认范围方案执行",
            dailyRuns,
            $"同组串行 / 复检间隔 {settings.VideoInspection.ReinspectionIntervalMinutes} 分钟",
            resultMode,
            dispatchMode);
    }

    private static InspectionExecutionModel BuildExecutionModel(
        InspectionSettingsSnapshot settings,
        InspectionTaskBoardModel taskBoard)
    {
        var currentTask = taskBoard.CurrentTask;
        var executedTodayCount = CountExecutedToday(taskBoard);
        var nextRunTime = settings.TaskExecution.ReserveScheduledTasks
            ? DateTime.Now.AddMinutes(settings.VideoInspection.ReinspectionIntervalMinutes).ToString("yyyy-MM-dd HH:mm")
            : "预留定时";

        return new InspectionExecutionModel(
            $"{executedTodayCount} 次",
            ResolveTaskStatusText(currentTask?.Status ?? InspectionTaskStatusModel.Pending, settings.TaskExecution.EnableBatchInspection),
            nextRunTime,
            currentTask?.Summary ?? "尚未发起巡检任务。",
            settings.TaskExecution.EnableBatchInspection);
    }

    private static int CountExecutedToday(InspectionTaskBoardModel taskBoard)
    {
        var taskIds = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        if (taskBoard.CurrentTask is not null
            && !taskBoard.CurrentTask.IsPlaceholderTask()
            && taskBoard.CurrentTask.CreatedAt.Date == DateTime.Today
            && taskIds.Add(taskBoard.CurrentTask.TaskId))
        {
            count++;
        }

        count += taskBoard.RecentTasks.Count(task =>
            task.CreatedAt.Date == DateTime.Today
            && taskIds.Add(task.TaskId));

        return count;
    }

    private static InspectionRunSummaryModel BuildRunSummary(
        InspectionTaskBoardModel taskBoard,
        string groupName)
    {
        var startedAt = taskBoard.CurrentTask?.StartedAt
            ?? taskBoard.CurrentTask?.CreatedAt
            ?? taskBoard.RecentTasks.FirstOrDefault()?.StartedAt
            ?? taskBoard.RecentTasks.FirstOrDefault()?.CreatedAt
            ?? DateTime.Now;

        return new InspectionRunSummaryModel(groupName, startedAt.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private static string ResolveTaskStatusText(InspectionTaskStatusModel status, bool isEnabled)
    {
        if (!isEnabled)
        {
            return "已停用";
        }

        return status switch
        {
            InspectionTaskStatusModel.Pending => "待执行",
            InspectionTaskStatusModel.Running => "执行中",
            InspectionTaskStatusModel.Completed => "已完成",
            InspectionTaskStatusModel.PartialFailure => "部分失败",
            InspectionTaskStatusModel.Failed => "失败",
            InspectionTaskStatusModel.Cancelled => "已取消",
            _ => "待执行"
        };
    }

    private static string BuildGroupSummary(int pointCount, InspectionSettingsSnapshot settings)
    {
        var scopePlan = ResolveDefaultScopePlan(settings.ScopePlans)?.PlanName ?? DefaultScopePlanName;
        return $"本组纳管 {pointCount} 个点位，默认按“{scopePlan}”生成任务，同组串行执行。";
    }

    private static string BuildGroupName()
        => DefaultGroupName;

    private static InspectionScopePlanSettings? ResolveDefaultScopePlan(IReadOnlyList<InspectionScopePlanSettings> scopePlans)
    {
        return scopePlans
            .FirstOrDefault(plan => plan.IsEnabled && plan.IsDefault)
            ?? scopePlans.FirstOrDefault(plan => plan.IsEnabled);
    }

    private static ScopeSelectionResult SelectScopePoints(
        InspectionScopePlanSettings? scopePlan,
        IReadOnlyList<PointWorkspaceItemModel> points)
    {
        if (scopePlan is null)
        {
            return new ScopeSelectionResult("默认范围巡检", points.ToList());
        }

        var byPointId = points.ToDictionary(point => point.PointId, point => point, StringComparer.Ordinal);
        var matchedPoints = scopePlan.IncludedPointIds
            .Where(pointId => byPointId.ContainsKey(pointId))
            .Select(pointId => byPointId[pointId])
            .ToList();

        if (matchedPoints.Count == 0)
        {
            matchedPoints = points.ToList();
        }

        if (scopePlan.ExcludedPointIds.Count > 0)
        {
            matchedPoints = matchedPoints
                .Where(point => !scopePlan.ExcludedPointIds.Contains(point.PointId, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        return new ScopeSelectionResult(scopePlan.PlanName, matchedPoints);
    }

    private List<InspectionTaskPointExecutionModel> BuildExecutionQueue(
        IReadOnlyList<PointWorkspaceItemModel> points,
        InspectionSettingsSnapshot settings,
        InspectionScopePlanSettings? scopePlan)
    {
        return points
            .Select((point, index) =>
            {
                var policy = ResolveEffectivePolicy(point, settings, scopePlan);
                return new InspectionTaskPointExecutionModel(
                    point.PointId,
                    point.DeviceCode,
                    point.PointName,
                    index + 1,
                    InspectionPointExecutionStatusModel.Pending,
                    string.Empty,
                    InspectionPointFailureCategoryModel.None,
                    string.Empty,
                    policy.IsFocusPoint,
                    policy.UsesOverridePolicy,
                    policy.PolicySnapshotSummary)
                {
                    ScreenshotPlannedCount = Math.Max(1, settings.VideoInspection.ScreenshotCount),
                    ScreenshotIntervalSeconds = Math.Max(1, settings.VideoInspection.ScreenshotIntervalSeconds),
                    EvidenceCaptureState = InspectionEvidenceValueKeys.CaptureStateNone,
                    EvidenceSummary = "待播放结果确定后生成截图证据。",
                    EvidenceRetentionMode = NormalizeEvidenceRetentionMode(settings.VideoInspection.EvidenceRetentionMode),
                    EvidenceRetentionDays = Math.Max(0, settings.VideoInspection.EvidenceRetentionDays),
                    AllowManualSupplementScreenshot = settings.VideoInspection.AllowManualSupplementScreenshot,
                    AiAnalysisStatus = InspectionEvidenceValueKeys.AiAnalysisReserved,
                    AiAnalysisSummary = string.Empty
                };
            })
            .ToList();
    }

    private static string ResolvePendingEvidenceCaptureState(
        InspectionTaskPointExecutionModel point,
        InspectionPointCheckResult result)
    {
        if (result.Status is InspectionPointExecutionStatusModel.Succeeded or InspectionPointExecutionStatusModel.Failed)
        {
            return InspectionEvidenceValueKeys.CaptureStatePending;
        }

        return point.EvidenceCaptureState;
    }

    private static string ResolvePendingEvidenceSummary(
        InspectionTaskPointExecutionModel point,
        InspectionPointCheckResult result)
    {
        if (result.Status == InspectionPointExecutionStatusModel.Succeeded)
        {
            return $"播放成功，待按策略生成 {Math.Max(1, point.ScreenshotPlannedCount)} 张截图证据。";
        }

        if (result.Status == InspectionPointExecutionStatusModel.Failed)
        {
            return "播放失败，待生成失败证据快照。";
        }

        return point.EvidenceSummary;
    }

    private static IReadOnlyList<InspectionPointEvidenceMetadataModel> NormalizeEvidenceItems(InspectionPointEvidenceWriteRequest request)
    {
        return (request.EvidenceItems ?? Array.Empty<InspectionPointEvidenceMetadataModel>())
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.LocalFilePath))
            .Select(item => item with
            {
                TaskId = string.IsNullOrWhiteSpace(item.TaskId) ? request.TaskId : item.TaskId.Trim(),
                PointId = string.IsNullOrWhiteSpace(item.PointId) ? request.PointId : item.PointId.Trim(),
                DeviceCode = string.IsNullOrWhiteSpace(item.DeviceCode) ? request.DeviceCode : item.DeviceCode.Trim(),
                LocalFilePath = item.LocalFilePath.Trim(),
                EvidenceSummary = item.EvidenceSummary?.Trim() ?? string.Empty,
                EvidenceKind = string.IsNullOrWhiteSpace(item.EvidenceKind)
                    ? InspectionEvidenceValueKeys.EvidenceKindPlaybackScreenshot
                    : item.EvidenceKind.Trim(),
                EvidenceSource = string.IsNullOrWhiteSpace(item.EvidenceSource)
                    ? InspectionEvidenceValueKeys.EvidenceSourcePreviewHost
                    : item.EvidenceSource.Trim(),
                AiAnalysisStatus = string.IsNullOrWhiteSpace(item.AiAnalysisStatus)
                    ? InspectionEvidenceValueKeys.AiAnalysisPending
                    : item.AiAnalysisStatus.Trim(),
                AiAnalysisSummary = item.AiAnalysisSummary?.Trim() ?? string.Empty
            })
            .OrderBy(item => item.ScreenshotTime)
            .ToList();
    }

    private static string NormalizeEvidenceCaptureState(string captureState, int evidenceCount)
    {
        if (!string.IsNullOrWhiteSpace(captureState))
        {
            return captureState.Trim();
        }

        return evidenceCount > 0
            ? InspectionEvidenceValueKeys.CaptureStateCompleted
            : InspectionEvidenceValueKeys.CaptureStateFailed;
    }

    private static string NormalizeEvidenceSummary(
        InspectionPointEvidenceWriteRequest request,
        string captureState,
        int evidenceCount)
    {
        if (!string.IsNullOrWhiteSpace(request.EvidenceSummary))
        {
            return request.EvidenceSummary.Trim();
        }

        return captureState switch
        {
            InspectionEvidenceValueKeys.CaptureStateCompleted when evidenceCount > 0 => $"已写入 {evidenceCount} 份证据。",
            InspectionEvidenceValueKeys.CaptureStateFailed => "截图取证未成功写入，待人工补充。",
            InspectionEvidenceValueKeys.CaptureStatePending => "截图取证待执行。",
            _ => string.Empty
        };
    }

    private static string NormalizeEvidenceRetentionMode(string retentionMode)
    {
        return retentionMode?.Trim() switch
        {
            InspectionSettingsValueKeys.EvidenceRetentionKeepLatestTask => InspectionSettingsValueKeys.EvidenceRetentionKeepLatestTask,
            InspectionSettingsValueKeys.EvidenceRetentionManualCleanup => InspectionSettingsValueKeys.EvidenceRetentionManualCleanup,
            _ => InspectionSettingsValueKeys.EvidenceRetentionKeepDays
        };
    }

    private static string BuildEvidencePathLog(IReadOnlyList<InspectionPointEvidenceMetadataModel> evidenceItems)
    {
        return evidenceItems.Count == 0
            ? string.Empty
            : string.Join(" | ", evidenceItems.Select(item => item.LocalFilePath));
    }

    private static void ApplyEvidenceRetention(
        string taskId,
        string pointId,
        string retentionMode,
        int retentionDays)
    {
        try
        {
            var currentTaskDirectory = InspectionEvidencePathBuilder.GetPointTaskDirectory(taskId, pointId);
            var pointDirectory = Directory.GetParent(currentTaskDirectory)?.FullName;
            if (string.IsNullOrWhiteSpace(pointDirectory) || !Directory.Exists(pointDirectory))
            {
                return;
            }

            switch (NormalizeEvidenceRetentionMode(retentionMode))
            {
                case InspectionSettingsValueKeys.EvidenceRetentionKeepLatestTask:
                    foreach (var candidate in Directory.GetDirectories(pointDirectory))
                    {
                        if (!string.Equals(candidate, currentTaskDirectory, StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.Delete(candidate, true);
                        }
                    }

                    break;
                case InspectionSettingsValueKeys.EvidenceRetentionKeepDays:
                    var cutoff = DateTime.Now.AddDays(-Math.Max(1, retentionDays));
                    foreach (var candidate in Directory.GetDirectories(pointDirectory))
                    {
                        if (string.Equals(candidate, currentTaskDirectory, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var lastWriteTime = Directory.GetLastWriteTime(candidate);
                        if (lastWriteTime < cutoff)
                        {
                            Directory.Delete(candidate, true);
                        }
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionEvidence",
                $"evidence retention skipped: pointId={pointId}, reason={NormalizeReason(ex.Message, "证据保留清理失败。")}");
        }
    }

    private static IEnumerable<InspectionTaskPointExecutionModel> BuildMissingPointQueue(
        IEnumerable<string> missingPointIds,
        int sequenceOffset)
    {
        return missingPointIds
            .Where(pointId => !string.IsNullOrWhiteSpace(pointId))
            .Select((pointId, index) => new InspectionTaskPointExecutionModel(
                pointId,
                pointId,
                pointId,
                sequenceOffset + index + 1,
                InspectionPointExecutionStatusModel.Failed,
                string.Empty,
                InspectionPointFailureCategoryModel.PointNotFound,
                "点位未命中当前巡检组。",
                false,
                false,
                "点位未命中策略快照"))
            .ToList();
    }

    private static ResolvedPointPolicy ResolveEffectivePolicy(
        PointWorkspaceItemModel point,
        InspectionSettingsSnapshot settings,
        InspectionScopePlanSettings? scopePlan = null)
    {
        var overridePolicy = settings.AlertStrategy.PointOverrides
            .FirstOrDefault(candidate => string.Equals(candidate.PointId, point.PointId, StringComparison.OrdinalIgnoreCase));
        var policy = overridePolicy?.Policy ?? settings.AlertStrategy.GlobalPolicy;
        var usesOverridePolicy = overridePolicy is not null;
        var alertTypes = overridePolicy is not null && !overridePolicy.UseGlobalAlertTypes
            ? overridePolicy.EnabledAlertTypeIds
            : settings.AlertStrategy.EnabledAlertTypeIds;
        var isFocusPoint = scopePlan?.FocusPointIds.Contains(point.PointId, StringComparer.OrdinalIgnoreCase) == true;

        return new ResolvedPointPolicy(
            policy,
            usesOverridePolicy,
            isFocusPoint,
            BuildPolicySnapshotSummary(policy, alertTypes, usesOverridePolicy, isFocusPoint));
    }

    private static ResolvedPointPolicy ResolveEffectivePolicy(
        PointWorkspaceItemModel point,
        InspectionSettingsSnapshot settings,
        bool isFocusPoint,
        bool usesOverridePolicy,
        string policySnapshotSummary)
    {
        var overridePolicy = settings.AlertStrategy.PointOverrides
            .FirstOrDefault(candidate => string.Equals(candidate.PointId, point.PointId, StringComparison.OrdinalIgnoreCase));
        var policy = overridePolicy?.Policy ?? settings.AlertStrategy.GlobalPolicy;
        var effectiveOverride = overridePolicy is not null || usesOverridePolicy;
        var alertTypes = overridePolicy is not null && !overridePolicy.UseGlobalAlertTypes
            ? overridePolicy.EnabledAlertTypeIds
            : settings.AlertStrategy.EnabledAlertTypeIds;

        return new ResolvedPointPolicy(
            policy,
            effectiveOverride,
            isFocusPoint,
            string.IsNullOrWhiteSpace(policySnapshotSummary)
                ? BuildPolicySnapshotSummary(policy, alertTypes, effectiveOverride, isFocusPoint)
                : policySnapshotSummary);
    }

    private static string BuildPolicySnapshotSummary(
        InspectionPointPolicySettings policy,
        IReadOnlyList<string> alertTypes,
        bool usesOverridePolicy,
        bool isFocusPoint)
    {
        var segments = new List<string>
        {
            policy.RequireOnlineStatusCheck ? "在线状态必检" : "在线状态按点位策略处理",
            policy.RequirePlaybackCheck ? "播放检查已启用" : "播放检查未启用",
            policy.EnableInterfaceAiDecision ? "接口判定预留" : "接口判定关闭",
            policy.EnableLocalScreenshotAnalysis ? "本地截图分析预留" : "本地截图分析关闭",
            policy.RouteAbnormalPointsToReviewWall ? "异常先上墙复核" : "异常直接进入派单链路",
            alertTypes.Count == 0 ? "告警类型待补充" : $"告警类型 {alertTypes.Count} 项"
        };

        if (usesOverridePolicy)
        {
            segments.Add("使用点位覆盖策略");
        }

        if (isFocusPoint)
        {
            segments.Add("重点关注点位");
        }

        return string.Join(" / ", segments);
    }

    private static string BuildTaskName(
        string pattern,
        InspectionTaskTypeModel taskType,
        string groupId,
        string pointId,
        string deviceCode,
        string? scopePlanId)
    {
        var now = DateTime.Now;
        var taskTypeCode = ResolveTaskTypeCode(taskType);
        var resolvedPattern = string.IsNullOrWhiteSpace(pattern)
            ? "AIIC-{yyyyMMdd}-{taskType}-{pointId}"
            : pattern.Trim();

        return resolvedPattern
            .Replace("{yyyyMMdd}", now.ToString("yyyyMMdd"), StringComparison.Ordinal)
            .Replace("{HHmmss}", now.ToString("HHmmss"), StringComparison.Ordinal)
            .Replace("{mode}", taskTypeCode, StringComparison.Ordinal)
            .Replace("{taskType}", taskTypeCode, StringComparison.Ordinal)
            .Replace("{pointId}", pointId, StringComparison.Ordinal)
            .Replace("{deviceCode}", deviceCode, StringComparison.Ordinal)
            .Replace("{groupId}", groupId, StringComparison.Ordinal)
            .Replace("{scopePlanId}", scopePlanId ?? "default-scope", StringComparison.Ordinal);
    }

    private static string ResolveTaskTypeCode(InspectionTaskTypeModel taskType)
    {
        return taskType switch
        {
            InspectionTaskTypeModel.SinglePoint => "single",
            InspectionTaskTypeModel.Batch => "batch",
            _ => "scope"
        };
    }

    private static InspectionTaskRecordModel CreateRejectedTask(
        string groupId,
        string groupName,
        string taskName,
        InspectionTaskTypeModel taskType,
        InspectionTaskStatusModel status,
        string summary)
    {
        return new InspectionTaskRecordModel(
            $"aitc-rejected-{DateTime.Now:yyyyMMddHHmmssfff}",
            groupId,
            groupName,
            taskName,
            taskType,
            InspectionTaskTriggerModel.Manual,
            status,
            DateTime.Now,
            null,
            DateTime.Now,
            null,
            DefaultScopePlanName,
            0,
            0,
            0,
            0,
            null,
            "--",
            summary,
            []);
    }

    private static InspectionPointModel CreatePoint(
        PointWorkspaceItemModel point,
        PointStagePlacementModel placement,
        int index,
        InspectionTaskPointExecutionModel? taskPoint)
    {
        var businessSummary = PointBusinessSummaryFactory.Create(point);
        var baseStatus = point.IsOnline == false
            ? InspectionPointStatusModel.PausedUntilRecovery
            : point.HasFault
                ? InspectionPointStatusModel.Fault
                : index == 0
                    ? InspectionPointStatusModel.Inspecting
                    : InspectionPointStatusModel.Normal;
        var status = ResolveRuntimeStatus(taskPoint, baseStatus);
        var completionStatus = ResolveCompletionStatus(taskPoint, baseStatus);
        var faultSummary = string.IsNullOrWhiteSpace(point.CurrentFaultSummary)
            ? taskPoint?.FailureReason ?? string.Empty
            : point.CurrentFaultSummary;
        var entersDispatchPool = taskPoint?.RouteToDispatchPoolReserved ?? point.EntersDispatchPool;

        return new InspectionPointModel(
            point.PointId,
            point.DeviceCode,
            point.PointName,
            point.UnitName,
            point.CurrentHandlingUnit,
            businessSummary.Longitude ?? point.Coordinate.Longitude,
            businessSummary.Latitude ?? point.Coordinate.Latitude,
            point.Coordinate.CanRenderOnMap,
            businessSummary.CoordinateStatus,
            point.Coordinate.RawLongitude,
            point.Coordinate.RawLatitude,
            point.Coordinate.Status,
            point.Coordinate.MapSource,
            placement.X,
            placement.Y,
            status,
            completionStatus,
            point.IsOnline,
            ContainsPlayableFlag(point.PlaybackStatusText) || point.IsOnline == true,
            LooksLikeImageAbnormal(point),
            point.IsOnline == true,
            faultSummary,
            point.LatestFaultTime?.ToString("yyyy-MM-dd HH:mm") ?? "--",
            entersDispatchPool,
            businessSummary,
            point.Coordinate.MapCoordinate?.Longitude,
            point.Coordinate.MapCoordinate?.Latitude,
            point.Coordinate.RegisteredCoordinate?.Longitude,
            point.Coordinate.RegisteredCoordinate?.Latitude,
            point.Coordinate.RegisteredCoordinateSystem,
            point.Coordinate.MapCoordinateSystem);
    }

    private static InspectionPointStatusModel ResolveRuntimeStatus(
        InspectionTaskPointExecutionModel? taskPoint,
        InspectionPointStatusModel baseStatus)
    {
        if (taskPoint is null)
        {
            return baseStatus;
        }

        return taskPoint.Status switch
        {
            InspectionPointExecutionStatusModel.Pending => InspectionPointStatusModel.Pending,
            InspectionPointExecutionStatusModel.Running => InspectionPointStatusModel.Inspecting,
            InspectionPointExecutionStatusModel.Succeeded => InspectionPointStatusModel.Normal,
            InspectionPointExecutionStatusModel.Failed => taskPoint.FailureCategory == InspectionPointFailureCategoryModel.DeviceOffline
                ? InspectionPointStatusModel.PausedUntilRecovery
                : InspectionPointStatusModel.Fault,
            InspectionPointExecutionStatusModel.Skipped => InspectionPointStatusModel.Silent,
            _ => baseStatus
        };
    }

    private static InspectionPointStatusModel ResolveCompletionStatus(
        InspectionTaskPointExecutionModel? taskPoint,
        InspectionPointStatusModel baseStatus)
    {
        if (taskPoint is null)
        {
            return baseStatus;
        }

        return taskPoint.Status switch
        {
            InspectionPointExecutionStatusModel.Succeeded => InspectionPointStatusModel.Normal,
            InspectionPointExecutionStatusModel.Failed => taskPoint.FailureCategory == InspectionPointFailureCategoryModel.DeviceOffline
                ? InspectionPointStatusModel.PausedUntilRecovery
                : InspectionPointStatusModel.Fault,
            InspectionPointExecutionStatusModel.Skipped => InspectionPointStatusModel.Silent,
            _ => baseStatus
        };
    }

    private static string NormalizeReason(string? message, string fallbackReason)
    {
        return string.IsNullOrWhiteSpace(message) ? fallbackReason : message.Trim();
    }

    private static bool ContainsPlayableFlag(string playbackStatusText)
    {
        return !string.IsNullOrWhiteSpace(playbackStatusText)
            && playbackStatusText.Contains("可播", StringComparison.Ordinal);
    }

    private static bool LooksLikeImageAbnormal(PointWorkspaceItemModel point)
    {
        return (!string.IsNullOrWhiteSpace(point.ImageStatusText)
                && point.ImageStatusText.Contains("异常", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(point.CurrentFaultType)
                && point.CurrentFaultType.Contains("画面", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(point.CurrentFaultSummary)
                && point.CurrentFaultSummary.Contains("画面", StringComparison.Ordinal));
    }

    private static bool IsTaskRunning(InspectionTaskStatusModel status)
        => status is InspectionTaskStatusModel.Pending or InspectionTaskStatusModel.Running;

    private static string DescribeBoard(InspectionTaskBoardModel board)
    {
        return board.CurrentTask is null
            ? "none"
            : $"{board.CurrentTask.TaskName} / {board.CurrentTask.Status} / recent={board.RecentTasks.Count}";
    }

    private sealed record ScopeSelectionResult(string TaskName, IReadOnlyList<PointWorkspaceItemModel> Points);

    private sealed record ResolvedPointPolicy(
        InspectionPointPolicySettings Policy,
        bool UsesOverridePolicy,
        bool IsFocusPoint,
        string PolicySnapshotSummary);
}
