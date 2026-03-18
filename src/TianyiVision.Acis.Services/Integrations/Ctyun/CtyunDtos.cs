using System.Globalization;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Integrations.Ctyun;

public sealed record CtyunAccessTokenDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    int RefreshExpiresIn);

public sealed record CtyunAccessTokenSnapshot(
    string TokenValue,
    string RefreshToken,
    DateTime AcquiredAt,
    DateTime ExpiresAt,
    DateTime RefreshExpiresAt);

public sealed record CtyunDeviceCatalogPageDto(
    long LastId,
    long? Total,
    IReadOnlyList<CtyunDeviceCatalogItemDto> Items);

public sealed record CtyunDeviceCatalogItemDto(
    string DeviceCode,
    string DeviceName,
    string? RegionGbId,
    string? GbId,
    string? SourceGbId);

public sealed record CtyunDeviceDetailDto(
    string DeviceCode,
    string DeviceName,
    string DeviceType,
    string? Longitude,
    string? Latitude,
    string? Location,
    bool? IsOnline,
    bool CloudStatus,
    bool PicCloudStatus,
    bool BandStatus,
    int? DeviceSource,
    string? FwVersion,
    int? SourceTypeFlag,
    DateTime? ReportTime,
    DateTime? ImportTime);

public sealed record CtyunAiAlertDto(
    string Id,
    string DeviceCode,
    string DeviceName,
    int AlertType,
    string? Content,
    DateTime CreateTime,
    DateTime? UpdateTime,
    int? AlertSource);

public sealed record CtyunDeviceAlertDto(
    string Id,
    string DeviceCode,
    string DeviceName,
    int AlertType,
    string? Content,
    DateTime CreateTime,
    DateTime? UpdateTime,
    int Status,
    int AlertSource);

public sealed record CtyunPreviewStreamUrlDto(
    int Protocol,
    string StreamUrl,
    string? Ipv6StreamUrl,
    int? Level)
{
    public string RawStreamUrl { get; init; } = string.Empty;

    public string ParsedProtocolType { get; init; } = string.Empty;

    public string MatchedFieldPath { get; init; } = string.Empty;

    public string DecryptMode { get; init; } = string.Empty;
}

public sealed record CtyunPreviewStreamSetDto(
    int? ExpireIn,
    int? VideoEnc,
    IReadOnlyList<CtyunPreviewStreamUrlDto> StreamUrls)
{
    public string MediaApiPath { get; init; } = string.Empty;

    public string RequestPayloadSummary { get; init; } = string.Empty;

    public int ResponseCode { get; init; }

    public string ResponseMessage { get; init; } = string.Empty;

    public string ResponseUrlFieldRaw { get; init; } = string.Empty;

    public string ResponseContentType { get; init; } = string.Empty;

    public string ResponseBodyPreviewFirst300 { get; init; } = string.Empty;

    public string ResponseEnvelopeShape { get; init; } = string.Empty;

    public string ResponseJsonTopLevelKeys { get; init; } = string.Empty;

    public string ResponseCandidateUrlKeys { get; init; } = string.Empty;

    public string ResponseNestedPathTried { get; init; } = string.Empty;

    public string OriginalDataRaw { get; init; } = string.Empty;

    public string MatchedFieldPath { get; init; } = string.Empty;

    public string DecryptMode { get; init; } = string.Empty;

    public string ParsedProtocolType { get; init; } = string.Empty;
}

