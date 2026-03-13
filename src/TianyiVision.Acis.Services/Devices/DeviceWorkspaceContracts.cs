using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Devices;

public enum PointCoordinateStatus
{
    Valid,
    Missing,
    Incomplete,
    Invalid,
    ZeroOrigin
}

public sealed record PointCoordinateModel(
    double Longitude,
    double Latitude,
    PointCoordinateStatus Status,
    bool CanRenderOnMap,
    string StatusText);

public sealed record DevicePoolItemModel(
    string PointId,
    string DeviceCode,
    string DeviceName,
    string DeviceType,
    string UnitName,
    string AreaName,
    PointCoordinateModel Coordinate,
    bool IsOnline,
    string OnlineStatusText,
    string SourceTag);

public sealed record DevicePointDetailModel(
    string PointId,
    string DeviceCode,
    string PointName,
    string DeviceType,
    string UnitName,
    string AreaName,
    string LocationText,
    PointCoordinateModel Coordinate,
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
    ServiceResponse<DevicePointDetailModel> GetPointDetail(string pointId);
}

public static class PointIdentity
{
    public static string CreatePointId(string deviceCode)
    {
        return string.IsNullOrWhiteSpace(deviceCode)
            ? string.Empty
            : deviceCode.Trim();
    }
}

public static class PointCoordinateParser
{
    public static PointCoordinateModel FromRaw(string? rawLongitude, string? rawLatitude)
    {
        var longitudeText = Normalize(rawLongitude);
        var latitudeText = Normalize(rawLatitude);

        if (string.IsNullOrWhiteSpace(longitudeText) && string.IsNullOrWhiteSpace(latitudeText))
        {
            return new PointCoordinateModel(0d, 0d, PointCoordinateStatus.Missing, false, "未配置经纬度");
        }

        if (string.IsNullOrWhiteSpace(longitudeText) || string.IsNullOrWhiteSpace(latitudeText))
        {
            var longitude = TryParseCoordinate(longitudeText, out var parsedLongitude) ? parsedLongitude : 0d;
            var latitude = TryParseCoordinate(latitudeText, out var parsedLatitude) ? parsedLatitude : 0d;
            return new PointCoordinateModel(longitude, latitude, PointCoordinateStatus.Incomplete, false, "经纬度不完整");
        }

        if (!TryParseCoordinate(longitudeText, out var longitudeValue)
            || !TryParseCoordinate(latitudeText, out var latitudeValue))
        {
            return new PointCoordinateModel(0d, 0d, PointCoordinateStatus.Invalid, false, "经纬度格式异常");
        }

        if (longitudeValue == 0d && latitudeValue == 0d)
        {
            return new PointCoordinateModel(longitudeValue, latitudeValue, PointCoordinateStatus.ZeroOrigin, false, "经纬度落在原点，暂不可落图");
        }

        if (longitudeValue is < -180d or > 180d || latitudeValue is < -90d or > 90d)
        {
            return new PointCoordinateModel(longitudeValue, latitudeValue, PointCoordinateStatus.Invalid, false, "经纬度超出有效范围");
        }

        return new PointCoordinateModel(longitudeValue, latitudeValue, PointCoordinateStatus.Valid, true, "坐标可落点");
    }

    public static PointCoordinateModel FromValues(double longitude, double latitude)
    {
        return FromRaw(
            longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
            latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string? Normalize(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
    }

    private static bool TryParseCoordinate(string? rawValue, out double value)
    {
        return double.TryParse(
            rawValue,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value)
            || double.TryParse(
                rawValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture,
                out value);
    }
}
