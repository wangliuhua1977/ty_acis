using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Dispatch;

namespace TianyiVision.Acis.Services.Home;

public sealed class ConfigDrivenHomeDashboardService : IHomeDashboardService
{
    private readonly IDeviceWorkspaceService _deviceWorkspaceService;
    private readonly IFaultPoolService _faultPoolService;
    private readonly IHomeDashboardService _demoService;

    public ConfigDrivenHomeDashboardService(
        IDeviceWorkspaceService deviceWorkspaceService,
        IFaultPoolService faultPoolService,
        IHomeDashboardService demoService)
    {
        _deviceWorkspaceService = deviceWorkspaceService;
        _faultPoolService = faultPoolService;
        _demoService = demoService;
    }

    public ServiceResponse<HomeDashboardSnapshot> GetDashboard()
    {
        var devicePoolResponse = _deviceWorkspaceService.GetDevicePool();
        if (!devicePoolResponse.IsSuccess || devicePoolResponse.Data.Count == 0)
        {
            return _demoService.GetDashboard();
        }

        var faultPoolResponse = _faultPoolService.GetFaultPool();
        var latestAlerts = faultPoolResponse.Data
            .OrderByDescending(item => item.LatestDetectedAt)
            .ToList();

        var devices = devicePoolResponse.Data.Take(8).ToList();
        var pointDetails = devices
            .Select(device =>
            {
                var detail = _deviceWorkspaceService.GetPointDetail(device.DeviceCode);
                return new KeyValuePair<string, DevicePointDetailModel>(
                    device.DeviceCode,
                    detail.IsSuccess ? detail.Data : CreateFallbackDetail(device));
            })
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var mapPoints = devices
            .Select((device, index) => CreateMapPoint(device, pointDetails[device.DeviceCode], latestAlerts, index))
            .ToList();
        var visiblePointIds = mapPoints
            .Select(point => point.Id)
            .ToHashSet(StringComparer.Ordinal);
        var visibleRecentFaults = latestAlerts
            .Where(item => visiblePointIds.Contains(item.PointId))
            .Take(4)
            .Select(item => new HomeRecentFaultModel(
                item.PointId,
                item.PointName,
                item.FaultType,
                item.LatestDetectedAt.ToString("yyyy-MM-dd HH:mm")))
            .ToList();

        var snapshot = new HomeDashboardSnapshot(
            $"CTYun 设备池接入 · 本轮加载 {devicePoolResponse.Data.Count} 个点位",
            $"{devices.Count(device => device.IsOnline)} / {devices.Count}",
            latestAlerts.Count == 0 ? "暂无 CTYun 故障池数据，当前保留页面壳层。" : $"最近时间窗聚合出 {latestAlerts.Count} 条故障项。",
            $"首页已通过真实设备池与真实故障池入口生成态势数据。",
            mapPoints,
            visibleRecentFaults);

        return ServiceResponse<HomeDashboardSnapshot>.Success(snapshot, devicePoolResponse.Message);
    }

    private static HomeMapPointModel CreateMapPoint(
        DevicePoolItemModel device,
        DevicePointDetailModel pointDetail,
        IReadOnlyList<FaultPoolItemModel> alerts,
        int index)
    {
        var latestAlert = alerts.FirstOrDefault(item => item.PointId == device.DeviceCode);
        var kind = latestAlert is not null
            ? HomeMapPointKindModel.Fault
            : device.IsOnline
                ? HomeMapPointKindModel.Normal
                : HomeMapPointKindModel.Inspecting;

        return new HomeMapPointModel(
            device.DeviceCode,
            pointDetail.PointName,
            pointDetail.UnitName,
            kind,
            140 + (index % 4) * 260,
            150 + (index / 4) * 170,
            latestAlert is not null ? "告警" : pointDetail.OnlineStatusText,
            latestAlert?.FaultType ?? "无故障",
            latestAlert?.FaultSummary ?? pointDetail.DetailSummary,
            latestAlert?.LatestDetectedAt.ToString("yyyy-MM-dd HH:mm") ?? "--",
            latestAlert is not null);
    }

    private static DevicePointDetailModel CreateFallbackDetail(DevicePoolItemModel device)
    {
        return new DevicePointDetailModel(
            device.DeviceCode,
            device.DeviceName,
            device.DeviceType,
            device.UnitName,
            device.AreaName,
            device.AreaName,
            device.Longitude,
            device.Latitude,
            device.IsOnline,
            device.OnlineStatusText,
            "待接视频巡检",
            "待接 AI 判定",
            $"点位详情获取失败，当前回退到设备池摘要。来源：{device.SourceTag}。",
            device.SourceTag);
    }
}
