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
            var demoResponse = _demoService.GetDashboard();
            var demoSnapshot = demoResponse.Data;
            MapPointSourceDiagnostics.WriteLines("HomeMap", [
                $"current map point source = demo/fallback",
                $"homeMap final source = demo/fallback, pointCount = {demoSnapshot.MapPoints.Count(point => point.CanRenderOnMap)}, reason = {NormalizeReason(pointCollectionResponse.Message, "点位工作区返回 0 条，页面绑定改用 demo 主舞台")}",
                $"homeMap preview = {BuildHomePreview(demoSnapshot.MapPoints)}"
            ]);
            return demoResponse;
        }

        var points = pointCollectionResponse.Data.ToList();
        var stagePlacements = PointStageProjection.Project(points, HomeStagePreset);
        var recentFaults = pointCollectionResponse.Data
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

        var snapshot = new HomeDashboardSnapshot(
            $"当前巡检点位 {pointCollectionResponse.Data.Count} 个",
            $"{points.Count(point => point.IsOnline)} / {points.Count}",
            recentFaults.Count == 0 ? "当前未发现待跟进故障点位。" : $"当前故障点位 {recentFaults.Count} 个，优先关注最新告警。",
            points.Any(point => !point.Coordinate.CanRenderOnMap)
                ? $"存在 {points.Count(point => !point.Coordinate.CanRenderOnMap)} 个未定位点位，可从未定位入口继续查看。"
                : "当前点位均已具备地图落点。",
            mapPoints,
            visibleRecentFaults);

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

        return ServiceResponse<HomeDashboardSnapshot>.Success(snapshot, pointCollectionResponse.Message);
    }

    private static HomeMapPointModel CreateMapPoint(
        PointWorkspaceItemModel point,
        PointStagePlacementModel placement)
    {
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
            point.HasFault ? "故障关联" : point.OnlineStatusText,
            point.CurrentFaultType,
            point.CurrentFaultSummary,
            point.LatestFaultTime?.ToString("yyyy-MM-dd HH:mm") ?? "--",
            point.HasFault);
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

    private static string NormalizeReason(string? message, string fallbackReason)
    {
        return string.IsNullOrWhiteSpace(message) ? fallbackReason : message.Trim();
    }
}
