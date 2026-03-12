using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Integrations.Ctyun;

public interface ICtyunDeviceListAdapter
{
    DeviceListItemDto MapDevice(CtyunDeviceDetailDto device);
}

public interface ICtyunInspectionResultAdapter
{
    InspectionResultDto MapResult(CtyunInspectionResultDto result);
}

public interface ICtyunFaultAlertAdapter
{
    FaultAlertDto MapAlert(CtyunFaultAlertDto alert);
}

public sealed class CtyunDeviceListAdapter : ICtyunDeviceListAdapter
{
    public DeviceListItemDto MapDevice(CtyunDeviceDetailDto device)
    {
        _ = double.TryParse(device.Longitude, out var longitude);
        _ = double.TryParse(device.Latitude, out var latitude);

        return new DeviceListItemDto(
            device.DeviceCode,
            device.DeviceName,
            device.DeviceModel,
            string.Empty,
            longitude,
            latitude,
            true);
    }
}

public sealed class CtyunInspectionResultAdapter : ICtyunInspectionResultAdapter
{
    public InspectionResultDto MapResult(CtyunInspectionResultDto result)
    {
        return new InspectionResultDto(
            string.Empty,
            string.Empty,
            result.DeviceCode,
            result.InspectTime,
            result.IsOnline,
            result.IsPlayable,
            result.IsImageAbnormal,
            result.FaultType,
            result.FaultDescription);
    }
}

public sealed class CtyunFaultAlertAdapter : ICtyunFaultAlertAdapter
{
    public FaultAlertDto MapAlert(CtyunFaultAlertDto alert)
    {
        var latestTime = alert.UpdateTime ?? alert.CreateTime;
        return new FaultAlertDto(
            alert.AlertId,
            alert.DeviceCode,
            alert.DeviceName,
            alert.AlertType.ToString(),
            alert.AlertSource,
            alert.Content,
            alert.CreateTime,
            latestTime,
            1);
    }
}
