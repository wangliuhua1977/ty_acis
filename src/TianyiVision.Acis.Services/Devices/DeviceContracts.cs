using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Devices;

public interface IDeviceCatalogService
{
    ServiceResponse<IReadOnlyList<DeviceListItemDto>> GetDevices();
}
