using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Demo;

public sealed class DemoDeviceCatalogService : IDeviceCatalogService
{
    public ServiceResponse<IReadOnlyList<DeviceListItemDto>> GetDevices()
    {
        IReadOnlyList<DeviceListItemDto> devices =
        [
            new(PointIdentity.CreatePointId("p-101"), "p-101", "江滨观景台 1 号位", "慢直播摄像机", "沿江运维一中心", PointCoordinateParser.FromValues(120.143, 30.271), true),
            new(PointIdentity.CreatePointId("p-102"), "p-102", "轮渡码头北口", "慢直播摄像机", "沿江运维一中心", PointCoordinateParser.FromValues(120.145, 30.275), true),
            new(PointIdentity.CreatePointId("p-103"), "p-103", "跨江大桥东塔", "桥梁联防中心", "桥梁联防中心", PointCoordinateParser.FromValues(120.151, 30.279), true),
            new(PointIdentity.CreatePointId("p-104"), "p-104", "城市阳台主广场", "城市景观摄像机", "文旅联合中心", PointCoordinateParser.FromValues(120.157, 30.284), true),
            new(PointIdentity.CreatePointId("p-105"), "p-105", "防汛泵站外侧", "防汛保障设备", "防汛保障中心", PointCoordinateParser.FromValues(120.163, 30.287), false),
            new(PointIdentity.CreatePointId("p-106"), "p-106", "江心灯塔监看点", "航道监看设备", "航道监护中心", PointCoordinateParser.FromValues(120.171, 30.291), false),
            new(PointIdentity.CreatePointId("p-107"), "p-107", "文化展厅西侧", "文旅景观摄像机", "文旅联合中心", PointCoordinateParser.FromValues(120.179, 30.293), true),
            new(PointIdentity.CreatePointId("p-108"), "p-108", "景观桥步道口", "桥梁联防设备", "桥梁联防中心", PointCoordinateParser.FromValues(120.186, 30.298), true)
        ];

        return ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Success(devices);
    }
}
