using System.Globalization;
using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Devices;

public enum CoordinateSystemKind
{
    Unknown,
    BD09,
    GCJ02
}

public enum PointCoordinateStatus
{
    Valid,
    Missing,
    Incomplete,
    Invalid,
    ZeroOrigin,
    ConversionFailed
}

public sealed record CoordinateValueModel(
    double Longitude,
    double Latitude,
    CoordinateSystemKind CoordinateSystem);

public sealed record ParsedRegisteredCoordinateModel(
    string? RawLongitude,
    string? RawLatitude,
    CoordinateValueModel? Coordinate,
    PointCoordinateStatus Status,
    string StatusText,
    string DiagnosticsText)
{
    public bool IsValid => Status == PointCoordinateStatus.Valid && Coordinate is not null;
}

public sealed record PointCoordinateModel(
    string? RawLongitude,
    string? RawLatitude,
    CoordinateValueModel? RegisteredCoordinate,
    CoordinateValueModel? MapCoordinate,
    PointCoordinateStatus Status,
    bool CanRenderOnMap,
    string StatusText,
    string DiagnosticsText,
    string MapSource)
{
    public double Longitude => MapCoordinate?.Longitude ?? RegisteredCoordinate?.Longitude ?? 0d;

    public double Latitude => MapCoordinate?.Latitude ?? RegisteredCoordinate?.Latitude ?? 0d;

    public double RegisteredLongitude => RegisteredCoordinate?.Longitude ?? 0d;

    public double RegisteredLatitude => RegisteredCoordinate?.Latitude ?? 0d;

    public CoordinateSystemKind RegisteredCoordinateSystem => RegisteredCoordinate?.CoordinateSystem ?? CoordinateSystemKind.Unknown;

    public CoordinateSystemKind MapCoordinateSystem => MapCoordinate?.CoordinateSystem ?? CoordinateSystemKind.Unknown;

    public bool IsConverted =>
        RegisteredCoordinate is not null
        && MapCoordinate is not null
        && RegisteredCoordinate.CoordinateSystem != MapCoordinate.CoordinateSystem;

    public bool HasRenderableCoordinateCandidate => CanRenderOnMap;
}

