using TianyiVision.Acis.Services.Contracts;
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
            return _demoService.GetDashboard();
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
}
