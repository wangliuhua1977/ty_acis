using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Devices;

public sealed class DeviceWorkspaceService : IDeviceWorkspaceService
{
    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IDevicePointDetailService _pointDetailService;

    public DeviceWorkspaceService(
        IDeviceCatalogService deviceCatalogService,
        IDevicePointDetailService pointDetailService)
    {
        _deviceCatalogService = deviceCatalogService;
        _pointDetailService = pointDetailService;
    }

    public ServiceResponse<IReadOnlyList<DevicePoolItemModel>> GetDevicePool()
    {
        var response = _deviceCatalogService.GetDevices();
        if (!response.IsSuccess || response.Data.Count == 0)
        {
            return ServiceResponse<IReadOnlyList<DevicePoolItemModel>>.Failure([], response.Message);
        }

        var devices = response.Data
            .Select(device => new DevicePoolItemModel(
                device.PointId,
                device.DeviceCode,
                device.DeviceName,
                device.DeviceType,
                ResolveUnitName(device.HandlingUnit),
                device.HandlingUnit,
                device.Coordinate,
                device.IsOnline,
                device.IsOnline ? "在线" : "离线",
                "设备目录"))
            .ToList();

        return ServiceResponse<IReadOnlyList<DevicePoolItemModel>>.Success(devices, response.Message);
    }

    public ServiceResponse<DevicePointDetailModel> GetPointDetail(string pointId)
    {
        return _pointDetailService.GetPointDetail(pointId);
    }

    private static string ResolveUnitName(string handlingUnit)
    {
        return string.IsNullOrWhiteSpace(handlingUnit) ? "待补齐所属单位" : handlingUnit;
    }
}

public sealed class DemoDevicePointDetailService : IDevicePointDetailService
{
    private readonly IDeviceCatalogService _deviceCatalogService;

    public DemoDevicePointDetailService(IDeviceCatalogService deviceCatalogService)
    {
        _deviceCatalogService = deviceCatalogService;
    }

    public ServiceResponse<DevicePointDetailModel> GetPointDetail(string pointId)
    {
        var catalogResponse = _deviceCatalogService.GetDevices();
        var device = catalogResponse.Data.FirstOrDefault(item => item.PointId == pointId || item.DeviceCode == pointId);
        if (device is null)
        {
            return ServiceResponse<DevicePointDetailModel>.Failure(
                Empty(pointId),
                "demo 点位详情未找到对应设备。");
        }

        return ServiceResponse<DevicePointDetailModel>.Success(new DevicePointDetailModel(
            device.PointId,
            device.DeviceCode,
            device.DeviceName,
            device.DeviceType,
            string.IsNullOrWhiteSpace(device.HandlingUnit) ? "演示维护单位" : device.HandlingUnit,
            device.HandlingUnit,
            device.HandlingUnit,
            device.Coordinate,
            device.IsOnline,
            device.IsOnline ? "在线" : "离线",
            device.IsOnline ? "可播放" : "待确认",
            "待确认",
            "当前点位详情暂未完整同步，先展示目录摘要信息。",
            "Demo"));
    }

    private static DevicePointDetailModel Empty(string pointId)
    {
        return new DevicePointDetailModel(
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
            string.Empty,
            "Demo");
    }
}

public sealed class FallbackDevicePointDetailService : IDevicePointDetailService
{
    private readonly IDevicePointDetailService _primary;
    private readonly IDevicePointDetailService _fallback;

    public FallbackDevicePointDetailService(
        IDevicePointDetailService primary,
        IDevicePointDetailService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public ServiceResponse<DevicePointDetailModel> GetPointDetail(string pointId)
    {
        ServiceResponse<DevicePointDetailModel> response;
        try
        {
            response = _primary.GetPointDetail(pointId);
        }
        catch (Exception ex)
        {
            response = ServiceResponse<DevicePointDetailModel>.Failure(Empty(pointId), $"真实点位详情调用异常。 {ex.Message}");
        }

        if (response.IsSuccess && !string.IsNullOrWhiteSpace(response.Data.PointName))
        {
            return response;
        }

        var fallback = _fallback.GetPointDetail(pointId);
        return fallback.IsSuccess
            ? ServiceResponse<DevicePointDetailModel>.Success(
                fallback.Data,
                string.IsNullOrWhiteSpace(response.Message)
                    ? "已回退到 demo 点位详情。"
                    : $"{response.Message} 已回退到 demo 点位详情。")
            : fallback;
    }

    private static DevicePointDetailModel Empty(string pointId)
    {
        return new DevicePointDetailModel(
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
            string.Empty,
            string.Empty);
    }
}
