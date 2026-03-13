using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Dispatch;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class ConfigDrivenInspectionTaskService : IInspectionTaskService
{
    private readonly IDeviceWorkspaceService _deviceWorkspaceService;
    private readonly IFaultPoolService _faultPoolService;
    private readonly IInspectionTaskService _demoService;

    public ConfigDrivenInspectionTaskService(
        IDeviceWorkspaceService deviceWorkspaceService,
        IFaultPoolService faultPoolService,
        IInspectionTaskService demoService)
    {
        _deviceWorkspaceService = deviceWorkspaceService;
        _faultPoolService = faultPoolService;
        _demoService = demoService;
    }

    public ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace()
    {
        var devicePoolResponse = _deviceWorkspaceService.GetDevicePool();
        if (!devicePoolResponse.IsSuccess || devicePoolResponse.Data.Count == 0)
        {
            return _demoService.GetWorkspace();
        }

        var devices = devicePoolResponse.Data.Take(12).ToList();
        var pointDetails = devices
            .Select(device =>
            {
                var detail = _deviceWorkspaceService.GetPointDetail(device.DeviceCode);
                return new KeyValuePair<string, DevicePointDetailModel>(
                    device.DeviceCode,
                    detail.IsSuccess ? detail.Data : CreateFallbackDetail(device));
            })
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var faultPool = _faultPoolService.GetFaultPool().Data;
        var faultByPoint = faultPool
            .GroupBy(item => item.PointId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.LatestDetectedAt).First(), StringComparer.Ordinal);

        var points = devices
            .Select((device, index) => CreatePoint(device, pointDetails[device.DeviceCode], faultByPoint, index))
            .ToList();

        var group = new InspectionGroupWorkspaceModel(
            new InspectionGroupModel("g-ctyun-live", "CTYun 实时目录组", $"本轮加载 {devices.Count} 个点位 · 设备池/故障池真实接入", true),
            new InspectionStrategyModel("08:30", "配置驱动", "串行执行", "上墙复核", "按巡检组配置"),
            new InspectionExecutionModel("1 / 1", "设备池已加载", DateTime.Now.AddHours(4).ToString("HH:mm"), "当前巡检页已优先使用真实设备池与点位详情入口。", true),
            new InspectionRunSummaryModel("CTYun 实时目录组", DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            points,
            points
                .Where(point => point.Status is InspectionPointStatusModel.Fault or InspectionPointStatusModel.PausedUntilRecovery)
                .Take(5)
                .Select(point => new InspectionRecentFaultModel(point.Id, point.Name, ResolveFaultType(point), point.LastFaultTime))
                .ToList());

        return ServiceResponse<InspectionWorkspaceSnapshot>.Success(new InspectionWorkspaceSnapshot([group]), devicePoolResponse.Message);
    }

    private static InspectionPointModel CreatePoint(
        DevicePoolItemModel device,
        DevicePointDetailModel pointDetail,
        IReadOnlyDictionary<string, FaultPoolItemModel> faultByPoint,
        int index)
    {
        var hasAlert = faultByPoint.TryGetValue(device.DeviceCode, out var alert);
        var status = !pointDetail.IsOnline
            ? InspectionPointStatusModel.PausedUntilRecovery
            : hasAlert
                ? InspectionPointStatusModel.Fault
                : index == 0
                    ? InspectionPointStatusModel.Inspecting
                    : InspectionPointStatusModel.Normal;

        return new InspectionPointModel(
            device.DeviceCode,
            pointDetail.PointName,
            pointDetail.UnitName,
            alert?.CurrentHandlingUnit ?? "待接派单链路",
            100 + (index % 4) * 230,
            120 + (index / 4) * 110,
            status,
            status == InspectionPointStatusModel.Inspecting ? InspectionPointStatusModel.Normal : status,
            pointDetail.IsOnline,
            pointDetail.IsOnline,
            hasAlert && alert!.FaultType.Contains("画面", StringComparison.Ordinal),
            pointDetail.IsOnline,
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
