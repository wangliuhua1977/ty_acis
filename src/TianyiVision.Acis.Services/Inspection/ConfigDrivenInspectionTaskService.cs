using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class ConfigDrivenInspectionTaskService : IInspectionTaskService
{
    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IAlertQueryService _alertQueryService;
    private readonly IInspectionTaskService _demoService;

    public ConfigDrivenInspectionTaskService(
        IDeviceCatalogService deviceCatalogService,
        IAlertQueryService alertQueryService,
        IInspectionTaskService demoService)
    {
        _deviceCatalogService = deviceCatalogService;
        _alertQueryService = alertQueryService;
        _demoService = demoService;
    }

    public ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace()
    {
        var devicesResponse = _deviceCatalogService.GetDevices();
        if (!devicesResponse.IsSuccess || devicesResponse.Data.Count == 0)
        {
            return _demoService.GetWorkspace();
        }

        var devices = devicesResponse.Data.Take(12).ToList();
        var endTime = DateTime.Now;
        var startTime = endTime.AddHours(-72);

        var aiAlerts = _alertQueryService.GetAiAlerts(new AiAlertQueryDto(
            null,
            startTime,
            endTime,
            null,
            null,
            1,
            20)).Data;

        var deviceAlerts = devices
            .SelectMany(device => _alertQueryService.GetDeviceAlerts(new DeviceAlertQueryDto(
                device.DeviceCode,
                startTime,
                endTime,
                null,
                null,
                1,
                10)).Data)
            .ToList();

        var alertByPoint = aiAlerts
            .Concat(deviceAlerts)
            .GroupBy(item => item.PointId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.LatestDetectedAt).First());

        var points = devices
            .Select((device, index) => CreatePoint(device, alertByPoint, index))
            .ToList();

        var group = new InspectionGroupWorkspaceModel(
            new InspectionGroupModel("g-ctyun-live", "CTYun 实时目录组", $"本轮加载 {devices.Count} 个点位 · 目录/告警真实接入", true),
            new InspectionStrategyModel("08:30", "配置驱动", "串行执行", "上墙复核", "按巡检组配置"),
            new InspectionExecutionModel("1 / 1", "目录已加载", DateTime.Now.AddHours(4).ToString("HH:mm"), "页面壳层已从真实设备目录入口生成点位。", true),
            new InspectionRunSummaryModel("CTYun 实时目录组", DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            points,
            points
                .Where(point => point.Status is InspectionPointStatusModel.Fault or InspectionPointStatusModel.PausedUntilRecovery)
                .Take(5)
                .Select(point => new InspectionRecentFaultModel(point.Id, point.Name, ResolveFaultType(point), point.LastFaultTime))
                .ToList());

        return ServiceResponse<InspectionWorkspaceSnapshot>.Success(new InspectionWorkspaceSnapshot([group]), devicesResponse.Message);
    }

    private static InspectionPointModel CreatePoint(
        DeviceListItemDto device,
        IReadOnlyDictionary<string, FaultAlertDto> alertByPoint,
        int index)
    {
        var hasAlert = alertByPoint.TryGetValue(device.DeviceCode, out var alert);
        var status = !device.IsOnline
            ? InspectionPointStatusModel.PausedUntilRecovery
            : hasAlert
                ? InspectionPointStatusModel.Fault
                : index == 0
                    ? InspectionPointStatusModel.Inspecting
                    : InspectionPointStatusModel.Normal;

        return new InspectionPointModel(
            device.DeviceCode,
            device.DeviceName,
            string.IsNullOrWhiteSpace(device.HandlingUnit) ? "CTYun设备目录" : device.HandlingUnit,
            "待接派单链路",
            100 + (index % 4) * 230,
            120 + (index / 4) * 110,
            status,
            status == InspectionPointStatusModel.Inspecting ? InspectionPointStatusModel.Normal : status,
            device.IsOnline,
            device.IsOnline,
            hasAlert && alert!.FaultType.Contains("画面", StringComparison.Ordinal),
            device.IsOnline,
            hasAlert ? alert!.LatestDetectedAt.ToString("yyyy-MM-dd HH:mm") : "--",
            hasAlert);
    }

    private static string ResolveFaultType(InspectionPointModel point)
    {
        if (!point.IsOnline)
        {
            return "设备离线";
        }

        if (point.IsImageAbnormal)
        {
            return "画面异常";
        }

        if (!point.IsPlayable)
        {
            return "播放失败";
        }

        return "设备告警";
    }
}