public sealed record DevicePoolItemModel(
    string PointId,
    string DeviceCode,
    string DeviceName,
    string DeviceType,
    string UnitName,
    string AreaName,
    PointCoordinateModel Coordinate,
    bool? IsOnline,
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
    bool? IsOnline,
    string OnlineStatusText,
    string PlaybackStatusText,
    string ImageStatusText,
    DateTime? LastSyncTime,
    string LastSyncSource,
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
    private const string MissingStatusText = "未配置经纬度";
    private const string IncompleteStatusText = "经纬度不完整";
    private const string InvalidFormatStatusText = "经纬度格式异常";
    private const string OutOfRangeStatusText = "经纬度超出有效范围";
    private const string ZeroOriginStatusText = "经纬度落在原点，暂不可落图";
    private const string RenderableStatusText = "坐标可落点";
    private const string ConversionFailureStatusText = "坐标异常";
    private const string DirectGcj02DiagnosticsText = "原始注册坐标已是 GCJ-02，可直接上图。";
    private const string ClientSideBaiduConversionDiagnosticsText = "原始注册坐标为 BD-09，将在地图前端通过 AMap.convertFrom(points, \"baidu\", ...) 转换后上图。";

    public static ParsedRegisteredCoordinateModel ParseRegistered(
        string? rawLongitude,
        string? rawLatitude,
        CoordinateSystemKind sourceSystem = CoordinateSystemKind.BD09)
    {
        var longitudeText = Normalize(rawLongitude);
        var latitudeText = Normalize(rawLatitude);

        if (string.IsNullOrWhiteSpace(longitudeText) && string.IsNullOrWhiteSpace(latitudeText))
        {
            return new ParsedRegisteredCoordinateModel(
                longitudeText,
                latitudeText,
                null,
                PointCoordinateStatus.Missing,
                MissingStatusText,
                MissingStatusText);
        }

        if (string.IsNullOrWhiteSpace(longitudeText) || string.IsNullOrWhiteSpace(latitudeText))
        {
            return new ParsedRegisteredCoordinateModel(
                longitudeText,
                latitudeText,
                null,
                PointCoordinateStatus.Incomplete,
                IncompleteStatusText,
                IncompleteStatusText);
        }

        if (!TryParseCoordinate(longitudeText, out var longitudeValue)
            || !TryParseCoordinate(latitudeText, out var latitudeValue))
        {
            return new ParsedRegisteredCoordinateModel(
                longitudeText,
                latitudeText,
                null,
                PointCoordinateStatus.Invalid,
                InvalidFormatStatusText,
                InvalidFormatStatusText);
        }

        var coordinate = new CoordinateValueModel(longitudeValue, latitudeValue, sourceSystem);

        if (longitudeValue == 0d && latitudeValue == 0d)
        {
            return new ParsedRegisteredCoordinateModel(
                longitudeText,
                latitudeText,
                coordinate,
                PointCoordinateStatus.ZeroOrigin,
                ZeroOriginStatusText,
                ZeroOriginStatusText);
        }

        if (longitudeValue is < -180d or > 180d || latitudeValue is < -90d or > 90d)
        {
            return new ParsedRegisteredCoordinateModel(
                longitudeText,
                latitudeText,
                coordinate,
                PointCoordinateStatus.Invalid,
                OutOfRangeStatusText,
                OutOfRangeStatusText);
        }

        return new ParsedRegisteredCoordinateModel(
            longitudeText,
            latitudeText,
            coordinate,
            PointCoordinateStatus.Valid,
            RenderableStatusText,
            RenderableStatusText);
    }

    public static PointCoordinateModel FromRaw(
        string? rawLongitude,
        string? rawLatitude,
        CoordinateSystemKind sourceSystem = CoordinateSystemKind.BD09)
    {
        return FromParsedRegistered(ParseRegistered(rawLongitude, rawLatitude, sourceSystem));
    }

    public static PointCoordinateModel FromValues(
        double longitude,
        double latitude,
        CoordinateSystemKind coordinateSystem = CoordinateSystemKind.GCJ02)
    {
        return FromRaw(
            longitude.ToString(CultureInfo.InvariantCulture),
            latitude.ToString(CultureInfo.InvariantCulture),
            coordinateSystem);
    }

    public static PointCoordinateModel FromParsedRegistered(ParsedRegisteredCoordinateModel parsed)
    {
        if (!parsed.IsValid || parsed.Coordinate is null)
        {
            return new PointCoordinateModel(
                parsed.RawLongitude,
                parsed.RawLatitude,
                parsed.Coordinate,
                null,
                parsed.Status,
                false,
                parsed.StatusText,
                parsed.DiagnosticsText,
                "unavailable");
        }

        return parsed.Coordinate.CoordinateSystem == CoordinateSystemKind.GCJ02
            ? CreateResolved(
                parsed.Coordinate,
                parsed.Coordinate,
                DirectGcj02DiagnosticsText,
                parsed.RawLongitude,
                parsed.RawLatitude)
            : CreateClientConvertible(
                parsed.Coordinate,
                parsed.RawLongitude,
                parsed.RawLatitude);
    }

    public static PointCoordinateModel CreateResolved(
        CoordinateValueModel registeredCoordinate,
        CoordinateValueModel mapCoordinate,
        string? diagnosticsText = null,
        string? rawLongitude = null,
        string? rawLatitude = null)
    {
        return new PointCoordinateModel(
            Normalize(rawLongitude) ?? registeredCoordinate.Longitude.ToString(CultureInfo.InvariantCulture),
            Normalize(rawLatitude) ?? registeredCoordinate.Latitude.ToString(CultureInfo.InvariantCulture),
            registeredCoordinate,
            mapCoordinate,
            PointCoordinateStatus.Valid,
            true,
            RenderableStatusText,
            string.IsNullOrWhiteSpace(diagnosticsText) ? RenderableStatusText : diagnosticsText.Trim(),
            registeredCoordinate.CoordinateSystem == CoordinateSystemKind.GCJ02
                ? "registered_gcj02"
                : "resolved_gcj02");
    }

    public static PointCoordinateModel CreateClientConvertible(
        CoordinateValueModel registeredCoordinate,
        string? rawLongitude = null,
        string? rawLatitude = null)
    {
        return new PointCoordinateModel(
            Normalize(rawLongitude) ?? registeredCoordinate.Longitude.ToString(CultureInfo.InvariantCulture),
            Normalize(rawLatitude) ?? registeredCoordinate.Latitude.ToString(CultureInfo.InvariantCulture),
            registeredCoordinate,
            null,
            PointCoordinateStatus.Valid,
            true,
            RenderableStatusText,
            ClientSideBaiduConversionDiagnosticsText,
            "amap_js_convert_from_baidu");
    }

    public static PointCoordinateModel CreateConversionFailed(
        CoordinateValueModel registeredCoordinate,
        string diagnosticsText,
        string? rawLongitude = null,
        string? rawLatitude = null)
    {
        return new PointCoordinateModel(
            Normalize(rawLongitude) ?? registeredCoordinate.Longitude.ToString(CultureInfo.InvariantCulture),
            Normalize(rawLatitude) ?? registeredCoordinate.Latitude.ToString(CultureInfo.InvariantCulture),
            registeredCoordinate,
            null,
            PointCoordinateStatus.ConversionFailed,
            false,
            ConversionFailureStatusText,
            string.IsNullOrWhiteSpace(diagnosticsText) ? ConversionFailureStatusText : diagnosticsText.Trim(),
            "unavailable");
    }

    public static PointCoordinateModel Missing()
    {
        return new PointCoordinateModel(
            null,
            null,
            null,
            null,
            PointCoordinateStatus.Missing,
            false,
            MissingStatusText,
            MissingStatusText,
            "unavailable");
    }

    private static string? Normalize(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
    }

    private static bool TryParseCoordinate(string? rawValue, out double value)
    {
        return double.TryParse(
            rawValue,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value)
            || double.TryParse(
                rawValue,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value);
    }
}