public sealed record CtyunPreviewMediaUrlDto(
    string Url,
    int? ExpireTime)
{
    public string MediaApiPath { get; init; } = string.Empty;

    public string RequestPayloadSummary { get; init; } = string.Empty;

    public int ResponseCode { get; init; }

    public string ResponseMessage { get; init; } = string.Empty;

    public string ResponseUrlFieldRaw { get; init; } = string.Empty;

    public string ParsedPreviewUrl { get; init; } = string.Empty;

    public string ResponseContentType { get; init; } = string.Empty;

    public string ResponseBodyPreviewFirst300 { get; init; } = string.Empty;

    public string ResponseEnvelopeShape { get; init; } = string.Empty;

    public string ResponseJsonTopLevelKeys { get; init; } = string.Empty;

    public string ResponseCandidateUrlKeys { get; init; } = string.Empty;

    public string ResponseNestedPathTried { get; init; } = string.Empty;

    public string OriginalDataRaw { get; init; } = string.Empty;

    public string MatchedFieldPath { get; init; } = string.Empty;

    public string DecryptMode { get; init; } = string.Empty;

    public string ParsedProtocolType { get; init; } = string.Empty;
}

public interface ICtyunDeviceListAdapter
{
    DeviceListItemDto MapDevice(CtyunDeviceCatalogItemDto catalogItem, CtyunDeviceDetailDto? details, PointCoordinateModel coordinate);
}

public interface ICtyunAiAlertAdapter
{
    FaultAlertDto MapAlert(CtyunAiAlertDto alert);
}

public interface ICtyunDeviceAlertAdapter
{
    FaultAlertDto MapAlert(CtyunDeviceAlertDto alert);
}

public sealed class CtyunDeviceListAdapter : ICtyunDeviceListAdapter
{
    public DeviceListItemDto MapDevice(CtyunDeviceCatalogItemDto catalogItem, CtyunDeviceDetailDto? details, PointCoordinateModel coordinate)
    {
        return new DeviceListItemDto(
            PointIdentity.CreatePointId(catalogItem.DeviceCode),
            catalogItem.DeviceCode,
            string.IsNullOrWhiteSpace(details?.DeviceName) ? catalogItem.DeviceName : details.DeviceName,
            details?.DeviceType ?? "CTYun设备",
            details?.Location ?? string.Empty,
            coordinate,
            details?.IsOnline,
            "CTYun");
    }
}

public sealed class CtyunAiAlertAdapter : ICtyunAiAlertAdapter
{
    public FaultAlertDto MapAlert(CtyunAiAlertDto alert)
    {
        return new FaultAlertDto(
            alert.Id,
            alert.DeviceCode,
            alert.DeviceName,
            ResolveAiAlertType(alert.AlertType),
            ResolveAlertSource(alert.AlertSource),
            alert.Content ?? string.Empty,
            alert.CreateTime,
            alert.UpdateTime ?? alert.CreateTime,
            1);
    }

    private static string ResolveAiAlertType(int alertType)
    {
        return alertType switch
        {
            3 => "画面异常",
            4 => "时光缩影",
            5 => "区域入侵",
            17 => "火情识别",
            21 => "大象识别",
            22 => "电动车识别",
            25 => "人群聚集",
            27 => "高空抛物",
            _ => $"AI告警({alertType})"
        };
    }

    private static string ResolveAlertSource(int? alertSource)
    {
        return alertSource switch
        {
            1 => "端侧AI",
            2 => "云化AI",
            3 => "AI能力中台",
            4 => "平安慧眼",
            _ => "AI"
        };
    }
}

public sealed class CtyunDeviceAlertAdapter : ICtyunDeviceAlertAdapter
{
    public FaultAlertDto MapAlert(CtyunDeviceAlertDto alert)
    {
        return new FaultAlertDto(
            alert.Id,
            alert.DeviceCode,
            alert.DeviceName,
            ResolveDeviceAlertType(alert.AlertType),
            "设备告警",
            alert.Content ?? string.Empty,
            alert.CreateTime,
            alert.UpdateTime ?? alert.CreateTime,
            alert.Status == 0 ? 1 : 2);
    }

    private static string ResolveDeviceAlertType(int alertType)
    {
        return alertType switch
        {
            1 => "设备离线",
            2 => "画面变动",
            10 => "设备上线",
            11 => "有人移动",
            _ => $"设备告警({alertType})"
        };
    }
}
