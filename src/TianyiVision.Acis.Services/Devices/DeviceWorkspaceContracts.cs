using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Devices;

public sealed record DevicePoolItemModel(
    string DeviceCode,
    string DeviceName,
    string DeviceType,
    string UnitName,
    string AreaName,
    double Longitude,
    double Latitude,
    bool IsOnline,
    string OnlineStatusText,
    string SourceTag);

public sealed record DevicePointDetailModel(
    string DeviceCode,
    string PointName,
    string DeviceType,
    string UnitName,
    string AreaName,
    string LocationText,
    double Longitude,
    double Latitude,
    bool IsOnline,
    string OnlineStatusText,
    string PlaybackStatusText,
    string ImageStatusText,
    string DetailSummary,
    string SourceTag);

public interface IDeviceWorkspaceService
{
    ServiceResponse<IReadOnlyList<DevicePoolItemModel>> GetDevicePool();

    ServiceResponse<DevicePointDetailModel> GetPointDetail(string deviceCode);
}

public interface IDevicePointDetailService
{
    ServiceResponse<DevicePointDetailModel> GetPointDetail(string deviceCode);
}
