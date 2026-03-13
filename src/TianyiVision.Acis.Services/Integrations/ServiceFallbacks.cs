using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Integrations;

public sealed class FallbackDeviceCatalogService : IDeviceCatalogService
{
    private readonly IDeviceCatalogService _primary;
    private readonly IDeviceCatalogService _fallback;

    public FallbackDeviceCatalogService(IDeviceCatalogService primary, IDeviceCatalogService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public ServiceResponse<IReadOnlyList<DeviceListItemDto>> GetDevices()
    {
        var response = _primary.GetDevices();
        if (response.IsSuccess && response.Data.Count > 0)
        {
            return response;
        }

        var fallbackResponse = _fallback.GetDevices();
        return fallbackResponse.IsSuccess
            ? ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Success(
                fallbackResponse.Data,
                string.IsNullOrWhiteSpace(response.Message)
                    ? "已回退到 demo 设备目录。"
                    : $"{response.Message} 已回退到 demo 设备目录。")
            : fallbackResponse;
    }
}

public sealed class FallbackAlertQueryService : IAlertQueryService
{
    private readonly IAlertQueryService _primary;
    private readonly IAlertQueryService _fallback;

    public FallbackAlertQueryService(IAlertQueryService primary, IAlertQueryService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public ServiceResponse<IReadOnlyList<FaultAlertDto>> GetAiAlerts(AiAlertQueryDto query)
    {
        return Fallback(_primary.GetAiAlerts(query), () => _fallback.GetAiAlerts(query), "AI 告警");
    }

    public ServiceResponse<IReadOnlyList<FaultAlertDto>> GetDeviceAlerts(DeviceAlertQueryDto query)
    {
        return Fallback(_primary.GetDeviceAlerts(query), () => _fallback.GetDeviceAlerts(query), "设备告警");
    }

    private static ServiceResponse<IReadOnlyList<FaultAlertDto>> Fallback(
        ServiceResponse<IReadOnlyList<FaultAlertDto>> primaryResponse,
        Func<ServiceResponse<IReadOnlyList<FaultAlertDto>>> fallbackFactory,
        string name)
    {
        if (primaryResponse.IsSuccess && primaryResponse.Data.Count > 0)
        {
            return primaryResponse;
        }

        var fallbackResponse = fallbackFactory();
        return fallbackResponse.IsSuccess
            ? ServiceResponse<IReadOnlyList<FaultAlertDto>>.Success(
                fallbackResponse.Data,
                string.IsNullOrWhiteSpace(primaryResponse.Message)
                    ? $"{name}已回退到 demo 数据。"
                    : $"{primaryResponse.Message} {name}已回退到 demo 数据。")
            : fallbackResponse;
    }
}
