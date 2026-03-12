using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Demo;

public sealed class DemoDeviceCatalogService : IDeviceCatalogService
{
    public ServiceResponse<IReadOnlyList<DeviceListItemDto>> GetDevices()
    {
        IReadOnlyList<DeviceListItemDto> devices =
        [
            new("p-101", "江滨观景台 1 号位", "慢直播摄像机", "沿江运维一中心", 120.143, 30.271, true),
            new("p-102", "轮渡码头北口", "慢直播摄像机", "沿江运维一中心", 120.145, 30.275, true),
            new("p-103", "跨江大桥东塔", "桥梁联防中心", "桥梁联防中心", 120.151, 30.279, true),
            new("p-104", "城市阳台主广场", "城市景观摄像机", "文旅联合中心", 120.157, 30.284, true),
            new("p-105", "防汛泵站外侧", "防汛保障设备", "防汛保障中心", 120.163, 30.287, false),
            new("p-106", "江心灯塔监看点", "航道监看设备", "航道监护中心", 120.171, 30.291, false),
            new("p-107", "文化展厅西侧", "文旅景观摄像机", "文旅联合中心", 120.179, 30.293, true),
            new("p-108", "景观桥步道口", "桥梁联防设备", "桥梁联防中心", 120.186, 30.298, true)
        ];

        return ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Success(devices);
    }
}
