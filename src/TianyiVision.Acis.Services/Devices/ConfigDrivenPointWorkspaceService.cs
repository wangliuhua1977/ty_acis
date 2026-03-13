using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Dispatch;

namespace TianyiVision.Acis.Services.Devices;

public sealed class ConfigDrivenPointWorkspaceService : IPointWorkspaceService
{
    private readonly IDeviceWorkspaceService _deviceWorkspaceService;
    private readonly IFaultPoolService _faultPoolService;

    public ConfigDrivenPointWorkspaceService(
        IDeviceWorkspaceService deviceWorkspaceService,
        IFaultPoolService faultPoolService)
    {
        _deviceWorkspaceService = deviceWorkspaceService;
        _faultPoolService = faultPoolService;
    }

    public ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>> GetPointCollection()
    {
        var devicePoolResponse = _deviceWorkspaceService.GetDevicePool();
        if (!devicePoolResponse.IsSuccess || devicePoolResponse.Data.Count == 0)
        {
            return ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>>.Failure([], devicePoolResponse.Message);
        }

        var faultPoolResponse = _faultPoolService.GetFaultPool();
        var latestFaultByPoint = faultPoolResponse.Data
            .GroupBy(item => item.PointId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.LatestDetectedAt).First(),
                StringComparer.Ordinal);

        var points = devicePoolResponse.Data
            .Select(device => BuildPoint(device, latestFaultByPoint.GetValueOrDefault(device.PointId)))
            .OrderByDescending(point => point.HasFault)
            .ThenBy(point => point.Coordinate.CanRenderOnMap ? 0 : 1)
            .ThenBy(point => point.PointName, StringComparer.Ordinal)
            .ToList();

        if (points.Count == 0)
        {
            return ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>>.Failure([], "点位工作区未生成可用数据。");
        }

        return ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>>.Success(
            points,
            CombineMessages(devicePoolResponse.Message, faultPoolResponse.IsSuccess ? string.Empty : faultPoolResponse.Message));
    }

    public ServiceResponse<PointWorkspaceItemModel> GetPoint(string pointId)
    {
        var collectionResponse = GetPointCollection();
        if (!collectionResponse.IsSuccess)
        {
            return ServiceResponse<PointWorkspaceItemModel>.Failure(Empty(pointId), collectionResponse.Message);
        }

        var point = collectionResponse.Data.FirstOrDefault(item => item.PointId == pointId);
        return point is null
            ? ServiceResponse<PointWorkspaceItemModel>.Failure(Empty(pointId), $"未找到点位 {pointId}。")
            : ServiceResponse<PointWorkspaceItemModel>.Success(point, collectionResponse.Message);
    }

    private PointWorkspaceItemModel BuildPoint(
        DevicePoolItemModel device,
        FaultPoolItemModel? fault)
    {
        var detailResponse = _deviceWorkspaceService.GetPointDetail(device.PointId);
        var detail = detailResponse.IsSuccess ? detailResponse.Data : CreateFallbackDetail(device);
        var hasFault = fault is not null || !detail.IsOnline;
        var currentFaultType = fault?.FaultType
            ?? (!detail.IsOnline ? "设备离线" : "无故障");
        var currentFaultSummary = fault?.FaultSummary
            ?? (!detail.IsOnline
                ? "设备目录当前显示离线，待告警链路补充故障摘要。"
                : detail.DetailSummary);

        return new PointWorkspaceItemModel(
            detail.PointId,
            detail.DeviceCode,
            detail.PointName,
            detail.DeviceType,
            detail.UnitName,
            detail.AreaName,
            fault?.CurrentHandlingUnit ?? detail.UnitName,
            detail.Coordinate,
            detail.IsOnline,
            detail.OnlineStatusText,
            detail.PlaybackStatusText,
            detail.ImageStatusText,
            fault?.LatestDetectedAt,
            currentFaultType,
            currentFaultSummary,
            hasFault,
            fault?.EntersDispatchPool ?? false,
            detail.DetailSummary,
            detail.SourceTag);
    }

    private static DevicePointDetailModel CreateFallbackDetail(DevicePoolItemModel device)
    {
        return new DevicePointDetailModel(
            device.PointId,
            device.DeviceCode,
            device.DeviceName,
            device.DeviceType,
            device.UnitName,
            device.AreaName,
            device.AreaName,
            device.Coordinate,
            device.IsOnline,
            device.OnlineStatusText,
            "待接视频巡检",
            "待接 AI 判定",
            $"点位详情获取失败，当前回退到设备池摘要。来源：{device.SourceTag}。",
            device.SourceTag);
    }

    private static PointWorkspaceItemModel Empty(string pointId)
    {
        return new PointWorkspaceItemModel(
            pointId,
            pointId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            new PointCoordinateModel(0d, 0d, PointCoordinateStatus.Missing, false, "未配置经纬度"),
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            false,
            false,
            string.Empty,
            string.Empty);
    }

    private static string CombineMessages(string? primary, string? secondary)
    {
        return string.Join(
            " ",
            new[] { primary, secondary }
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!.Trim()));
    }
}
