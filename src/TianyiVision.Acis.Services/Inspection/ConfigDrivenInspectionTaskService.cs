using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class ConfigDrivenInspectionTaskService : IInspectionTaskService
{
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

    private readonly IPointWorkspaceService _pointWorkspaceService;
    private readonly IInspectionTaskService _demoService;

    public ConfigDrivenInspectionTaskService(
        IPointWorkspaceService pointWorkspaceService,
        IInspectionTaskService demoService)
    {
        _pointWorkspaceService = pointWorkspaceService;
        _demoService = demoService;
    }

    public ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace()
    {
        var pointCollectionResponse = _pointWorkspaceService.GetPointCollection();
        if (!pointCollectionResponse.IsSuccess || pointCollectionResponse.Data.Count == 0)
        {
            MapPointSourceDiagnostics.WriteLines("InspectionMap", [
                "current map point source = pending",
                $"inspectionMap final source = pending, pointCount = 0, reason = {NormalizeReason(pointCollectionResponse.Message, "点位工作区返回 0 条，巡检页改为安全占位状态")}",
                "inspectionMap preview = none"
            ]);
            return ServiceResponse<InspectionWorkspaceSnapshot>.Success(CreatePendingWorkspace(), pointCollectionResponse.Message);
        }

        var points = pointCollectionResponse.Data.ToList();
        var stagePlacements = PointStageProjection.Project(points, InspectionStagePreset);

        var inspectionPoints = points
            .Select((point, index) => CreatePoint(point, stagePlacements[point.PointId], index))
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
            new InspectionGroupModel("g-ctyun-live", "CTYun 实时目录组", $"本轮加载 {points.Count} 个点位 · 串行巡检", true),
            new InspectionStrategyModel("08:30", "配置驱动", "串行执行", "上墙复核", "按巡检组配置"),
            new InspectionExecutionModel("1 / 1", "待执行", DateTime.Now.AddHours(4).ToString("HH:mm"), "当前点位已同步到巡检工作区，可继续查看明细。", true),
            new InspectionRunSummaryModel("CTYun 实时目录组", DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            inspectionPoints,
            recentFaults);

        var renderablePoints = points.Where(point => point.Coordinate.CanRenderOnMap).ToList();
        var sourceBreakdown = points
            .GroupBy(point => MapPointSourceDiagnostics.ClassifySourceTag(point.SourceTag), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var finalSource = ResolveFinalSource(sourceBreakdown);
        var finalReason = finalSource == "demo/fallback"
            ? NormalizeReason(pointCollectionResponse.Message, "点位工作区最终使用了 demo/fallback 点位")
            : renderablePoints.Count == 0
                ? "坐标清洗后可落点为 0"
                : NormalizeReason(pointCollectionResponse.Message, "真实点位已进入 AI 巡检地图中台");

        MapPointSourceDiagnostics.WriteLines("InspectionMap", [
            $"current map point source = {finalSource}",
            $"inspectionMap final source = {finalSource}, pointCount = {renderablePoints.Count}, reason = {finalReason}",
            $"inspectionMap sourceBreakdown = {MapPointSourceDiagnostics.SummarizeCounts(sourceBreakdown)}",
            $"inspectionMap preview = {BuildWorkspacePreview(renderablePoints.Count > 0 ? renderablePoints : points)}"
        ]);

        return ServiceResponse<InspectionWorkspaceSnapshot>.Success(new InspectionWorkspaceSnapshot([group]), pointCollectionResponse.Message);
    }

    public ServiceResponse<SingleInspectionTaskRecordModel> StartSinglePointInspection(string pointId)
    {
        var startedAt = DateTime.Now;
        var pointResponse = _pointWorkspaceService.GetPoint(pointId);
        if (!pointResponse.IsSuccess)
        {
            var failedRecord = CreateFailedRecord(pointId, pointId, pointId, startedAt);
            MapPointSourceDiagnostics.Write(
                "SingleInspection",
                $"single point inspection failed before task creation: pointId = {pointId}, reason = {NormalizeReason(pointResponse.Message, "点位未找到")}");
            return ServiceResponse<SingleInspectionTaskRecordModel>.Failure(failedRecord, pointResponse.Message);
        }

        var point = pointResponse.Data;
        var summary = PointBusinessSummaryFactory.Create(point);
        if (!string.Equals(summary.SourceType, "real", StringComparison.Ordinal))
        {
            var failedRecord = CreateFailedRecord(point.PointId, point.DeviceCode, point.PointName, startedAt);
            MapPointSourceDiagnostics.Write(
                "SingleInspection",
                $"single point inspection rejected: pointId = {point.PointId}, deviceCode = {point.DeviceCode}, source = {summary.SourceType}");
            return ServiceResponse<SingleInspectionTaskRecordModel>.Failure(failedRecord, "当前点位不是实时点位。");
        }

        var completedAt = DateTime.Now;
        var taskId = BuildTaskId();
        var completedRecord = new SingleInspectionTaskRecordModel(
            taskId,
            point.DeviceCode,
            point.PointName,
            InspectionTaskStatusModel.Completed,
            startedAt,
            completedAt,
            "待接入");

        MapPointSourceDiagnostics.WriteLines("SingleInspection", [
            $"single point inspection task created: taskId = {taskId}, pointId = {point.PointId}, deviceCode = {point.DeviceCode}, deviceName = {point.PointName}, status = {InspectionTaskStatusModel.Pending}",
            $"single point inspection task started: taskId = {taskId}, pointId = {point.PointId}, status = {InspectionTaskStatusModel.Running}",
            $"single point inspection task completed: taskId = {taskId}, pointId = {point.PointId}, status = {completedRecord.TaskStatus}, startedAt = {startedAt:yyyy-MM-dd HH:mm:ss}, finishedAt = {completedAt:yyyy-MM-dd HH:mm:ss}, resultSummary = {completedRecord.ResultSummary}"
        ]);

        return ServiceResponse<SingleInspectionTaskRecordModel>.Success(completedRecord);
    }

    private static InspectionPointModel CreatePoint(
        PointWorkspaceItemModel point,
        PointStagePlacementModel placement,
        int index)
    {
        var businessSummary = PointBusinessSummaryFactory.Create(point);
        var status = point.IsOnline == false
            ? InspectionPointStatusModel.PausedUntilRecovery
            : point.HasFault
                ? InspectionPointStatusModel.Fault
                : index == 0
                    ? InspectionPointStatusModel.Inspecting
                    : InspectionPointStatusModel.Normal;

        return new InspectionPointModel(
            point.PointId,
            point.DeviceCode,
            point.PointName,
            point.UnitName,
            point.CurrentHandlingUnit,
            point.Coordinate.Longitude,
            point.Coordinate.Latitude,
            point.Coordinate.CanRenderOnMap,
            point.Coordinate.StatusText,
            placement.X,
            placement.Y,
            status,
            status == InspectionPointStatusModel.Inspecting ? InspectionPointStatusModel.Normal : status,
            point.IsOnline,
            point.PlaybackStatusText.Contains("可播放", StringComparison.Ordinal) || point.IsOnline == true,
            point.CurrentFaultType.Contains("画面", StringComparison.Ordinal),
            point.IsOnline == true,
            point.CurrentFaultSummary,
            point.LatestFaultTime?.ToString("yyyy-MM-dd HH:mm") ?? "--",
            point.EntersDispatchPool,
            businessSummary);
    }

    private static string ResolveFinalSource(IReadOnlyDictionary<string, int> sourceBreakdown)
    {
        var realCount = sourceBreakdown.GetValueOrDefault("real");
        var demoCount = sourceBreakdown.GetValueOrDefault("demo");

        if (realCount > 0 && demoCount == 0)
        {
            return "real";
        }

        if (demoCount > 0 && realCount == 0)
        {
            return "demo/fallback";
        }

        return demoCount > 0 ? "mixed" : "unknown";
    }

    private static string BuildWorkspacePreview(IEnumerable<PointWorkspaceItemModel> points)
    {
        var preview = points
            .Take(10)
            .Select(point => $"{point.PointName} [PointId={point.PointId}, DeviceCode={point.DeviceCode}, source={MapPointSourceDiagnostics.ClassifySourceTag(point.SourceTag)}]")
            .ToList();

        return preview.Count == 0 ? "none" : string.Join("; ", preview);
    }

    private static string BuildInspectionPreview(IEnumerable<InspectionPointModel> points)
    {
        var preview = points
            .Take(10)
            .Select(point => $"{point.Name} [PointId={point.Id}, DeviceCode={point.DeviceCode}]")
            .ToList();

        return preview.Count == 0 ? "none" : string.Join("; ", preview);
    }

    private static InspectionWorkspaceSnapshot CreatePendingWorkspace()
    {
        return new InspectionWorkspaceSnapshot([
            new InspectionGroupWorkspaceModel(
                new InspectionGroupModel("g-ctyun-live", "CTYun 实时目录组", "点位状态待接入", true),
                new InspectionStrategyModel("待接入", "待接入", "待接入", "待接入", "待接入"),
                new InspectionExecutionModel("待接入", "待接入", "待接入", "当前点位状态待接入，页面保留工作区骨架。", false),
                new InspectionRunSummaryModel("CTYun 实时目录组", "待接入"),
                "待接入",
                [],
                [])
        ]);
    }

    private static string BuildTaskId()
        => $"sip-{DateTime.Now:yyyyMMddHHmmssfff}";

    private static SingleInspectionTaskRecordModel CreateFailedRecord(
        string pointId,
        string deviceCode,
        string deviceName,
        DateTime startedAt)
    {
        var taskId = $"{BuildTaskId()}-failed";
        return new SingleInspectionTaskRecordModel(
            taskId,
            string.IsNullOrWhiteSpace(deviceCode) ? pointId : deviceCode,
            string.IsNullOrWhiteSpace(deviceName) ? pointId : deviceName,
            InspectionTaskStatusModel.Failed,
            startedAt,
            startedAt,
            "待接入");
    }

    private static string NormalizeReason(string? message, string fallbackReason)
    {
        return string.IsNullOrWhiteSpace(message) ? fallbackReason : message.Trim();
    }
}
