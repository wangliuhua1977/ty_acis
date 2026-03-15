using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Home;

public sealed class ConfigDrivenHomeDashboardService : IHomeDashboardService
{
    private static readonly PointStageLayoutPreset HomeStagePreset = new(
        120,
        120,
        980,
        620,
        1010,
        150,
        2,
        120,
        140);

    private readonly IPointWorkspaceService _pointWorkspaceService;
    private readonly IHomeDashboardService _demoService;

    public ConfigDrivenHomeDashboardService(
        IPointWorkspaceService pointWorkspaceService,
        IHomeDashboardService demoService)
    {
        _pointWorkspaceService = pointWorkspaceService;
        _demoService = demoService;
    }

    public ServiceResponse<HomeDashboardSnapshot> GetDashboard()
    {
        var pointCollectionResponse = _pointWorkspaceService.GetPointCollection();
        if (!pointCollectionResponse.IsSuccess || pointCollectionResponse.Data.Count == 0)
        {
            MapPointSourceDiagnostics.WriteLines("HomeMap", [
                "current map point source = pending",
                $"homeMap final source = pending, pointCount = 0, reason = {NormalizeReason(pointCollectionResponse.Message, "点位工作区返回 0 条，首页改为安全占位状态")}",
                "homeMap preview = none"
            ]);
            MapPointSourceDiagnostics.WriteLines("HomeHeaderMetrics", [
                "homeHeaderMetrics final source = pending",
                "homeHeaderMetrics values = inspectionTasks=待接入, faults=待接入, outstanding=待接入, pendingReview=待接入, pendingDispatch=待接入",
                "homeHeaderMetrics source = inspectionTasks:待接入, faults:待接入, outstanding:待接入, pendingReview:待接入, pendingDispatch:待接入"
            ]);
            return ServiceResponse<HomeDashboardSnapshot>.Success(CreatePendingSnapshot(), pointCollectionResponse.Message);
        }

        var points = pointCollectionResponse.Data.ToList();
        var stagePlacements = PointStageProjection.Project(points, HomeStagePreset);
        var hasPendingFaultStatus = points.Any(point => point.FaultStatus == PointFaultObservationStatus.Pending);
        var recentFaults = points
            .Where(point => point.HasFault && point.LatestFaultTime.HasValue)
            .OrderByDescending(point => point.LatestFaultTime)
            .ToList();
        var mapPoints = points
            .Select(point => CreateMapPoint(point, stagePlacements[point.PointId]))
            .ToList();
        var visiblePointIds = points.Select(point => point.PointId).ToHashSet(StringComparer.Ordinal);
        var visibleRecentFaults = recentFaults
            .Where(point => visiblePointIds.Contains(point.PointId))
            .Take(4)
            .Select(point => new HomeRecentFaultModel(
                point.PointId,
                point.PointName,
                point.CurrentFaultType,
                point.LatestFaultTime?.ToString("yyyy-MM-dd HH:mm") ?? "--"))
            .ToList();
        var pendingDispatchCount = hasPendingFaultStatus
            ? 0
            : points.Count(point => point.FaultStatus == PointFaultObservationStatus.HasFault && point.EntersDispatchPool);
        var headerMetrics = BuildHeaderMetrics(points, pendingDispatchCount, hasPendingFaultStatus);

        var snapshot = new HomeDashboardSnapshot(
            $"当前点位 {points.Count} 个",
            BuildExecutionProgress(points),
            recentFaults.Count == 0
                ? "当前未发现待跟进故障点位。"
                : $"当前故障点位 {recentFaults.Count} 个，优先关注最新告警。",
            hasPendingFaultStatus
                ? "待派单口径待接入。"
                : pendingDispatchCount == 0
                ? "当前暂无待派单点位。"
                : $"当前待派单点位 {pendingDispatchCount} 个。",
            mapPoints,
            visibleRecentFaults,
            headerMetrics);

        var renderablePoints = points.Where(point => point.Coordinate.CanRenderOnMap).ToList();
        var sourceBreakdown = points
            .GroupBy(point => MapPointSourceDiagnostics.ClassifySourceTag(point.SourceTag), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var finalSource = ResolveFinalSource(sourceBreakdown);
        var finalReason = finalSource == "demo/fallback"
            ? NormalizeReason(pointCollectionResponse.Message, "点位工作区最终使用了 demo/fallback 点位")
            : renderablePoints.Count == 0
                ? "坐标清洗后可落点为 0"
                : NormalizeReason(pointCollectionResponse.Message, "真实点位已进入首页地图");

        MapPointSourceDiagnostics.WriteLines("HomeMap", [
            $"current map point source = {finalSource}",
            $"homeMap final source = {finalSource}, pointCount = {renderablePoints.Count}, reason = {finalReason}",
            $"homeMap sourceBreakdown = {MapPointSourceDiagnostics.SummarizeCounts(sourceBreakdown)}",
            $"homeMap preview = {BuildWorkspacePreview(renderablePoints.Count > 0 ? renderablePoints : points)}"
        ]);

        MapPointSourceDiagnostics.WriteLines("HomeHeaderMetrics", [
            $"homeHeaderMetrics final source = {finalSource}",
            $"homeHeaderMetrics values = inspectionTasks={headerMetrics.InspectionTasks}, faults={headerMetrics.Faults}, outstanding={headerMetrics.Outstanding}, pendingReview={headerMetrics.PendingReview}, pendingDispatch={headerMetrics.PendingDispatch}",
            hasPendingFaultStatus
                ? "homeHeaderMetrics source = inspectionTasks:待接入, faults:待接入, outstanding:待接入, pendingReview:待接入, pendingDispatch:待接入"
                : "homeHeaderMetrics source = inspectionTasks:待接入, faults:pointCollection(realStatus), outstanding:待接入, pendingReview:待接入, pendingDispatch:pointCollection(realStatus)"
        ]);

        return ServiceResponse<HomeDashboardSnapshot>.Success(snapshot, pointCollectionResponse.Message);
    }

    private static HomeMapPointModel CreateMapPoint(
        PointWorkspaceItemModel point,
        PointStagePlacementModel placement)
    {
        var businessSummary = PointBusinessSummaryFactory.Create(point);
        var kind = point.HasFault
            ? HomeMapPointKindModel.Fault
            : !point.Coordinate.CanRenderOnMap
                ? HomeMapPointKindModel.Key
                : HomeMapPointKindModel.Normal;

        return new HomeMapPointModel(
            point.PointId,
            point.DeviceCode,
            point.PointName,
            point.UnitName,
            point.Coordinate.Longitude,
            point.Coordinate.Latitude,
            point.Coordinate.CanRenderOnMap,
            point.Coordinate.StatusText,
            kind,
            placement.X,
            placement.Y,
            businessSummary.OnlineStatus,
            businessSummary.FaultType,
            businessSummary.StatusSummary,
            point.LatestFaultTime?.ToString("yyyy-MM-dd HH:mm") ?? "--",
            point.HasFault,
            businessSummary);
    }

    private static HomeHeaderMetricsModel BuildHeaderMetrics(
        IReadOnlyCollection<PointWorkspaceItemModel> points,
        int pendingDispatchCount,
        bool hasPendingFaultStatus)
    {
        return new HomeHeaderMetricsModel(
            "待接入",
            hasPendingFaultStatus
                ? "待接入"
                : points.Count(point => point.FaultStatus == PointFaultObservationStatus.HasFault).ToString(),
            "待接入",
            "待接入",
            hasPendingFaultStatus ? "待接入" : pendingDispatchCount.ToString());
    }

    private static string BuildExecutionProgress(IReadOnlyCollection<PointWorkspaceItemModel> points)
    {
        return points.All(point => point.IsOnline.HasValue)
            ? $"{points.Count(point => point.IsOnline == true)} / {points.Count}"
            : "待接入";
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

    private static string BuildHomePreview(IEnumerable<HomeMapPointModel> points)
    {
        var preview = points
            .Take(10)
            .Select(point => $"{point.Name} [PointId={point.Id}, DeviceCode={point.DeviceCode}]")
            .ToList();

        return preview.Count == 0 ? "none" : string.Join("; ", preview);
    }

    private static HomeDashboardSnapshot CreatePendingSnapshot()
    {
        return new HomeDashboardSnapshot(
            "当前点位待接入",
            "待接入",
            "待接入",
            "待接入",
            [],
            [],
            new HomeHeaderMetricsModel("待接入", "待接入", "待接入", "待接入", "待接入"));
    }

    private static string NormalizeReason(string? message, string fallbackReason)
    {
        return string.IsNullOrWhiteSpace(message) ? fallbackReason : message.Trim();
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
