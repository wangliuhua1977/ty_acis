using TianyiVision.Acis.Services.Contracts;
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
            return _demoService.GetWorkspace();
        }

        var points = pointCollectionResponse.Data.ToList();
        var stagePlacements = PointStageProjection.Project(points, InspectionStagePreset);

        var inspectionPoints = points
            .Select((point, index) => CreatePoint(point, stagePlacements[point.PointId], index))
            .ToList();
        var recentFaults = pointCollectionResponse.Data
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

        return ServiceResponse<InspectionWorkspaceSnapshot>.Success(new InspectionWorkspaceSnapshot([group]), pointCollectionResponse.Message);
    }

    private static InspectionPointModel CreatePoint(
        PointWorkspaceItemModel point,
        PointStagePlacementModel placement,
        int index)
    {
        var status = !point.IsOnline
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
            point.PlaybackStatusText.Contains("可播放", StringComparison.Ordinal) || point.IsOnline,
            point.CurrentFaultType.Contains("画面", StringComparison.Ordinal),
            point.IsOnline,
            point.CurrentFaultSummary,
            point.LatestFaultTime?.ToString("yyyy-MM-dd HH:mm") ?? "--",
            point.EntersDispatchPool);
    }
}
