using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Home;

public sealed class ConfigDrivenHomeDashboardService : IHomeDashboardService
{
    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IAlertQueryService _alertQueryService;
    private readonly IHomeDashboardService _demoService;

    public ConfigDrivenHomeDashboardService(
        IDeviceCatalogService deviceCatalogService,
        IAlertQueryService alertQueryService,
        IHomeDashboardService demoService)
    {
        _deviceCatalogService = deviceCatalogService;
        _alertQueryService = alertQueryService;
        _demoService = demoService;
    }

    public ServiceResponse<HomeDashboardSnapshot> GetDashboard()
    {
        var devicesResponse = _deviceCatalogService.GetDevices();
        if (!devicesResponse.IsSuccess || devicesResponse.Data.Count == 0)
        {
            return _demoService.GetDashboard();
        }

        var endTime = DateTime.Now;
        var startTime = endTime.AddHours(-72);
        var aiAlerts = _alertQueryService.GetAiAlerts(new AiAlertQueryDto(
            null,
            startTime,
            endTime,
            null,
            null,
            1,
            20));

        var deviceAlerts = devicesResponse.Data
            .Take(6)
            .SelectMany(device => _alertQueryService.GetDeviceAlerts(new DeviceAlertQueryDto(
                device.DeviceCode,
                startTime,
                endTime,
                null,
                null,
                1,
                10)).Data)
            .ToList();

        var latestAlerts = aiAlerts.Data
            .Concat(deviceAlerts)
            .OrderByDescending(item => item.LatestDetectedAt)
            .ToList();

        var devices = devicesResponse.Data.Take(8).ToList();
        var mapPoints = devices
            .Select((device, index) => CreateMapPoint(device, latestAlerts, index))
            .ToList();

        var snapshot = new HomeDashboardSnapshot(
            $"CTYun 设备目录接入 · 本轮加载 {devicesResponse.Data.Count} 个点位",
            $"{devices.Count(device => device.IsOnline)} / {devices.Count}",
            latestAlerts.Count == 0 ? "暂无 CTYun 告警，当前保留页面壳层。" : $"最近 72 小时共发现 {latestAlerts.Count} 条告警。",
            $"待派单链路仍走 demo，本轮重点验证目录与告警查询。",
            mapPoints,
            latestAlerts
                .Take(4)
                .Select(item => new HomeRecentFaultModel(
                    item.PointId,
                    item.PointName,
                    item.FaultType,
                    item.LatestDetectedAt.ToString("yyyy-MM-dd HH:mm")))
                .ToList());

        return ServiceResponse<HomeDashboardSnapshot>.Success(snapshot, devicesResponse.Message);
    }

    private static HomeMapPointModel CreateMapPoint(
        DeviceListItemDto device,
        IReadOnlyList<FaultAlertDto> alerts,
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
            device.DeviceName,
            string.IsNullOrWhiteSpace(device.HandlingUnit) ? "CTYun设备目录" : device.HandlingUnit,
            kind,
            140 + (index % 4) * 260,
            150 + (index / 4) * 170,
            latestAlert is not null ? "告警" : device.IsOnline ? "在线" : "离线",
            latestAlert?.FaultType ?? "无故障",
            latestAlert?.Content ?? "当前通过 CTYun 目录数据填充首页地图壳层。",
            latestAlert?.LatestDetectedAt.ToString("yyyy-MM-dd HH:mm") ?? "--",
            latestAlert is not null);
    }
}
