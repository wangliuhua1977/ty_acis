using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
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
        ServiceResponse<IReadOnlyList<DeviceListItemDto>> response;
        try
        {
            response = _primary.GetDevices();
        }
        catch (Exception ex)
        {
            response = ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Failure([], $"设备目录接口异常：{ex.Message}");
        }

        if (response.IsSuccess && response.Data.Count > 0)
        {
            return response;
        }

        var reason = string.IsNullOrWhiteSpace(response.Message)
            ? "设备目录返回 0 条"
            : response.Message.Trim();
        MapPointSourceDiagnostics.Write("Fallback", $"DeviceCatalog fallback triggered: reason = {reason}");
        var fallbackResponse = _fallback.GetDevices();
        return fallbackResponse.IsSuccess
            ? ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Success(
                fallbackResponse.Data,
                $"{reason} 已回退到 demo 设备目录。")
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
        return Fallback(SafeExecute(() => _primary.GetAiAlerts(query), "真实 AI 告警调用异常。"), () => _fallback.GetAiAlerts(query), "AI 告警");
    }

    public ServiceResponse<IReadOnlyList<FaultAlertDto>> GetDeviceAlerts(DeviceAlertQueryDto query)
    {
        return Fallback(SafeExecute(() => _primary.GetDeviceAlerts(query), "真实设备告警调用异常。"), () => _fallback.GetDeviceAlerts(query), "设备告警");
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

    private static ServiceResponse<IReadOnlyList<FaultAlertDto>> SafeExecute(
        Func<ServiceResponse<IReadOnlyList<FaultAlertDto>>> action,
        string fallbackMessage)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return ServiceResponse<IReadOnlyList<FaultAlertDto>>.Failure([], $"{fallbackMessage} {ex.Message}");
        }
    }
}
