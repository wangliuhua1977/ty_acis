using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Integrations.Ctyun;

public interface ICtyunAccessTokenService
{
    ServiceResponse<CtyunAccessTokenSnapshot> GetAccessToken();
}

internal sealed record ProtectedApiResponse(
    bool IsSuccess,
    string Payload,
    string Message,
    int ResponseCode,
    string ResponseMessage,
    string ResponseContentType,
    string Path,
    string RequestPayloadSummary);

public sealed class CtyunAccessTokenService : ICtyunAccessTokenService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly PlatformIntegrationSettings _settings;
    private readonly object _syncRoot = new();
    private CtyunAccessTokenSnapshot? _cachedToken;

    public CtyunAccessTokenService(HttpClient httpClient, PlatformIntegrationSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public ServiceResponse<CtyunAccessTokenSnapshot> GetAccessToken()
    {
        lock (_syncRoot)
        {
            if (_cachedToken is not null && IsReusable(_cachedToken))
            {
                return ServiceResponse<CtyunAccessTokenSnapshot>.Success(_cachedToken, "使用内存缓存 accessToken。");
            }

            var refreshResponse = TryRefreshToken();
            if (refreshResponse.IsSuccess)
            {
                _cachedToken = refreshResponse.Data;
                return refreshResponse;
            }

            var requestResponse = RequestToken(_settings.OpenPlatform.GrantType, null);
            if (!requestResponse.IsSuccess)
            {
                return requestResponse;
            }

            _cachedToken = requestResponse.Data;
            return requestResponse;
        }
    }

    private ServiceResponse<CtyunAccessTokenSnapshot> TryRefreshToken()
    {
        if (_cachedToken is null
            || string.IsNullOrWhiteSpace(_cachedToken.RefreshToken)
            || _cachedToken.RefreshExpiresAt <= DateTime.UtcNow)
        {
            return ServiceResponse<CtyunAccessTokenSnapshot>.Failure(
                EmptyToken(),
                "当前没有可用 refreshToken，改为重新申请 accessToken。");
        }

        return RequestToken(_settings.OpenPlatform.Token.RefreshGrantType, _cachedToken.RefreshToken);
    }

    private ServiceResponse<CtyunAccessTokenSnapshot> RequestToken(string grantType, string? refreshToken)
    {
        try
        {
            MapPointSourceDiagnostics.Write(
                "CTYunToken",
                $"Calling access token API: path = {_settings.OpenPlatform.Token.AccessTokenPath}, grantType = {grantType}, enterpriseUser = {MapPointSourceDiagnostics.MaskValue(_settings.OpenPlatform.EnterpriseUser)}");

            var businessParameters = new List<KeyValuePair<string, string>>
            {
                new("grantType", grantType)
            };

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                businessParameters.Add(new("refreshToken", refreshToken));
            }

            var requestParameters = new List<KeyValuePair<string, string>>
            {
                new("appId", _settings.OpenPlatform.AppId),
                new("clientType", _settings.OpenPlatform.ClientType),
                new("params", CtyunSecurity.EncryptParams(businessParameters, _settings.OpenPlatform.AppSecret)),
                new("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)),
                new("version", _settings.OpenPlatform.Version)
            };
            requestParameters.Add(new("signature", CtyunSecurity.BuildSignature(requestParameters, _settings.OpenPlatform.AppSecret)));

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                BuildUrl(_settings.OpenPlatform.BaseUrl, _settings.OpenPlatform.Token.AccessTokenPath))
            {
                Content = new FormUrlEncodedContent(requestParameters)
            };
            request.Headers.TryAddWithoutValidation("apiVersion", _settings.OpenPlatform.ApiVersion);

            using var response = _httpClient.Send(request);
            var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            MapPointSourceDiagnostics.Write(
                "CTYunToken",
                $"Access token API responded: status = {(int)response.StatusCode} {response.StatusCode}");
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("code", out var codeElement) || codeElement.ValueKind != JsonValueKind.Number)
            {
                var topLevelProperties = DescribeTopLevelProperties(root);
                var alternateCode = ReadOptionalText(root, "CODE");
                var alternateMessage = ReadOptionalText(root, "MSG");
                MapPointSourceDiagnostics.Write(
                    "CTYunToken",
                    $"Access token response missing code field: topLevelProperties = {topLevelProperties}, alternateCode = {alternateCode ?? "none"}, alternateMessage = {alternateMessage ?? "none"}");
                return ServiceResponse<CtyunAccessTokenSnapshot>.Failure(
                    EmptyToken(),
                    $"CTYun accessToken 响应缺少 code 字段（顶层字段：{topLevelProperties}，备用 CODE = {alternateCode ?? "none"}，MSG = {alternateMessage ?? "none"}）。");
            }

            var code = codeElement.GetInt32();
            var message = root.TryGetProperty("msg", out var messageElement)
                ? messageElement.GetString() ?? "未知错误"
                : "未知错误";
            if (code != 0)
            {
                MapPointSourceDiagnostics.Write(
                    "CTYunToken",
                    $"Access token request failed: code = {code}, grantType = {grantType}, message = {message}");
                return ServiceResponse<CtyunAccessTokenSnapshot>.Failure(
                    EmptyToken(),
                    $"CTYun accessToken 获取失败：{message}");
            }

            if (!root.TryGetProperty("data", out var dataElement)
                || dataElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                MapPointSourceDiagnostics.Write("CTYunToken", "Access token response missing data field.");
                return ServiceResponse<CtyunAccessTokenSnapshot>.Failure(
                    EmptyToken(),
                    "CTYun accessToken 响应缺少 data 字段。");
            }

            var data = JsonSerializer.Deserialize<CtyunAccessTokenDto>(
                dataElement.GetRawText(),
                SerializerOptions);

            if (data is null || string.IsNullOrWhiteSpace(data.AccessToken))
            {
                MapPointSourceDiagnostics.Write("CTYunToken", "Access token response missing usable token payload.");
                return ServiceResponse<CtyunAccessTokenSnapshot>.Failure(
                    EmptyToken(),
                    "CTYun accessToken 响应缺少有效 token。");
            }

            var acquiredAt = DateTime.UtcNow;
            var snapshot = new CtyunAccessTokenSnapshot(
                data.AccessToken,
                data.RefreshToken,
                acquiredAt,
                acquiredAt.AddSeconds(data.ExpiresIn),
                acquiredAt.AddSeconds(data.RefreshExpiresIn));
            MapPointSourceDiagnostics.Write(
                "CTYunToken",
                $"Access token acquired successfully: expiresAt = {snapshot.ExpiresAt:yyyy-MM-dd HH:mm:ss}, refreshExpiresAt = {snapshot.RefreshExpiresAt:yyyy-MM-dd HH:mm:ss}");

            return ServiceResponse<CtyunAccessTokenSnapshot>.Success(snapshot, "已获取最新 accessToken。");
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "CTYunToken",
                $"Access token request exception: type = {ex.GetType().Name}, message = {ex.Message}");
            return ServiceResponse<CtyunAccessTokenSnapshot>.Failure(
                EmptyToken(),
                $"CTYun accessToken 请求异常：{ex.Message}");
        }
    }

    private bool IsReusable(CtyunAccessTokenSnapshot token)
    {
        return token.ExpiresAt > DateTime.UtcNow.AddSeconds(_settings.OpenPlatform.Token.ReuseBeforeExpirySeconds);
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static CtyunAccessTokenSnapshot EmptyToken()
    {
        return new CtyunAccessTokenSnapshot(string.Empty, string.Empty, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
    }

    private static string DescribeTopLevelProperties(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            ? string.Join(", ", element.EnumerateObject().Select(property => property.Name))
            : element.ValueKind.ToString();
    }

    private static string? ReadOptionalText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => value.ToString()
        };
    }
}

public sealed class CtyunOpenPlatformClient
{
    private static readonly TimeSpan DeviceDetailCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DeviceDetailPartialCacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DeviceDetailFailureCacheLifetime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan DeviceDetailMinimumRefreshInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DeviceAlertCacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DeviceAlertFailureCacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DeviceAlertMinimumRefreshInterval = TimeSpan.FromSeconds(15);
    private static readonly string[] PreviewUrlPropertyNames =
    [
        "rtcUrl",
        "webrtcUrl",
        "webRtcUrl",
        "url",
        "streamUrl",
        "playUrl",
        "mediaUrl",
        "flvUrl",
        "hlsUrl",
        "previewUrl",
        "h5Url",
        "liveUrl",
        "httpUrl",
        "httpsUrl"
    ];

    private readonly HttpClient _httpClient;
    private readonly PlatformIntegrationSettings _settings;
    private readonly ICtyunAccessTokenService _accessTokenService;
    private readonly object _deviceDetailCacheSyncRoot = new();
    private readonly Dictionary<string, CachedServiceResponseEntry<CtyunDeviceDetailDto>> _deviceDetailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _deviceAlertCacheSyncRoot = new();
    private readonly Dictionary<string, CachedServiceResponseEntry<IReadOnlyList<CtyunDeviceAlertDto>>> _deviceAlertCache = new(StringComparer.OrdinalIgnoreCase);

    public CtyunOpenPlatformClient(
        HttpClient httpClient,
        PlatformIntegrationSettings settings,
        ICtyunAccessTokenService accessTokenService)
    {
        _httpClient = httpClient;
        _settings = settings;
        _accessTokenService = accessTokenService;
    }

    public ServiceResponse<CtyunDeviceCatalogPageDto> GetDeviceCatalogPage(long lastId)
    {
        MapPointSourceDiagnostics.Write(
            "DeviceCatalog",
            $"Calling device catalog API: path = {_settings.OpenPlatform.DeviceApi.DeviceListPath}, lastId = {lastId}, pageSize = {_settings.OpenPlatform.DeviceApi.PageSize}, hasChildDevices = {_settings.OpenPlatform.DeviceApi.HasChildDevices}");

        var response = SendProtectedRequest(
            _settings.OpenPlatform.DeviceApi.DeviceListPath,
            [
                new("accessToken", GetTokenOrThrow()),
                new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
                new("lastId", lastId.ToString(CultureInfo.InvariantCulture)),
                new("pageSize", _settings.OpenPlatform.DeviceApi.PageSize.ToString(CultureInfo.InvariantCulture)),
                new("hasChildDevices", _settings.OpenPlatform.DeviceApi.HasChildDevices.ToString(CultureInfo.InvariantCulture)),
                .. BuildParentUserParameter()
            ]);

        if (!response.IsSuccess)
        {
            MapPointSourceDiagnostics.Write(
                "DeviceCatalog",
                $"Device catalog API failed: path = {_settings.OpenPlatform.DeviceApi.DeviceListPath}, reason = {response.Message}");
            return ServiceResponse<CtyunDeviceCatalogPageDto>.Failure(new CtyunDeviceCatalogPageDto(-1, null, []), response.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Data);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data)
                || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                var topLevelProperties = DescribeTopLevelProperties(root);
                MapPointSourceDiagnostics.Write(
                    "DeviceCatalog",
                    $"Device catalog response missing data field: topLevelProperties = {topLevelProperties}");
                return ServiceResponse<CtyunDeviceCatalogPageDto>.Failure(
                    new CtyunDeviceCatalogPageDto(-1, null, []),
                    $"CTYun 设备列表响应缺少 data 字段（顶层字段：{topLevelProperties}）。");
            }

            if (!data.TryGetProperty("list", out var listElement) || listElement.ValueKind != JsonValueKind.Array)
            {
                var dataProperties = DescribeTopLevelProperties(data);
                MapPointSourceDiagnostics.Write(
                    "DeviceCatalog",
                    $"Device catalog response missing list array: dataProperties = {dataProperties}");
                return ServiceResponse<CtyunDeviceCatalogPageDto>.Failure(
                    new CtyunDeviceCatalogPageDto(-1, null, []),
                    $"CTYun 设备列表响应缺少 list 数组（data 字段：{dataProperties}）。");
            }

            if (!data.TryGetProperty("lastId", out var lastIdElement) || lastIdElement.ValueKind != JsonValueKind.Number)
            {
                var dataProperties = DescribeTopLevelProperties(data);
                MapPointSourceDiagnostics.Write(
                    "DeviceCatalog",
                    $"Device catalog response missing lastId: dataProperties = {dataProperties}");
                return ServiceResponse<CtyunDeviceCatalogPageDto>.Failure(
                    new CtyunDeviceCatalogPageDto(-1, null, []),
                    $"CTYun 设备列表响应缺少 lastId 字段（data 字段：{dataProperties}）。");
            }

            var items = data.GetProperty("list")
                .EnumerateArray()
                .Select(item => new CtyunDeviceCatalogItemDto(
                    item.GetProperty("deviceCode").GetString() ?? string.Empty,
                    item.GetProperty("deviceName").GetString() ?? string.Empty,
                    item.TryGetProperty("regionGBId", out var regionGbId) ? regionGbId.GetString() : null,
                    item.TryGetProperty("gbId", out var gbId) ? gbId.GetString() : null,
                    item.TryGetProperty("sourceGbId", out var sourceGbId) ? sourceGbId.GetString() : null))
                .ToList();

            var page = new CtyunDeviceCatalogPageDto(
                lastIdElement.GetInt64(),
                data.TryGetProperty("total", out var total) && total.ValueKind != JsonValueKind.Null ? total.GetInt64() : null,
                items);

            MapPointSourceDiagnostics.Write(
                "DeviceCatalog",
                $"Device catalog page loaded: returnedCount = {items.Count}, nextLastId = {page.LastId}, total = {(page.Total?.ToString(CultureInfo.InvariantCulture) ?? "unknown")}");

            return ServiceResponse<CtyunDeviceCatalogPageDto>.Success(page);
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "DeviceCatalog",
                $"Device catalog response parse exception: type = {ex.GetType().Name}, message = {ex.Message}");
            return ServiceResponse<CtyunDeviceCatalogPageDto>.Failure(
                new CtyunDeviceCatalogPageDto(-1, null, []),
                $"CTYun 设备列表解析失败：{ex.Message}");
        }
    }

    public ServiceResponse<CtyunDeviceDetailDto> GetDeviceDetail(string deviceCode)
    {
        var normalizedDeviceCode = deviceCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDeviceCode))
        {
            return ServiceResponse<CtyunDeviceDetailDto>.Failure(EmptyDeviceDetail(string.Empty), "设备编码为空，无法获取点位详情。");
        }

        if (TryGetCachedDeviceDetail(normalizedDeviceCode, out var cachedResponse))
        {
            return cachedResponse;
        }

        ServiceResponse<CtyunDeviceDetailDto>? firstFailure = null;
        CtyunDeviceDetailDto? mergedDetail = null;
        string lastSuccessMessage = string.Empty;

        foreach (var path in BuildDeviceDetailPaths())
        {
            var response = GetDeviceDetailFromPath(normalizedDeviceCode, path);
            if (!response.IsSuccess)
            {
                firstFailure ??= response;
                continue;
            }

            mergedDetail = mergedDetail is null
                ? response.Data
                : MergeDeviceDetail(mergedDetail, response.Data);
            lastSuccessMessage = response.Message;

            if (HasUsableRegisteredCoordinate(mergedDetail))
            {
                var successResponse = ServiceResponse<CtyunDeviceDetailDto>.Success(mergedDetail, response.Message);
                CacheDeviceDetail(normalizedDeviceCode, successResponse, DeviceDetailCacheLifetime);
                MapPointSourceDiagnostics.Write(
                    "PointDetail",
                    $"Device detail resolved with usable registered coordinate: deviceCode = {normalizedDeviceCode}, path = {path}");
                return successResponse;
            }

            if (ShouldStopDeviceDetailFallback(path, mergedDetail))
            {
                MapPointSourceDiagnostics.Write(
                    "PointDetail",
                    $"Device detail fallback stopped early: deviceCode = {normalizedDeviceCode}, path = {path}, reason = showDevice_returned_stable_non_coordinate_detail");
                break;
            }
        }

        if (mergedDetail is not null)
        {
            var mergedResponse = ServiceResponse<CtyunDeviceDetailDto>.Success(
                mergedDetail,
                string.IsNullOrWhiteSpace(lastSuccessMessage) ? "已合并设备详情接口返回。" : lastSuccessMessage);
            CacheDeviceDetail(normalizedDeviceCode, mergedResponse, DeviceDetailPartialCacheLifetime);
            var parsedCoordinate = PointCoordinateParser.ParseRegistered(mergedDetail.Longitude, mergedDetail.Latitude, CoordinateSystemKind.BD09);
            MapPointSourceDiagnostics.Write(
                "PointDetail",
                $"Device detail resolved without usable registered coordinate: deviceCode = {normalizedDeviceCode}, coordinateStatus = {parsedCoordinate.Status}");
            return mergedResponse;
        }

        if (firstFailure is not null)
        {
            CacheDeviceDetail(normalizedDeviceCode, firstFailure, DeviceDetailFailureCacheLifetime);
            MapPointSourceDiagnostics.Write(
                "PointDetail",
                $"Device detail resolution failed: deviceCode = {normalizedDeviceCode}, reason = {firstFailure.Message}");
        }

        return firstFailure
            ?? ServiceResponse<CtyunDeviceDetailDto>.Failure(EmptyDeviceDetail(normalizedDeviceCode), "CTYun 设备详情未返回有效数据。");
    }

    public ServiceResponse<IReadOnlyList<CtyunAiAlertDto>> GetAiAlerts(AiAlertQueryDto query)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("accessToken", GetTokenOrThrow()),
            new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
            new("pageNo", query.PageNo.ToString(CultureInfo.InvariantCulture)),
            new("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(query.DeviceCode))
        {
            parameters.Add(new("deviceCode", query.DeviceCode));
        }

        if (!string.IsNullOrWhiteSpace(query.AlertTypeList))
        {
            parameters.Add(new("alertTypeList", query.AlertTypeList));
        }

        if (query.AlertSource.HasValue)
        {
            parameters.Add(new("alertSource", query.AlertSource.Value.ToString(CultureInfo.InvariantCulture)));
        }

        AppendTimeRange(parameters, query.StartTime, query.EndTime);
        parameters.AddRange(BuildParentUserParameter());

        var response = SendProtectedRequest(_settings.OpenPlatform.AlarmApi.AiAlertListPath, parameters);
        if (!response.IsSuccess)
        {
            return ServiceResponse<IReadOnlyList<CtyunAiAlertDto>>.Failure([], response.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Data);
            var data = document.RootElement.GetProperty("data");
            var listElement = data.ValueKind == JsonValueKind.Array ? data : data.GetProperty("list");
            var alerts = listElement
                .EnumerateArray()
                .Select(item => new CtyunAiAlertDto(
                    item.GetProperty("id").ToString(),
                    item.GetProperty("deviceCode").GetString() ?? string.Empty,
                    item.TryGetProperty("deviceName", out var deviceName) ? deviceName.GetString() ?? string.Empty : string.Empty,
                    item.GetProperty("alertType").GetInt32(),
                    item.TryGetProperty("content", out var content) ? content.GetString() : null,
                    ParseDateTime(item.TryGetProperty("createTime", out var createTime) ? createTime.ToString() : null),
                    item.TryGetProperty("updateTime", out var updateTime) ? ParseNullableDateTime(updateTime.ToString()) : null,
                    item.TryGetProperty("alertSource", out var alertSource) && alertSource.ValueKind != JsonValueKind.Null ? alertSource.GetInt32() : null))
                .ToList();

            return ServiceResponse<IReadOnlyList<CtyunAiAlertDto>>.Success(alerts);
        }
        catch (Exception ex)
        {
            return ServiceResponse<IReadOnlyList<CtyunAiAlertDto>>.Failure([], $"CTYun AI 告警解析失败：{ex.Message}");
        }
    }

    public ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>> GetDeviceAlerts(DeviceAlertQueryDto query)
    {
        var normalizedDeviceCode = query.DeviceCode?.Trim() ?? string.Empty;
        var normalizedAlertTypeList = string.IsNullOrWhiteSpace(query.AlertTypeList)
            ? _settings.OpenPlatform.AlarmApi.DeviceAlertTypeList?.Trim() ?? string.Empty
            : query.AlertTypeList.Trim();
        var normalizedAlertSource = query.AlertSource ?? _settings.OpenPlatform.AlarmApi.DeviceAlertSource;

        if (string.IsNullOrWhiteSpace(normalizedDeviceCode))
        {
            MapPointSourceDiagnostics.Write(
                "CTYunAlarm",
                "Device alarm request skipped: reason = missing_device_code");
            return ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Failure([], "设备编码为空，已跳过设备告警查询。");
        }

        if (string.IsNullOrWhiteSpace(normalizedAlertTypeList) || normalizedAlertSource <= 0)
        {
            MapPointSourceDiagnostics.Write(
                "CTYunAlarm",
                $"Device alarm request skipped: deviceCode = {normalizedDeviceCode}, reason = missing_required_alarm_parameters, alertSource = {normalizedAlertSource}, alertTypeList = {(string.IsNullOrWhiteSpace(normalizedAlertTypeList) ? "missing" : normalizedAlertTypeList)}");
            return ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Failure([], "设备告警请求缺少必填参数，已跳过调用。");
        }

        var cacheKey = BuildDeviceAlertCacheKey(
            normalizedDeviceCode,
            normalizedAlertTypeList,
            normalizedAlertSource,
            query.PageNo,
            query.PageSize);
        if (TryGetCachedDeviceAlerts(cacheKey, normalizedDeviceCode, out var cachedAlerts))
        {
            return cachedAlerts;
        }

        MapPointSourceDiagnostics.Write(
            "CTYunAlarm",
            $"Device alarm cache miss: deviceCode = {normalizedDeviceCode}, minimumRefreshIntervalSeconds = {DeviceAlertMinimumRefreshInterval.TotalSeconds:0}, cacheLifetimeSeconds = {DeviceAlertCacheLifetime.TotalSeconds:0}");

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("accessToken", GetTokenOrThrow()),
            new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
            new("deviceCode", normalizedDeviceCode),
            new("alertTypeList", normalizedAlertTypeList),
            new("alertSource", normalizedAlertSource.ToString(CultureInfo.InvariantCulture)),
            new("pageNo", query.PageNo.ToString(CultureInfo.InvariantCulture)),
            new("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture))
        };

        AppendTimeRange(parameters, query.StartTime, query.EndTime);
        parameters.AddRange(BuildParentUserParameter());

        MapPointSourceDiagnostics.Write(
            "CTYunAlarm",
            $"Calling device alarm API: path = {_settings.OpenPlatform.AlarmApi.DeviceAlertListPath}, deviceCode = {normalizedDeviceCode}, pageNo = {query.PageNo}, pageSize = {query.PageSize}, alertSource = {normalizedAlertSource}, alertTypeList = {normalizedAlertTypeList}");

        var response = SendProtectedRequest(_settings.OpenPlatform.AlarmApi.DeviceAlertListPath, parameters);
        if (!response.IsSuccess)
        {
            var failedResponse = ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Failure([], response.Message);
            CacheDeviceAlerts(cacheKey, failedResponse, DeviceAlertFailureCacheLifetime);
            return failedResponse;
        }

        try
        {
            using var document = JsonDocument.Parse(response.Data);
            var data = document.RootElement.GetProperty("data");
            var alerts = data
                .EnumerateArray()
                .Select(item => new CtyunDeviceAlertDto(
                    item.GetProperty("id").ToString(),
                    item.GetProperty("deviceCode").ToString(),
                    item.TryGetProperty("deviceName", out var deviceName) ? deviceName.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("alertType", out var alertType) ? alertType.GetInt32() : 0,
                    item.TryGetProperty("content", out var content) ? content.GetString() : null,
                    ParseDateTime(item.TryGetProperty("createTime", out var createTime) ? createTime.ToString() : null),
                    item.TryGetProperty("updateTime", out var updateTime) ? ParseNullableDateTime(updateTime.ToString()) : null,
                    item.TryGetProperty("status", out var status) ? status.GetInt32() : 0,
                    normalizedAlertSource))
                .ToList();

            var successResponse = ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Success(alerts);
            CacheDeviceAlerts(cacheKey, successResponse, DeviceAlertCacheLifetime);
            return successResponse;
        }
        catch (Exception ex)
        {
            var failedResponse = ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Failure([], $"CTYun 设备告警解析失败：{ex.Message}");
            CacheDeviceAlerts(cacheKey, failedResponse, DeviceAlertFailureCacheLifetime);
            return failedResponse;
        }
    }

    public ServiceResponse<CtyunPreviewStreamSetDto> GetH5PreviewStreamSet(string deviceCode)
    {
        const string path = "/open/token/vpaas/getH5StreamUrl";
        var normalizedDeviceCode = deviceCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDeviceCode))
        {
            return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                CreatePreviewStreamSetDiagnostic(path, string.Empty, 0, "设备编码为空，无法获取 H5 预览地址。"),
                "设备编码为空，无法获取 H5 预览地址。");
        }

        var requestParameters =
            new List<KeyValuePair<string, string>>
            {
                new("accessToken", GetTokenOrThrow()),
                new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
                new("deviceCode", normalizedDeviceCode),
                new("mediaType", "0"),
                new("mute", "0"),
                new("playerType", "0"),
                new("wasm", "1"),
                new("allLiveUrl", "1")
            };
        requestParameters.AddRange(BuildParentUserParameter());

        var response = SendProtectedRequestDetailed(path, requestParameters);
        if (!response.IsSuccess)
        {
            return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                CreatePreviewStreamSetDiagnostic(
                    path,
                    response.RequestPayloadSummary,
                    response.ResponseCode,
                    response.ResponseMessage,
                    responseContentType: response.ResponseContentType),
                response.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Payload);
            var payloadResolution = ResolvePreviewPayload(document.RootElement, path, response.Payload);
            if (!payloadResolution.IsSuccess)
            {
                MapPointSourceDiagnostics.Write(
                    "CTYunPreview",
                    $"path = {path}, responseContentType = {response.ResponseContentType}, responseBodyPreviewFirst300 = {payloadResolution.Diagnostics.ResponseBodyPreviewFirst300}, responseEnvelopeShape = {payloadResolution.Diagnostics.ResponseEnvelopeShape}, responseJsonTopLevelKeys = {payloadResolution.Diagnostics.ResponseJsonTopLevelKeys}, responseCandidateUrlKeys = {payloadResolution.Diagnostics.ResponseCandidateUrlKeys}, responseNestedPathTried = {payloadResolution.Diagnostics.ResponseNestedPathTried}");
                return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                    CreatePreviewStreamSetDiagnostic(
                        path,
                        response.RequestPayloadSummary,
                        response.ResponseCode,
                        "H5 预览接口未返回可解析的数据。",
                        payloadResolution.Diagnostics,
                        response.ResponseContentType),
                    "H5 预览接口未返回可解析的数据。");
            }

            var data = payloadResolution.Payload;
            var streamUrls = ExtractPreviewStreamUrls(data).ToList();
            var rawUrlFields = streamUrls
                .Select(item => item.RawStreamUrl)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            var fallbackPreviewCandidate = ExtractPreviewUrlCandidates(data).FirstOrDefault();
            var fallbackStreamUrl = fallbackPreviewCandidate?.Value ?? string.Empty;
            if (streamUrls.Count == 0 && !string.IsNullOrWhiteSpace(fallbackStreamUrl))
            {
                rawUrlFields.Add(fallbackStreamUrl);
                streamUrls.Add(new CtyunPreviewStreamUrlDto(
                    InferPreviewProtocol(fallbackStreamUrl, fallbackPreviewCandidate?.ProtocolType),
                    fallbackStreamUrl.Trim(),
                    null,
                    null)
                {
                    RawStreamUrl = fallbackStreamUrl,
                    ParsedProtocolType = fallbackPreviewCandidate?.ProtocolType ?? string.Empty,
                    MatchedFieldPath = fallbackPreviewCandidate?.Path ?? string.Empty,
                    DecryptMode = payloadResolution.Diagnostics.DecryptMode
                });
            }

            var firstStreamUrl = streamUrls.FirstOrDefault();
            MapPointSourceDiagnostics.Write(
                "CTYunPreview",
                $"path = {path}, responseContentType = {response.ResponseContentType}, responseBodyPreviewFirst300 = {payloadResolution.Diagnostics.ResponseBodyPreviewFirst300}, responseEnvelopeShape = {payloadResolution.Diagnostics.ResponseEnvelopeShape}, responseJsonTopLevelKeys = {payloadResolution.Diagnostics.ResponseJsonTopLevelKeys}, responseCandidateUrlKeys = {payloadResolution.Diagnostics.ResponseCandidateUrlKeys}, responseNestedPathTried = {payloadResolution.Diagnostics.ResponseNestedPathTried}, extractedStreamUrlCount = {streamUrls.Count}, extractedRawUrlCount = {rawUrlFields.Count}");

            return ServiceResponse<CtyunPreviewStreamSetDto>.Success(
                new CtyunPreviewStreamSetDto(
                    data.TryGetProperty("expireIn", out var expireInElement) && expireInElement.ValueKind == JsonValueKind.Number
                        ? expireInElement.GetInt32()
                        : null,
                    data.TryGetProperty("videoEnc", out var videoEncElement) && videoEncElement.ValueKind == JsonValueKind.Number
                        ? videoEncElement.GetInt32()
                        : null,
                    streamUrls)
                {
                    MediaApiPath = path,
                    RequestPayloadSummary = response.RequestPayloadSummary,
                    ResponseCode = response.ResponseCode,
                    ResponseMessage = response.ResponseMessage,
                    ResponseUrlFieldRaw = string.Join(" | ", rawUrlFields.Where(item => !string.IsNullOrWhiteSpace(item))),
                    ResponseContentType = response.ResponseContentType,
                    ResponseBodyPreviewFirst300 = payloadResolution.Diagnostics.ResponseBodyPreviewFirst300,
                    ResponseEnvelopeShape = payloadResolution.Diagnostics.ResponseEnvelopeShape,
                    ResponseJsonTopLevelKeys = payloadResolution.Diagnostics.ResponseJsonTopLevelKeys,
                    ResponseCandidateUrlKeys = payloadResolution.Diagnostics.ResponseCandidateUrlKeys,
                    ResponseNestedPathTried = payloadResolution.Diagnostics.ResponseNestedPathTried,
                    OriginalDataRaw = payloadResolution.Diagnostics.OriginalDataRaw,
                    MatchedFieldPath = firstStreamUrl?.MatchedFieldPath ?? payloadResolution.Diagnostics.MatchedFieldPath,
                    DecryptMode = payloadResolution.Diagnostics.DecryptMode,
                    ParsedProtocolType = firstStreamUrl?.ParsedProtocolType ?? payloadResolution.Diagnostics.ParsedProtocolType
                });
        }
        catch (Exception ex)
        {
            PreviewPayloadDiagnostics diagnostics;
            try
            {
                using var document = JsonDocument.Parse(response.Payload);
                diagnostics = ResolvePreviewPayload(document.RootElement, path, response.Payload).Diagnostics;
            }
            catch
            {
                diagnostics = PreviewPayloadDiagnostics.Empty with
                {
                    ResponseBodyPreviewFirst300 = BuildResponseBodyPreview(response.Payload)
                };
            }

            return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                CreatePreviewStreamSetDiagnostic(
                    path,
                    response.RequestPayloadSummary,
                    response.ResponseCode,
                    $"H5 预览地址解析失败：{ex.Message}",
                    diagnostics,
                    response.ResponseContentType),
                $"H5 预览地址解析失败：{ex.Message}");
        }
    }

    public ServiceResponse<CtyunPreviewMediaUrlDto> GetPreviewMediaUrl(string deviceCode, string path)
    {
        var normalizedDeviceCode = deviceCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDeviceCode))
        {
            return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                CreatePreviewMediaDiagnostic(path, string.Empty, 0, "设备编码为空，无法获取预览地址。"),
                "设备编码为空，无法获取预览地址。");
        }

        var requestParameters =
            new List<KeyValuePair<string, string>>
            {
                new("accessToken", GetTokenOrThrow()),
                new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
                new("deviceCode", normalizedDeviceCode),
                new("mediaType", "0"),
                new("supportDomain", "1"),
                new("mute", "0"),
                new("netType", "0"),
                new("expire", "300")
            };
        requestParameters.AddRange(BuildParentUserParameter());

        var response = SendProtectedRequestDetailed(path, requestParameters);
        if (!response.IsSuccess)
        {
            return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                CreatePreviewMediaDiagnostic(
                    path,
                    response.RequestPayloadSummary,
                    response.ResponseCode,
                    response.ResponseMessage,
                    responseContentType: response.ResponseContentType),
                response.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Payload);
            var payloadResolution = ResolvePreviewPayload(document.RootElement, path, response.Payload);
            if (!payloadResolution.IsSuccess)
            {
                MapPointSourceDiagnostics.Write(
                    "CTYunPreview",
                    $"path = {path}, responseContentType = {response.ResponseContentType}, responseBodyPreviewFirst300 = {payloadResolution.Diagnostics.ResponseBodyPreviewFirst300}, responseEnvelopeShape = {payloadResolution.Diagnostics.ResponseEnvelopeShape}, responseJsonTopLevelKeys = {payloadResolution.Diagnostics.ResponseJsonTopLevelKeys}, responseCandidateUrlKeys = {payloadResolution.Diagnostics.ResponseCandidateUrlKeys}, responseNestedPathTried = {payloadResolution.Diagnostics.ResponseNestedPathTried}");
                return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                    CreatePreviewMediaDiagnostic(
                        path,
                        response.RequestPayloadSummary,
                        response.ResponseCode,
                        "预览地址接口未返回可解析的数据。",
                        payloadResolution.Diagnostics,
                        response.ResponseContentType),
                    "预览地址接口未返回可解析的数据。");
            }

            var data = payloadResolution.Payload;
            var previewCandidates = ExtractPreviewUrlCandidates(data);
            var previewCandidate = previewCandidates.FirstOrDefault();
            var responseUrlFieldRaw = previewCandidate?.Value ?? string.Empty;
            var previewUrl = responseUrlFieldRaw.Trim();
            MapPointSourceDiagnostics.Write(
                "CTYunPreview",
                $"path = {path}, responseContentType = {response.ResponseContentType}, responseBodyPreviewFirst300 = {payloadResolution.Diagnostics.ResponseBodyPreviewFirst300}, responseEnvelopeShape = {payloadResolution.Diagnostics.ResponseEnvelopeShape}, responseJsonTopLevelKeys = {payloadResolution.Diagnostics.ResponseJsonTopLevelKeys}, responseCandidateUrlKeys = {payloadResolution.Diagnostics.ResponseCandidateUrlKeys}, responseNestedPathTried = {payloadResolution.Diagnostics.ResponseNestedPathTried}, decryptMode = {payloadResolution.Diagnostics.DecryptMode}, matchedFieldPath = {previewCandidate?.Path ?? "null"}, parsedPreviewUrl = {(string.IsNullOrWhiteSpace(previewUrl) ? "null" : previewUrl)}");
            return ServiceResponse<CtyunPreviewMediaUrlDto>.Success(
                new CtyunPreviewMediaUrlDto(
                    previewUrl,
                    data.TryGetProperty("expireTime", out var expireTimeElement) && expireTimeElement.ValueKind == JsonValueKind.Number
                        ? expireTimeElement.GetInt32()
                        : null)
                {
                    MediaApiPath = path,
                    RequestPayloadSummary = response.RequestPayloadSummary,
                    ResponseCode = response.ResponseCode,
                    ResponseMessage = response.ResponseMessage,
                    ResponseUrlFieldRaw = responseUrlFieldRaw,
                    ParsedPreviewUrl = previewUrl,
                    ResponseContentType = response.ResponseContentType,
                    ResponseBodyPreviewFirst300 = payloadResolution.Diagnostics.ResponseBodyPreviewFirst300,
                    ResponseEnvelopeShape = payloadResolution.Diagnostics.ResponseEnvelopeShape,
                    ResponseJsonTopLevelKeys = payloadResolution.Diagnostics.ResponseJsonTopLevelKeys,
                    ResponseCandidateUrlKeys = payloadResolution.Diagnostics.ResponseCandidateUrlKeys,
                    ResponseNestedPathTried = payloadResolution.Diagnostics.ResponseNestedPathTried,
                    OriginalDataRaw = payloadResolution.Diagnostics.OriginalDataRaw,
                    MatchedFieldPath = previewCandidate?.Path ?? string.Empty,
                    DecryptMode = payloadResolution.Diagnostics.DecryptMode,
                    ParsedProtocolType = previewCandidate?.ProtocolType ?? string.Empty
                });
        }
        catch (Exception ex)
        {
            PreviewPayloadDiagnostics diagnostics;
            try
            {
                using var document = JsonDocument.Parse(response.Payload);
                diagnostics = ResolvePreviewPayload(document.RootElement, path, response.Payload).Diagnostics;
            }
            catch
            {
                diagnostics = PreviewPayloadDiagnostics.Empty with
                {
                    ResponseBodyPreviewFirst300 = BuildResponseBodyPreview(response.Payload)
                };
            }

            return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                CreatePreviewMediaDiagnostic(
                    path,
                    response.RequestPayloadSummary,
                    response.ResponseCode,
                    $"预览地址解析失败：{ex.Message}",
                    diagnostics,
                    response.ResponseContentType),
                $"预览地址解析失败：{ex.Message}");
        }
    }

    private ServiceResponse<string> SendProtectedRequest(string path, IReadOnlyList<KeyValuePair<string, string>> businessParameters)
    {
        var response = SendProtectedRequestDetailed(path, businessParameters);
        return response.IsSuccess
            ? ServiceResponse<string>.Success(response.Payload)
            : ServiceResponse<string>.Failure(string.Empty, response.Message);
    }

    private ProtectedApiResponse SendProtectedRequestDetailed(string path, IReadOnlyList<KeyValuePair<string, string>> businessParameters)
    {
        var requestPayloadSummary = SummarizeBusinessParametersForLog(businessParameters);

        try
        {
            var requestParameters = new List<KeyValuePair<string, string>>
            {
                new("appId", _settings.OpenPlatform.AppId),
                new("clientType", _settings.OpenPlatform.ClientType),
                new("params", CtyunSecurity.EncryptParams(businessParameters, _settings.OpenPlatform.AppSecret)),
                new("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)),
                new("version", _settings.OpenPlatform.Version)
            };
            requestParameters.Add(new("signature", CtyunSecurity.BuildSignature(requestParameters, _settings.OpenPlatform.AppSecret)));

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(_settings.OpenPlatform.BaseUrl, path))
            {
                Content = new FormUrlEncodedContent(requestParameters)
            };
            request.Headers.TryAddWithoutValidation("apiVersion", _settings.OpenPlatform.ApiVersion);

            using var response = _httpClient.Send(request);
            var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var responseContentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
            MapPointSourceDiagnostics.Write(
                "CTYunHttp",
                $"Protected API responded: path = {path}, status = {(int)response.StatusCode} {response.StatusCode}");
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("code", out var codeElement) || codeElement.ValueKind != JsonValueKind.Number)
            {
                var topLevelProperties = DescribeTopLevelProperties(root);
                MapPointSourceDiagnostics.Write(
                    "CTYunHttp",
                    $"Protected API response missing code field: path = {path}, topLevelProperties = {topLevelProperties}");
                return new ProtectedApiResponse(
                    false,
                    payload,
                    $"CTYun 接口响应缺少 code 字段：path = {path}，顶层字段 = {topLevelProperties}",
                    -2,
                    $"missing_code:{topLevelProperties}",
                    responseContentType,
                    path,
                    requestPayloadSummary);
            }

            var code = codeElement.GetInt32();
            var message = root.TryGetProperty("msg", out var messageElement)
                ? messageElement.GetString() ?? "未知错误"
                : "未知错误";
            if (code != 0)
            {
                MapPointSourceDiagnostics.Write(
                    "CTYunHttp",
                    $"Protected API returned business failure: path = {path}, code = {code}, message = {message}");
                return new ProtectedApiResponse(
                    false,
                    payload,
                    $"CTYun 接口调用失败：path = {path}，code = {code}，message = {message}",
                    code,
                    message,
                    responseContentType,
                    path,
                    requestPayloadSummary);
            }

            return new ProtectedApiResponse(
                true,
                payload,
                string.Empty,
                code,
                message,
                responseContentType,
                path,
                requestPayloadSummary);
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "CTYunHttp",
                $"Protected API exception: path = {path}, type = {ex.GetType().Name}, message = {ex.Message}");
            return new ProtectedApiResponse(
                false,
                string.Empty,
                $"CTYun 接口请求异常：path = {path}，{ex.Message}",
                -1,
                ex.Message,
                string.Empty,
                path,
                requestPayloadSummary);
        }
    }

    private string GetTokenOrThrow()
    {
        var tokenResponse = _accessTokenService.GetAccessToken();
        if (!tokenResponse.IsSuccess || string.IsNullOrWhiteSpace(tokenResponse.Data.TokenValue))
        {
            throw new InvalidOperationException(tokenResponse.Message);
        }

        return tokenResponse.Data.TokenValue;
    }

    private IEnumerable<KeyValuePair<string, string>> BuildParentUserParameter()
    {
        if (string.IsNullOrWhiteSpace(_settings.OpenPlatform.ParentUser))
        {
            return [];
        }

        return [new("parentUser", _settings.OpenPlatform.ParentUser)];
    }

    private static CtyunPreviewMediaUrlDto CreatePreviewMediaDiagnostic(
        string path,
        string requestPayloadSummary,
        int responseCode,
        string responseMessage,
        PreviewPayloadDiagnostics? diagnostics = null,
        string responseContentType = "")
    {
        return new CtyunPreviewMediaUrlDto(string.Empty, null)
        {
            MediaApiPath = path,
            RequestPayloadSummary = requestPayloadSummary,
            ResponseCode = responseCode,
            ResponseMessage = responseMessage ?? string.Empty,
            ResponseUrlFieldRaw = string.Empty,
            ParsedPreviewUrl = string.Empty,
            ResponseContentType = responseContentType,
            ResponseBodyPreviewFirst300 = diagnostics?.ResponseBodyPreviewFirst300 ?? string.Empty,
            ResponseEnvelopeShape = diagnostics?.ResponseEnvelopeShape ?? string.Empty,
            ResponseJsonTopLevelKeys = diagnostics?.ResponseJsonTopLevelKeys ?? string.Empty,
            ResponseCandidateUrlKeys = diagnostics?.ResponseCandidateUrlKeys ?? string.Empty,
            ResponseNestedPathTried = diagnostics?.ResponseNestedPathTried ?? string.Empty,
            OriginalDataRaw = diagnostics?.OriginalDataRaw ?? string.Empty,
            MatchedFieldPath = diagnostics?.MatchedFieldPath ?? string.Empty,
            DecryptMode = diagnostics?.DecryptMode ?? string.Empty,
            ParsedProtocolType = diagnostics?.ParsedProtocolType ?? string.Empty
        };
    }

    private static CtyunPreviewStreamSetDto CreatePreviewStreamSetDiagnostic(
        string path,
        string requestPayloadSummary,
        int responseCode,
        string responseMessage,
        PreviewPayloadDiagnostics? diagnostics = null,
        string responseContentType = "")
    {
        return new CtyunPreviewStreamSetDto(null, null, [])
        {
            MediaApiPath = path,
            RequestPayloadSummary = requestPayloadSummary,
            ResponseCode = responseCode,
            ResponseMessage = responseMessage ?? string.Empty,
            ResponseUrlFieldRaw = string.Empty,
            ResponseContentType = responseContentType,
            ResponseBodyPreviewFirst300 = diagnostics?.ResponseBodyPreviewFirst300 ?? string.Empty,
            ResponseEnvelopeShape = diagnostics?.ResponseEnvelopeShape ?? string.Empty,
            ResponseJsonTopLevelKeys = diagnostics?.ResponseJsonTopLevelKeys ?? string.Empty,
            ResponseCandidateUrlKeys = diagnostics?.ResponseCandidateUrlKeys ?? string.Empty,
            ResponseNestedPathTried = diagnostics?.ResponseNestedPathTried ?? string.Empty,
            OriginalDataRaw = diagnostics?.OriginalDataRaw ?? string.Empty,
            MatchedFieldPath = diagnostics?.MatchedFieldPath ?? string.Empty,
            DecryptMode = diagnostics?.DecryptMode ?? string.Empty,
            ParsedProtocolType = diagnostics?.ParsedProtocolType ?? string.Empty
        };
    }

    private static string SummarizeBusinessParametersForLog(IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        if (parameters.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ", ",
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Key))
                .Select(parameter => $"{parameter.Key}={SanitizeParameterValueForLog(parameter.Key, parameter.Value)}"));
    }

    private static string SanitizeParameterValueForLog(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty";
        }

        return key switch
        {
            "accessToken" => "redacted",
            "enterpriseUser" => "masked",
            "parentUser" => "masked",
            _ => value.Trim()
        };
    }

    private PreviewPayloadResolution ResolvePreviewPayload(JsonElement root, string path, string rawPayload)
    {
        var topLevelKeys = DescribeTopLevelProperties(root);
        var nestedPaths = new List<string>();
        var envelopeShape = new List<string>();
        var candidateUrlKeys = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<PreviewJsonCandidate>();
        queue.Enqueue(new PreviewJsonCandidate("root", root.Clone(), "none", ReadElementRawText(root)));
        EnqueuePrimaryPreviewCandidate(queue, root);

        while (queue.Count > 0 && visited.Count < 64)
        {
            var candidate = queue.Dequeue();
            if (!visited.Add(candidate.Path))
            {
                continue;
            }

            nestedPaths.Add(candidate.Path);
            envelopeShape.Add(DescribePreviewNode(candidate.Path, candidate.Element));
            CollectPreviewUrlKeys(candidate.Element, candidate.Path, candidateUrlKeys, 0);

            if (LooksLikePreviewPayload(candidate.Element))
            {
                var primaryUrlCandidate = ExtractPreviewUrlCandidates(candidate.Element).FirstOrDefault();
                var diagnostics = CreatePreviewPayloadDiagnostics(
                    rawPayload,
                    topLevelKeys,
                    envelopeShape,
                    candidateUrlKeys,
                    nestedPaths,
                    candidate.OriginalDataRaw,
                    primaryUrlCandidate?.Path ?? string.Empty,
                    primaryUrlCandidate?.ProtocolType ?? string.Empty,
                    candidate.DecryptMode);
                MapPointSourceDiagnostics.Write(
                    "CTYunPreview",
                    $"Preview payload resolved: path = {path}, responseEnvelopeShape = {diagnostics.ResponseEnvelopeShape}, responseJsonTopLevelKeys = {diagnostics.ResponseJsonTopLevelKeys}, responseCandidateUrlKeys = {diagnostics.ResponseCandidateUrlKeys}, responseNestedPathTried = {diagnostics.ResponseNestedPathTried}, decryptMode = {diagnostics.DecryptMode}, matchedFieldPath = {diagnostics.MatchedFieldPath}");
                return new PreviewPayloadResolution(true, candidate.Element.Clone(), diagnostics);
            }

            EnqueuePreviewChildren(queue, candidate);
        }

        var failedDiagnostics = CreatePreviewPayloadDiagnostics(
            rawPayload,
            topLevelKeys,
            envelopeShape,
            candidateUrlKeys,
            nestedPaths,
            root.TryGetProperty("data", out var dataElement) ? ReadElementRawText(dataElement) : string.Empty,
            string.Empty,
            string.Empty,
            "unresolved");
        MapPointSourceDiagnostics.Write(
            "CTYunPreview",
            $"Preview payload remained unreadable: path = {path}, responseEnvelopeShape = {failedDiagnostics.ResponseEnvelopeShape}, responseJsonTopLevelKeys = {failedDiagnostics.ResponseJsonTopLevelKeys}, responseCandidateUrlKeys = {failedDiagnostics.ResponseCandidateUrlKeys}, responseNestedPathTried = {failedDiagnostics.ResponseNestedPathTried}, responseBodyPreviewFirst300 = {failedDiagnostics.ResponseBodyPreviewFirst300}, decryptMode = {failedDiagnostics.DecryptMode}");
        return new PreviewPayloadResolution(false, default, failedDiagnostics);
    }

    private void EnqueuePrimaryPreviewCandidate(Queue<PreviewJsonCandidate> queue, JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataElement)
            || dataElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        var dataRawText = ReadElementRawText(dataElement);
        switch (dataElement.ValueKind)
        {
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                queue.Enqueue(new PreviewJsonCandidate("root.data", dataElement.Clone(), "none", dataRawText));
                break;
            case JsonValueKind.String when TryResolvePreviewTextPayload(dataElement.GetString(), out var resolved):
                using (var nested = JsonDocument.Parse(resolved.JsonPayload))
                {
                    queue.Enqueue(new PreviewJsonCandidate($"root.data[{resolved.ResolveMode}]", nested.RootElement.Clone(), resolved.DecryptMode, dataRawText));
                }

                break;
        }
    }

    private void EnqueuePreviewChildren(
        Queue<PreviewJsonCandidate> queue,
        PreviewJsonCandidate candidate)
    {
        switch (candidate.Element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in candidate.Element.EnumerateObject().Take(16))
                {
                    var nextPath = $"{candidate.Path}.{property.Name}";
                    var rawText = ReadElementRawText(property.Value);
                    switch (property.Value.ValueKind)
                    {
                        case JsonValueKind.Object:
                        case JsonValueKind.Array:
                            queue.Enqueue(new PreviewJsonCandidate(nextPath, property.Value.Clone(), candidate.DecryptMode, rawText));
                            break;
                        case JsonValueKind.String when TryResolvePreviewTextPayload(property.Value.GetString(), out var resolved):
                            using (var nested = JsonDocument.Parse(resolved.JsonPayload))
                            {
                                queue.Enqueue(new PreviewJsonCandidate($"{nextPath}[{resolved.ResolveMode}]", nested.RootElement.Clone(), resolved.DecryptMode, rawText));
                            }

                            break;
                    }
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in candidate.Element.EnumerateArray().Take(8))
                {
                    var nextPath = $"{candidate.Path}[{index}]";
                    var rawText = ReadElementRawText(item);
                    switch (item.ValueKind)
                    {
                        case JsonValueKind.Object:
                        case JsonValueKind.Array:
                            queue.Enqueue(new PreviewJsonCandidate(nextPath, item.Clone(), candidate.DecryptMode, rawText));
                            break;
                        case JsonValueKind.String when TryResolvePreviewTextPayload(item.GetString(), out var resolved):
                            using (var nested = JsonDocument.Parse(resolved.JsonPayload))
                            {
                                queue.Enqueue(new PreviewJsonCandidate($"{nextPath}[{resolved.ResolveMode}]", nested.RootElement.Clone(), resolved.DecryptMode, rawText));
                            }

                            break;
                    }

                    index++;
                }

                break;
        }
    }

    private static PreviewPayloadDiagnostics CreatePreviewPayloadDiagnostics(
        string rawPayload,
        string topLevelKeys,
        IReadOnlyList<string> envelopeShape,
        IReadOnlyCollection<string> candidateUrlKeys,
        IReadOnlyList<string> nestedPaths,
        string originalDataRaw,
        string matchedFieldPath,
        string parsedProtocolType,
        string decryptMode)
    {
        return new PreviewPayloadDiagnostics(
            BuildResponseBodyPreview(rawPayload),
            string.Join(" -> ", envelopeShape.Where(item => !string.IsNullOrWhiteSpace(item)).Take(12)),
            topLevelKeys,
            string.Join(" | ", candidateUrlKeys.Where(item => !string.IsNullOrWhiteSpace(item)).Take(16)),
            string.Join(" -> ", nestedPaths.Where(item => !string.IsNullOrWhiteSpace(item)).Take(16)),
            BuildResponseBodyPreview(originalDataRaw),
            matchedFieldPath,
            string.IsNullOrWhiteSpace(parsedProtocolType) ? "unknown" : parsedProtocolType,
            string.IsNullOrWhiteSpace(decryptMode) ? "none" : decryptMode);
    }

    private static string BuildResponseBodyPreview(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        var normalized = payload
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return normalized.Length <= 300 ? normalized : normalized[..300];
    }

    private static string ReadElementRawText(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();
    }

    private static string DescribePreviewNode(string path, JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            ? $"{path}:{element.ValueKind}({string.Join(",", element.EnumerateObject().Select(property => property.Name).Take(8))})"
            : $"{path}:{element.ValueKind}";
    }

    private static bool LooksLikePreviewPayload(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Any(property =>
                    string.Equals(property.Name, "streamUrls", StringComparison.OrdinalIgnoreCase)
                    || PreviewUrlPropertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                || ExtractPreviewUrlCandidates(element).Count > 0,
            JsonValueKind.Array => element.EnumerateArray().Take(8).Any(LooksLikePreviewPayload),
            _ => false
        };
    }

    private static IReadOnlyList<CtyunPreviewStreamUrlDto> ExtractPreviewStreamUrls(JsonElement element)
    {
        var streamUrls = new List<CtyunPreviewStreamUrlDto>();
        CollectPreviewStreamUrls(element, streamUrls, 0);
        return streamUrls
            .GroupBy(item => item.StreamUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static void CollectPreviewStreamUrls(JsonElement element, ICollection<CtyunPreviewStreamUrlDto> streamUrls, int depth)
    {
        if (depth > 5)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("streamUrls", out var streamUrlsElement)
                    && streamUrlsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in streamUrlsElement.EnumerateArray())
                    {
                        var previewCandidate = ExtractPreviewUrlCandidates(item).FirstOrDefault();
                        var rawStreamUrl = previewCandidate?.Value ?? string.Empty;
                        var streamUrl = rawStreamUrl.Trim();
                        if (string.IsNullOrWhiteSpace(streamUrl))
                        {
                            continue;
                        }

                        var protocolType = item.TryGetProperty("protocol", out var protocolElement)
                            ? InferPreviewProtocolName(streamUrl, protocolElement.ToString())
                            : previewCandidate?.ProtocolType ?? InferPreviewProtocolName(streamUrl, string.Empty);
                        streamUrls.Add(new CtyunPreviewStreamUrlDto(
                            item.TryGetProperty("protocol", out var numericProtocolElement) && numericProtocolElement.ValueKind == JsonValueKind.Number
                                ? numericProtocolElement.GetInt32()
                                : InferPreviewProtocol(streamUrl, protocolType),
                            streamUrl,
                            ReadOptionalTextLocal(item, "ipv6StreamUrl"),
                            item.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.Number
                                ? levelElement.GetInt32()
                                : null)
                        {
                            RawStreamUrl = rawStreamUrl,
                            ParsedProtocolType = protocolType,
                            MatchedFieldPath = previewCandidate?.Path ?? string.Empty,
                            DecryptMode = previewCandidate?.DecryptMode ?? string.Empty
                        });
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectPreviewStreamUrls(property.Value, streamUrls, depth + 1);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray().Take(8))
                {
                    CollectPreviewStreamUrls(item, streamUrls, depth + 1);
                }

                break;
        }
    }

    private static IReadOnlyList<PreviewUrlCandidate> ExtractPreviewUrlCandidates(JsonElement element)
    {
        var candidates = new List<PreviewUrlCandidate>();
        CollectPreviewUrlCandidates(element, "payload", candidates, 0);
        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Value))
            .OrderBy(candidate => GetPreviewUrlPriority(candidate.PropertyName))
            .ThenBy(candidate => candidate.Depth)
            .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static void CollectPreviewUrlCandidates(
        JsonElement element,
        string path,
        ICollection<PreviewUrlCandidate> candidates,
        int depth)
    {
        if (depth > 5)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextPath = $"{path}.{property.Name}";
                    if (PreviewUrlPropertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var value = ReadOptionalTextLocal(element, property.Name) ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            candidates.Add(new PreviewUrlCandidate(
                                property.Name,
                                nextPath,
                                value,
                                depth,
                                InferPreviewProtocolName(value, property.Name),
                                "none"));
                        }
                    }

                    CollectPreviewUrlCandidates(property.Value, nextPath, candidates, depth + 1);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray().Take(8))
                {
                    CollectPreviewUrlCandidates(item, $"{path}[{index}]", candidates, depth + 1);
                    index++;
                }

                break;
        }
    }

    private static void CollectPreviewUrlKeys(JsonElement element, string path, ISet<string> keys, int depth)
    {
        if (depth > 5)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextPath = $"{path}.{property.Name}";
                    if (string.Equals(property.Name, "streamUrls", StringComparison.OrdinalIgnoreCase)
                        || PreviewUrlPropertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        keys.Add(nextPath);
                    }

                    CollectPreviewUrlKeys(property.Value, nextPath, keys, depth + 1);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray().Take(8))
                {
                    CollectPreviewUrlKeys(item, $"{path}[{index}]", keys, depth + 1);
                    index++;
                }

                break;
        }
    }

    private bool TryResolvePreviewTextPayload(string? text, out ResolvedPreviewTextPayload resolvedPayload)
    {
        resolvedPayload = default;

        if (TryNormalizePreviewResolvedText(text, "none", out resolvedPayload))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(text) && CtyunRsaDecryptor.IsHexCipherText(text))
        {
            if (TryDecryptPayloadWithRsa(text, out var rsaPlainText, out var rsaDecryptMode)
                && TryNormalizePreviewResolvedText(rsaPlainText, rsaDecryptMode, out resolvedPayload))
            {
                return true;
            }

            if (TryDecodeHexText(text, out var hexPlainText)
                && TryNormalizePreviewResolvedText(hexPlainText, "hex-utf8", out resolvedPayload))
            {
                return true;
            }

            if (TryDecryptPayloadWithXxTea(text, out var xxTeaPlainText)
                && TryNormalizePreviewResolvedText(xxTeaPlainText, "xxtea-hex", out resolvedPayload))
            {
                return true;
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(text)
            && LooksLikeBase64Text(text)
            && TryDecryptPayloadWithRsa(text, out var base64RsaPlainText, out var base64RsaDecryptMode)
            && TryNormalizePreviewResolvedText(base64RsaPlainText, base64RsaDecryptMode, out resolvedPayload))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(text)
            && TryDecodeBase64Text(text, out var base64PlainText)
            && TryNormalizePreviewResolvedText(base64PlainText, "base64-utf8", out resolvedPayload))
        {
            return true;
        }

        return false;
    }

    private static bool TryNormalizePreviewResolvedText(
        string? text,
        string decryptMode,
        out ResolvedPreviewTextPayload resolvedPayload)
    {
        resolvedPayload = default;
        if (TryNormalizeJsonText(text, out var jsonPayload, out var resolveMode))
        {
            resolvedPayload = new ResolvedPreviewTextPayload(
                jsonPayload,
                resolveMode,
                string.IsNullOrWhiteSpace(decryptMode) ? "none" : decryptMode);
            return true;
        }

        if (TryExtractPreviewUrlText(text, out var previewUrl))
        {
            resolvedPayload = new ResolvedPreviewTextPayload(
                JsonSerializer.Serialize(new { url = previewUrl }),
                "plain-url",
                string.IsNullOrWhiteSpace(decryptMode) ? "none" : decryptMode);
            return true;
        }

        return false;
    }

    private static bool TryDecodeHexText(string? text, out string decodedText)
    {
        decodedText = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        if ((normalized.Length & 1) != 0
            || !normalized.All(Uri.IsHexDigit))
        {
            return false;
        }

        try
        {
            decodedText = Encoding.UTF8.GetString(Convert.FromHexString(normalized)).Trim();
            return !string.IsNullOrWhiteSpace(decodedText);
        }
        catch
        {
            decodedText = string.Empty;
            return false;
        }
    }

    private static bool TryDecodeBase64Text(string? text, out string decodedText)
    {
        decodedText = string.Empty;
        if (!LooksLikeBase64Text(text))
        {
            return false;
        }

        try
        {
            decodedText = Encoding.UTF8.GetString(Convert.FromBase64String(NormalizeBase64Text(text!))).Trim();
            return !string.IsNullOrWhiteSpace(decodedText);
        }
        catch
        {
            decodedText = string.Empty;
            return false;
        }
    }

    private static bool TryExtractPreviewUrlText(string? text, out string previewUrl)
    {
        previewUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        var httpIndex = normalized.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
        var httpsIndex = normalized.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
        var startIndex = httpIndex switch
        {
            >= 0 when httpsIndex >= 0 => Math.Min(httpIndex, httpsIndex),
            >= 0 => httpIndex,
            _ => httpsIndex
        };

        if (startIndex < 0)
        {
            return false;
        }

        var urlCandidate = normalized[startIndex..]
            .Split(['"', '\'', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(urlCandidate)
            || !Uri.TryCreate(urlCandidate.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        previewUrl = uri.AbsoluteUri;
        return true;
    }

    private static bool LooksLikeBase64Text(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        if ((normalized.Length & 3) != 0 && !normalized.Contains('='))
        {
            return false;
        }

        return normalized.All(ch =>
            char.IsLetterOrDigit(ch)
            || ch is '+' or '/' or '='
            || char.IsWhiteSpace(ch));
    }

    private static bool TryNormalizeJsonText(string? text, out string jsonPayload, out string resolveMode)
    {
        jsonPayload = string.Empty;
        resolveMode = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var current = text.Trim();
        var modes = new List<string>();
        for (var depth = 0; depth < 5; depth++)
        {
            if (current.StartsWith("{", StringComparison.Ordinal)
                || current.StartsWith("[", StringComparison.Ordinal))
            {
                jsonPayload = current;
                resolveMode = modes.Count == 0 ? "plain-json" : string.Join(">", modes);
                return true;
            }

            if (!current.StartsWith("\"", StringComparison.Ordinal)
                || !current.EndsWith("\"", StringComparison.Ordinal))
            {
                break;
            }

            try
            {
                var unescaped = JsonSerializer.Deserialize<string>(current);
                if (string.IsNullOrWhiteSpace(unescaped))
                {
                    break;
                }

                modes.Add($"json-string:{depth + 1}");
                current = unescaped.Trim();
            }
            catch
            {
                break;
            }
        }

        return false;
    }

    private bool TryDecryptPayloadWithRsa(string cipherText, out string plainText, out string decryptMode)
    {
        plainText = string.Empty;
        decryptMode = string.Empty;
        if (string.IsNullOrWhiteSpace(_settings.OpenPlatform.RsaPrivateKey))
        {
            return false;
        }

        try
        {
            plainText = CtyunRsaDecryptor.Decrypt(cipherText, _settings.OpenPlatform.RsaPrivateKey);
            if (!string.IsNullOrWhiteSpace(plainText))
            {
                decryptMode = CtyunRsaDecryptor.IsHexCipherText(cipherText)
                    ? "rsa-hex-pkcs1"
                    : "rsa-base64-pkcs1";
                return true;
            }
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "CTYunPreview",
                $"RSA preview payload decryption failed: {ex.Message}");
        }

        return false;
    }

    private bool TryDecryptPayloadWithXxTea(string cipherText, out string plainText)
    {
        plainText = string.Empty;
        try
        {
            var cipherBytes = Convert.FromHexString(cipherText);
            var keyBytes = Encoding.UTF8.GetBytes(_settings.OpenPlatform.AppSecret);
            var plainBytes = XXTeaDecrypt(cipherBytes, keyBytes);
            if (plainBytes.Length == 0)
            {
                return false;
            }

            plainText = Encoding.UTF8.GetString(plainBytes).Trim();
            return !string.IsNullOrWhiteSpace(plainText);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeBase64Text(string base64)
    {
        var normalized = base64.Trim()
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace(' ', '+');
        var padding = normalized.Length % 4;
        return padding == 0 ? normalized : normalized.PadRight(normalized.Length + (4 - padding), '=');
    }

    private static int InferPreviewProtocol(string previewUrl, string? protocolType = null)
    {
        return (protocolType ?? InferPreviewProtocolName(previewUrl, string.Empty)).ToLowerInvariant() switch
        {
            "webrtc" => 3,
            "flv" => 2,
            "hls" => 1,
            _ => 0
        };
    }

    private static string InferPreviewProtocolName(string previewUrl, string? hint)
    {
        var normalizedHint = hint?.Trim() ?? string.Empty;
        if (normalizedHint.Contains("rtc", StringComparison.OrdinalIgnoreCase)
            || normalizedHint.Contains("webrtc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHint, "3", StringComparison.Ordinal))
        {
            return "webrtc";
        }

        if (normalizedHint.Contains("flv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHint, "2", StringComparison.Ordinal))
        {
            return "flv";
        }

        if (normalizedHint.Contains("hls", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedHint, "1", StringComparison.Ordinal))
        {
            return "hls";
        }

        if (string.IsNullOrWhiteSpace(previewUrl))
        {
            return "unknown";
        }

        var normalizedPreviewUrl = previewUrl.Trim();
        if (normalizedPreviewUrl.Contains("webrtc", StringComparison.OrdinalIgnoreCase)
            || normalizedPreviewUrl.Contains("rtc", StringComparison.OrdinalIgnoreCase))
        {
            return "webrtc";
        }

        var previewPath = GetPreviewUrlPath(normalizedPreviewUrl);
        if (previewPath.EndsWith(".flv", StringComparison.OrdinalIgnoreCase))
        {
            return "flv";
        }

        if (previewPath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return "hls";
        }

        return "unknown";
    }

    private static string GetPreviewUrlPath(string previewUrl)
    {
        if (Uri.TryCreate(previewUrl, UriKind.Absolute, out var previewUri))
        {
            return previewUri.AbsolutePath;
        }

        var queryIndex = previewUrl.IndexOfAny(['?', '#']);
        return queryIndex >= 0
            ? previewUrl[..queryIndex]
            : previewUrl;
    }

    private static int GetPreviewUrlPriority(string propertyName)
    {
        return propertyName.ToLowerInvariant() switch
        {
            "rtcurl" => 0,
            "webrtcurl" => 1,
            "flvurl" => 2,
            "hlsurl" => 3,
            "previewurl" => 4,
            "playurl" => 5,
            "streamurl" => 6,
            "mediaurl" => 7,
            "url" => 8,
            _ => 9
        };
    }

    private static string? ReadOptionalTextLocal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => value.ToString()
        };
    }

    private static byte[] XXTeaDecrypt(byte[] cipherData, byte[] key)
    {
        return XXTeaToByteArray(XXTeaDecrypt(XXTeaToIntArray(cipherData, false), XXTeaToIntArray(key, false)), true);
    }

    private static uint[] XXTeaDecrypt(uint[] v, uint[] k)
    {
        var n = v.Length - 1;
        if (n < 1)
        {
            return v;
        }

        if (k.Length < 4)
        {
            Array.Resize(ref k, 4);
        }

        uint z;
        uint y = v[0];
        const uint delta = 0x9E3779B9;
        var q = 6 + 52 / (n + 1);
        var sum = (uint)(q * delta);

        while (sum != 0)
        {
            var e = (sum >> 2) & 3;
            for (var p = n; p > 0; p--)
            {
                z = v[p - 1];
                y = v[p] -= XXTeaMx(sum, y, z, p, e, k);
            }

            z = v[n];
            y = v[0] -= XXTeaMx(sum, y, z, 0, e, k);
            sum -= delta;
        }

        return v;
    }

    private static uint XXTeaMx(uint sum, uint y, uint z, int p, uint e, uint[] k)
    {
        return ((z >> 5) ^ (y << 2)) + ((y >> 3) ^ (z << 4)) ^ (sum ^ y) + (k[(p & 3) ^ e] ^ z);
    }

    private static uint[] XXTeaToIntArray(byte[] data, bool includeLength)
    {
        var n = (data.Length & 3) == 0 ? data.Length >> 2 : (data.Length >> 2) + 1;
        var result = includeLength ? new uint[n + 1] : new uint[n];
        if (includeLength)
        {
            result[n] = (uint)data.Length;
        }

        for (var i = 0; i < data.Length; i++)
        {
            result[i >> 2] |= (uint)data[i] << ((i & 3) << 3);
        }

        return result;
    }

    private static byte[] XXTeaToByteArray(uint[] data, bool includeLength)
    {
        var n = data.Length << 2;
        if (includeLength)
        {
            var m = (int)data[^1];
            if (m > n || m < 0)
            {
                return [];
            }

            n = m;
        }

        var result = new byte[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = (byte)((data[i >> 2] >> ((i & 3) << 3)) & 0xFF);
        }

        return result;
    }

    private static void AppendTimeRange(List<KeyValuePair<string, string>> parameters, DateTime? startTime, DateTime? endTime)
    {
        if (startTime.HasValue)
        {
            parameters.Add(new("startTime", FormatDateTime(startTime.Value)));
        }

        if (endTime.HasValue)
        {
            parameters.Add(new("endTime", FormatDateTime(endTime.Value)));
        }
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static bool ReadBooleanLike(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.GetInt32() == 1,
            JsonValueKind.String => bool.TryParse(element.GetString(), out var flag)
                ? flag
                : element.GetString() == "1",
            _ => false
        };
    }

    private static bool? TryReadBooleanLike(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyValue)
            || propertyValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return ReadBooleanLike(propertyValue);
    }

    private static DateTime ParseDateTime(string? rawValue)
    {
        return ParseNullableDateTime(rawValue) ?? DateTime.UtcNow;
    }

    private static DateTime? ParseNullableDateTime(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss:fff",
            "yyyy-MM-dd HH:mm:ss.fff"
        };

        return DateTime.TryParseExact(
            rawValue,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed
            : DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed)
                ? parsed
                : null;
    }

    private static CtyunDeviceDetailDto EmptyDeviceDetail(string deviceCode)
    {
        return new CtyunDeviceDetailDto(
            deviceCode,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            false,
            false,
            false,
            null,
            null,
            null,
            null,
            null);
    }

    private IEnumerable<string> BuildDeviceDetailPaths()
    {
        var deduplicated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preferredPaths = new[]
        {
            _settings.OpenPlatform.DeviceApi.DeviceDetailPath,
            "/open/token/device/showDevice",
            "/open/token/device/getDeviceInfoByDeviceCode"
        };

        foreach (var path in preferredPaths)
        {
            var normalized = path?.Trim();
            if (string.IsNullOrWhiteSpace(normalized) || !deduplicated.Add(normalized))
            {
                continue;
            }

            yield return normalized;
        }
    }

    private ServiceResponse<CtyunDeviceDetailDto> GetDeviceDetailFromPath(string deviceCode, string path)
    {
        MapPointSourceDiagnostics.Write(
            "PointDetail",
            $"Calling device detail API: deviceCode = {deviceCode}, path = {path}");

        var response = SendProtectedRequest(
            path,
            [
                new("accessToken", GetTokenOrThrow()),
                new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
                new("deviceCode", deviceCode),
                .. BuildParentUserParameter()
            ]);

        if (!response.IsSuccess)
        {
            MapPointSourceDiagnostics.Write(
                "PointDetail",
                $"Device detail API failed: deviceCode = {deviceCode}, path = {path}, reason = {response.Message}");
            return ServiceResponse<CtyunDeviceDetailDto>.Failure(EmptyDeviceDetail(deviceCode), response.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Data);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data)
                || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                var topLevelProperties = DescribeTopLevelProperties(root);
                MapPointSourceDiagnostics.Write(
                    "PointDetail",
                    $"Device detail response missing data field: deviceCode = {deviceCode}, path = {path}, topLevelProperties = {topLevelProperties}");
                return ServiceResponse<CtyunDeviceDetailDto>.Failure(
                    EmptyDeviceDetail(deviceCode),
                    $"CTYun 设备详情响应缺少 data 字段：path = {path}，顶层字段 = {topLevelProperties}");
            }

            var detail = new CtyunDeviceDetailDto(
                ReadPreferredString(data, "deviceCode") ?? deviceCode,
                ReadPreferredString(data, "deviceName") ?? string.Empty,
                ReadPreferredString(data, "deviceModel", "deviceType") ?? string.Empty,
                ReadPreferredString(data, "longitude"),
                ReadPreferredString(data, "latitude"),
                ReadPreferredString(data, "location", "fullRegionName", "regionCode"),
                TryReadBooleanLike(data, "onlineStatus"),
                data.TryGetProperty("cloudStatus", out var cloudStatus) && ReadBooleanLike(cloudStatus),
                data.TryGetProperty("picCloudStatus", out var picCloudStatus) && ReadBooleanLike(picCloudStatus),
                data.TryGetProperty("bandStatus", out var bandStatus) && ReadBooleanLike(bandStatus),
                TryReadInt32(data, "deviceSource"),
                ReadPreferredString(data, "fwVersion", "firmwareVersion"),
                TryReadInt32(data, "sourceTypeFlag"),
                ParseNullableDateTime(ReadPreferredString(data, "reportTime")),
                ParseNullableDateTime(ReadPreferredString(data, "importTime")));

            var coordinate = PointCoordinateParser.ParseRegistered(detail.Longitude, detail.Latitude, CoordinateSystemKind.BD09);
            var lastSyncTime = detail.ReportTime ?? detail.ImportTime;
            var lastSyncSource = detail.ReportTime.HasValue
                ? "reportTime"
                : detail.ImportTime.HasValue
                    ? "importTime"
                    : "待接入";
            MapPointSourceDiagnostics.Write(
                "PointDetail",
                $"Device detail parsed: deviceCode = {deviceCode}, path = {path}, pointName = {(string.IsNullOrWhiteSpace(detail.DeviceName) ? "missing" : "present")}, registeredCoordinateStatus = {coordinate.Status}, registeredCoordinateSystem = {(coordinate.Coordinate?.CoordinateSystem.ToString() ?? "Unknown")}, online = {(detail.IsOnline.HasValue ? detail.IsOnline.Value.ToString() : "null")}, lastSyncSource = {lastSyncSource}, lastSyncTime = {(lastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "待接入")}");
            MapPointSourceDiagnostics.Write(
                "PointDetailRaw",
                $"deviceCode = {deviceCode}, path = {path}, rawLongitude = {NormalizeCoordinateLogValue(detail.Longitude)}, rawLatitude = {NormalizeCoordinateLogValue(detail.Latitude)}, parsedLongitude = {FormatCoordinateLogValue(coordinate.Coordinate?.Longitude)}, parsedLatitude = {FormatCoordinateLogValue(coordinate.Coordinate?.Latitude)}, coordinateStatusEnum = {coordinate.Status}, coordinateStatusText = {NormalizeCoordinateLogValue(coordinate.StatusText)}");

            return ServiceResponse<CtyunDeviceDetailDto>.Success(detail, $"已获取设备详情：{path}");
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "PointDetail",
                $"Device detail parse exception: deviceCode = {deviceCode}, path = {path}, type = {ex.GetType().Name}, message = {ex.Message}");
            return ServiceResponse<CtyunDeviceDetailDto>.Failure(
                EmptyDeviceDetail(deviceCode),
                $"CTYun 设备详情解析失败：{ex.Message}");
        }
    }

    private static CtyunDeviceDetailDto MergeDeviceDetail(CtyunDeviceDetailDto primary, CtyunDeviceDetailDto candidate)
    {
        return new CtyunDeviceDetailDto(
            string.IsNullOrWhiteSpace(primary.DeviceCode) ? candidate.DeviceCode : primary.DeviceCode,
            string.IsNullOrWhiteSpace(primary.DeviceName) ? candidate.DeviceName : primary.DeviceName,
            string.IsNullOrWhiteSpace(primary.DeviceType) ? candidate.DeviceType : primary.DeviceType,
            string.IsNullOrWhiteSpace(primary.Longitude) ? candidate.Longitude : primary.Longitude,
            string.IsNullOrWhiteSpace(primary.Latitude) ? candidate.Latitude : primary.Latitude,
            string.IsNullOrWhiteSpace(primary.Location) ? candidate.Location : primary.Location,
            primary.IsOnline ?? candidate.IsOnline,
            primary.CloudStatus || candidate.CloudStatus,
            primary.PicCloudStatus || candidate.PicCloudStatus,
            primary.BandStatus || candidate.BandStatus,
            primary.DeviceSource ?? candidate.DeviceSource,
            string.IsNullOrWhiteSpace(primary.FwVersion) ? candidate.FwVersion : primary.FwVersion,
            primary.SourceTypeFlag ?? candidate.SourceTypeFlag,
            primary.ReportTime ?? candidate.ReportTime,
            primary.ImportTime ?? candidate.ImportTime);
    }

    private static bool HasUsableRegisteredCoordinate(CtyunDeviceDetailDto detail)
    {
        return !string.IsNullOrWhiteSpace(detail.DeviceName)
            && PointCoordinateParser.ParseRegistered(detail.Longitude, detail.Latitude, CoordinateSystemKind.BD09).IsValid;
    }

    private static bool ShouldStopDeviceDetailFallback(string currentPath, CtyunDeviceDetailDto mergedDetail)
    {
        return string.Equals(currentPath, "/open/token/device/showDevice", StringComparison.OrdinalIgnoreCase)
            && !HasUsableRegisteredCoordinate(mergedDetail)
            && HasStableNonCoordinateDetail(mergedDetail);
    }

    private static bool HasStableNonCoordinateDetail(CtyunDeviceDetailDto detail)
    {
        return !string.IsNullOrWhiteSpace(detail.DeviceName)
            && (detail.IsOnline.HasValue
                || detail.ReportTime.HasValue
                || detail.ImportTime.HasValue
                || !string.IsNullOrWhiteSpace(detail.Location)
                || !string.IsNullOrWhiteSpace(detail.DeviceType));
    }

    private bool TryGetCachedDeviceDetail(string deviceCode, out ServiceResponse<CtyunDeviceDetailDto> response)
    {
        lock (_deviceDetailCacheSyncRoot)
        {
            if (_deviceDetailCache.TryGetValue(deviceCode, out var cached)
                && cached.IsReusable(DateTime.UtcNow, DeviceDetailMinimumRefreshInterval))
            {
                response = cached.Response;
                return true;
            }

            _deviceDetailCache.Remove(deviceCode);
        }

        response = ServiceResponse<CtyunDeviceDetailDto>.Failure(EmptyDeviceDetail(deviceCode), string.Empty);
        return false;
    }

    private void CacheDeviceDetail(
        string deviceCode,
        ServiceResponse<CtyunDeviceDetailDto> response,
        TimeSpan cacheLifetime)
    {
        lock (_deviceDetailCacheSyncRoot)
        {
            _deviceDetailCache[deviceCode] = new CachedServiceResponseEntry<CtyunDeviceDetailDto>(
                response,
                DateTime.UtcNow,
                DateTime.UtcNow.Add(cacheLifetime));
        }
    }

    private static string BuildDeviceAlertCacheKey(
        string deviceCode,
        string alertTypeList,
        int alertSource,
        int pageNo,
        int pageSize)
    {
        return $"{deviceCode}|{alertTypeList}|{alertSource}|{pageNo}|{pageSize}";
    }

    private bool TryGetCachedDeviceAlerts(
        string cacheKey,
        string deviceCode,
        out ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>> response)
    {
        lock (_deviceAlertCacheSyncRoot)
        {
            if (_deviceAlertCache.TryGetValue(cacheKey, out var cached))
            {
                var now = DateTime.UtcNow;
                var minimumRefreshUntil = cached.CachedAt.Add(DeviceAlertMinimumRefreshInterval);
                if (cached.IsReusable(now, DeviceAlertMinimumRefreshInterval))
                {
                    response = cached.Response;
                    MapPointSourceDiagnostics.Write(
                        "CTYunAlarm",
                        $"Device alarm cache hit: deviceCode = {deviceCode}, cachedAt = {cached.CachedAt:yyyy-MM-dd HH:mm:ss}, expiresAt = {cached.ExpiresAt:yyyy-MM-dd HH:mm:ss}, minimumRefreshUntil = {minimumRefreshUntil:yyyy-MM-dd HH:mm:ss}, alertCount = {response.Data.Count}, isSuccess = {response.IsSuccess}");
                    return true;
                }

                MapPointSourceDiagnostics.Write(
                    "CTYunAlarm",
                    $"Device alarm cache stale: deviceCode = {deviceCode}, cachedAt = {cached.CachedAt:yyyy-MM-dd HH:mm:ss}, expiresAt = {cached.ExpiresAt:yyyy-MM-dd HH:mm:ss}, minimumRefreshUntil = {minimumRefreshUntil:yyyy-MM-dd HH:mm:ss}, now = {now:yyyy-MM-dd HH:mm:ss}");
                _deviceAlertCache.Remove(cacheKey);
            }
        }

        response = ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Failure([], string.Empty);
        return false;
    }

    private void CacheDeviceAlerts(
        string cacheKey,
        ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>> response,
        TimeSpan cacheLifetime)
    {
        lock (_deviceAlertCacheSyncRoot)
        {
            _deviceAlertCache[cacheKey] = new CachedServiceResponseEntry<IReadOnlyList<CtyunDeviceAlertDto>>(
                response,
                DateTime.UtcNow,
                DateTime.UtcNow.Add(cacheLifetime));
        }
    }

    private static string? ReadPreferredString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var propertyValue)
                || propertyValue.ValueKind == JsonValueKind.Null
                || propertyValue.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            return propertyValue.ValueKind switch
            {
                JsonValueKind.String => propertyValue.GetString(),
                JsonValueKind.Number => propertyValue.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => propertyValue.ToString()
            };
        }

        return null;
    }

    private static int? TryReadInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null
            || value.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numericValue))
        {
            return numericValue;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static string DescribeTopLevelProperties(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            ? string.Join(", ", element.EnumerateObject().Select(property => property.Name))
            : element.ValueKind.ToString();
    }

    private static string NormalizeCoordinateLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
    }

    private static string FormatCoordinateLogValue(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.######", CultureInfo.InvariantCulture)
            : "null";
    }

    private sealed record CachedServiceResponseEntry<T>(
        ServiceResponse<T> Response,
        DateTime CachedAt,
        DateTime ExpiresAt)
    {
        public bool IsReusable(DateTime now, TimeSpan minimumRefreshInterval)
        {
            return ExpiresAt > now || CachedAt.Add(minimumRefreshInterval) > now;
        }
    }

    private sealed record PreviewPayloadResolution(
        bool IsSuccess,
        JsonElement Payload,
        PreviewPayloadDiagnostics Diagnostics);

    private sealed record PreviewPayloadDiagnostics(
        string ResponseBodyPreviewFirst300,
        string ResponseEnvelopeShape,
        string ResponseJsonTopLevelKeys,
        string ResponseCandidateUrlKeys,
        string ResponseNestedPathTried,
        string OriginalDataRaw,
        string MatchedFieldPath,
        string ParsedProtocolType,
        string DecryptMode)
    {
        public static PreviewPayloadDiagnostics Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private sealed record PreviewJsonCandidate(string Path, JsonElement Element, string DecryptMode, string OriginalDataRaw);

    private readonly record struct ResolvedPreviewTextPayload(
        string JsonPayload,
        string ResolveMode,
        string DecryptMode);

    private sealed record PreviewUrlCandidate(
        string PropertyName,
        string Path,
        string Value,
        int Depth,
        string ProtocolType,
        string DecryptMode);
}

public sealed class CtyunDeviceCatalogService : IDeviceCatalogService
{
    private static readonly TimeSpan CatalogCacheLifetime = TimeSpan.FromMinutes(2);
    private readonly CtyunOpenPlatformClient _client;
    private readonly ICtyunDeviceListAdapter _adapter;
    private readonly PlatformIntegrationSettings _settings;
    private readonly object _catalogCacheSyncRoot = new();
    private CachedCatalogEntry? _catalogCache;

    public CtyunDeviceCatalogService(
        CtyunOpenPlatformClient client,
        ICtyunDeviceListAdapter adapter,
        PlatformIntegrationSettings settings)
    {
        _client = client;
        _adapter = adapter;
        _settings = settings;
    }

    public ServiceResponse<IReadOnlyList<DeviceListItemDto>> GetDevices()
    {
        if (TryGetCachedCatalog(out var cachedDevices, out var cachedMessage))
        {
            MapPointSourceDiagnostics.Write(
                "DeviceCatalog",
                $"Using cached device catalog: cachedDeviceCount = {cachedDevices.Count}");
            return ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Success(cachedDevices, cachedMessage);
        }

        var allCatalogItems = new List<CtyunDeviceCatalogItemDto>();
        var nextLastId = _settings.OpenPlatform.DeviceApi.InitialLastId;
        string? pageMessage = null;

        while (true)
        {
            var pageResponse = _client.GetDeviceCatalogPage(nextLastId);
            if (!pageResponse.IsSuccess)
            {
                if (allCatalogItems.Count == 0)
                {
                    MapPointSourceDiagnostics.Write("DeviceCatalog", $"Device catalog request failed before any device was loaded: {pageResponse.Message}");
                    return ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Failure([], $"设备目录获取失败：{pageResponse.Message}");
                }

                pageMessage = pageResponse.Message;
                MapPointSourceDiagnostics.Write(
                    "DeviceCatalog",
                    $"Device catalog paging stopped early: alreadyLoaded = {allCatalogItems.Count}, reason = {pageMessage}");
                break;
            }

            allCatalogItems.AddRange(pageResponse.Data.Items);

            if (pageResponse.Data.LastId < 0 || pageResponse.Data.Items.Count == 0)
            {
                break;
            }

            nextLastId = pageResponse.Data.LastId;
        }

        var catalogItems = allCatalogItems
            .GroupBy(item => item.DeviceCode, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (catalogItems.Count == 0)
        {
            MapPointSourceDiagnostics.Write("DeviceCatalog", "Device catalog returned 0 devices after deduplication.");
            return ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Failure([], "设备目录返回 0 条。");
        }

        var devices = new List<DeviceListItemDto>(catalogItems.Count);
        var detailFailureReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var detailContexts = catalogItems
            .Select(item =>
            {
                var detailResponse = _client.GetDeviceDetail(item.DeviceCode);
                if (!detailResponse.IsSuccess
                    || !HasUsableDetailPayload(detailResponse.Data))
                {
                    Increment(detailFailureReasons, ClassifyDetailFailureReason(detailResponse));
                }

                return new CatalogItemDetailContext(
                    item,
                    detailResponse,
                    detailResponse.IsSuccess
                        ? PointCoordinateParser.ParseRegistered(
                            detailResponse.Data.Longitude,
                            detailResponse.Data.Latitude,
                            CoordinateSystemKind.BD09)
                        : PointCoordinateParser.ParseRegistered(null, null, CoordinateSystemKind.BD09));
            })
            .ToList();
        foreach (var context in detailContexts)
        {
            var detail = context.DetailResponse.IsSuccess ? context.DetailResponse.Data : null;
            var coordinate = ResolveCatalogCoordinate(context);
            devices.Add(_adapter.MapDevice(context.Item, detail, coordinate));
        }

        if (devices.Count == 0)
        {
            MapPointSourceDiagnostics.Write("DeviceCatalog", "Device detail enrichment produced 0 devices.");
            return ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Failure([], "CTYun 设备目录未返回可用设备详情。");
        }

        var renderableCount = devices.Count(device => device.Coordinate.CanRenderOnMap);
        var unmappedCount = devices.Count - renderableCount;
        var coordinateFailureReasons = devices
            .Where(device => !device.Coordinate.CanRenderOnMap)
            .GroupBy(device => ClassifyCoordinateFailureReason(device.Coordinate), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        MapPointSourceDiagnostics.WriteLines("DeviceCatalog", [
            $"deviceCatalogApi = {_settings.OpenPlatform.DeviceApi.DeviceListPath}",
            $"deviceCatalogTotal = {devices.Count}",
            $"deviceCatalogIsEmpty = {devices.Count == 0}",
            $"detailSuccessCount = {catalogItems.Count - detailFailureReasons.Values.Sum()}",
            $"detailFailureCount = {detailFailureReasons.Values.Sum()}",
            $"detailFailureReasons = {MapPointSourceDiagnostics.SummarizeCounts(detailFailureReasons)}",
            $"renderablePointCount = {renderableCount}",
            $"unrenderablePointCount = {unmappedCount}",
            $"unrenderableReasons = {MapPointSourceDiagnostics.SummarizeCounts(coordinateFailureReasons)}"
        ]);

        var message = BuildCatalogMessage(pageMessage, devices.Count, renderableCount, detailFailureReasons.Values.Sum());
        CacheCatalog(devices, message);
        return ServiceResponse<IReadOnlyList<DeviceListItemDto>>.Success(devices, message);
    }

    private static PointCoordinateModel ResolveCatalogCoordinate(
        CatalogItemDetailContext context)
    {
        if (!context.DetailResponse.IsSuccess)
        {
            return PointCoordinateParser.Missing();
        }

        return PointCoordinateParser.FromParsedRegistered(context.RegisteredCoordinate);
    }

    private bool TryGetCachedCatalog(out IReadOnlyList<DeviceListItemDto> devices, out string message)
    {
        lock (_catalogCacheSyncRoot)
        {
            if (_catalogCache is not null && _catalogCache.ExpiresAt > DateTime.UtcNow)
            {
                devices = _catalogCache.Devices;
                message = _catalogCache.Message;
                return true;
            }

            _catalogCache = null;
        }

        devices = [];
        message = string.Empty;
        return false;
    }

    private void CacheCatalog(IReadOnlyList<DeviceListItemDto> devices, string message)
    {
        lock (_catalogCacheSyncRoot)
        {
            _catalogCache = new CachedCatalogEntry(devices, message, DateTime.UtcNow.Add(CatalogCacheLifetime));
        }
    }

    private static string BuildCatalogMessage(string? pageMessage, int totalCount, int renderableCount, int detailFailureCount)
    {
        var messages = new List<string>();

        if (!string.IsNullOrWhiteSpace(pageMessage))
        {
            messages.Add(pageMessage.Trim());
        }

        messages.Add($"已同步 {totalCount} 个目录点位。");
        messages.Add(renderableCount > 0
            ? $"其中 {renderableCount} 个点位具备地图坐标。"
            : "当前目录点位暂未返回可落图坐标。");

        if (detailFailureCount > 0)
        {
            messages.Add($"{detailFailureCount} 个点位详情未补齐，已先保留目录入口。");
        }

        return string.Join(" ", messages);
    }

    private static bool HasUsableDetailPayload(CtyunDeviceDetailDto detail)
    {
        return !string.IsNullOrWhiteSpace(detail.DeviceName)
            || !string.IsNullOrWhiteSpace(detail.Longitude)
            || !string.IsNullOrWhiteSpace(detail.Latitude);
    }

    private static string ClassifyDetailFailureReason(ServiceResponse<CtyunDeviceDetailDto> response)
    {
        var message = response.Message?.Trim() ?? string.Empty;
        if (message.Contains("接口异常", StringComparison.Ordinal)
            || message.Contains("调用异常", StringComparison.Ordinal)
            || message.Contains("请求异常", StringComparison.Ordinal))
        {
            return "接口异常";
        }

        if (message.Contains("解析失败", StringComparison.Ordinal))
        {
            return "解析失败";
        }

        if (message.Contains("未返回", StringComparison.Ordinal)
            || message.Contains("缺少", StringComparison.Ordinal)
            || message.Contains("为空", StringComparison.Ordinal))
        {
            return "无详情";
        }

        return HasUsableDetailPayload(response.Data) ? "字段缺失" : "无详情";
    }

    private static string ClassifyCoordinateFailureReason(PointCoordinateModel coordinate)
    {
        return coordinate.Status switch
        {
            PointCoordinateStatus.Missing => "空值",
            PointCoordinateStatus.Incomplete => "空值",
            PointCoordinateStatus.ZeroOrigin => "0/0",
            PointCoordinateStatus.ConversionFailed => "转换失败",
            PointCoordinateStatus.Invalid when coordinate.StatusText.Contains("范围", StringComparison.Ordinal) => "越界",
            PointCoordinateStatus.Invalid => "格式异常",
            _ => coordinate.StatusText
        };
    }

    private static void Increment(IDictionary<string, int> counts, string key)
    {
        if (counts.TryGetValue(key, out var value))
        {
            counts[key] = value + 1;
            return;
        }

        counts[key] = 1;
    }

    private sealed record CachedCatalogEntry(IReadOnlyList<DeviceListItemDto> Devices, string Message, DateTime ExpiresAt);

    private sealed record CatalogItemDetailContext(
        CtyunDeviceCatalogItemDto Item,
        ServiceResponse<CtyunDeviceDetailDto> DetailResponse,
        ParsedRegisteredCoordinateModel RegisteredCoordinate);
}

public sealed class CtyunAlertQueryService : IAlertQueryService
{
    private readonly CtyunOpenPlatformClient _client;
    private readonly ICtyunAiAlertAdapter _aiAlertAdapter;
    private readonly ICtyunDeviceAlertAdapter _deviceAlertAdapter;

    public CtyunAlertQueryService(
        CtyunOpenPlatformClient client,
        ICtyunAiAlertAdapter aiAlertAdapter,
        ICtyunDeviceAlertAdapter deviceAlertAdapter)
    {
        _client = client;
        _aiAlertAdapter = aiAlertAdapter;
        _deviceAlertAdapter = deviceAlertAdapter;
    }

    public ServiceResponse<IReadOnlyList<FaultAlertDto>> GetAiAlerts(AiAlertQueryDto query)
    {
        var response = _client.GetAiAlerts(query);
        return !response.IsSuccess
            ? ServiceResponse<IReadOnlyList<FaultAlertDto>>.Failure([], response.Message)
            : ServiceResponse<IReadOnlyList<FaultAlertDto>>.Success(response.Data.Select(_aiAlertAdapter.MapAlert).ToList());
    }

    public ServiceResponse<IReadOnlyList<FaultAlertDto>> GetDeviceAlerts(DeviceAlertQueryDto query)
    {
        var response = _client.GetDeviceAlerts(query);
        return !response.IsSuccess
            ? ServiceResponse<IReadOnlyList<FaultAlertDto>>.Failure([], response.Message)
            : ServiceResponse<IReadOnlyList<FaultAlertDto>>.Success(response.Data.Select(_deviceAlertAdapter.MapAlert).ToList());
    }
}

public sealed class CtyunDevicePointDetailService : IDevicePointDetailService
{
    private readonly CtyunOpenPlatformClient _client;

    public CtyunDevicePointDetailService(
        CtyunOpenPlatformClient client,
        IDeviceCatalogService deviceCatalogService)
    {
        _client = client;
    }

    public ServiceResponse<DevicePointDetailModel> GetPointDetail(string pointId)
    {
        var detailResponse = _client.GetDeviceDetail(pointId);
        if (!detailResponse.IsSuccess)
        {
            return ServiceResponse<DevicePointDetailModel>.Failure(Empty(pointId), $"点位详情获取失败：{detailResponse.Message}");
        }

        var detail = detailResponse.Data;
        var pointName = !string.IsNullOrWhiteSpace(detail.DeviceName)
            ? detail.DeviceName
            : pointId;
        var unitName = !string.IsNullOrWhiteSpace(detail.Location)
            ? detail.Location
            : "待补齐所属单位";
        var onlineText = detail.IsOnline switch
        {
            true => "在线",
            false => "离线",
            _ => "未知"
        };
        var parsedCoordinate = PointCoordinateParser.ParseRegistered(detail.Longitude, detail.Latitude, CoordinateSystemKind.BD09);
        var coordinate = ResolveDetailCoordinate(parsedCoordinate);
        var lastSyncTime = detail.ReportTime ?? detail.ImportTime;
        var lastSyncSource = detail.ReportTime.HasValue
            ? "reportTime"
            : detail.ImportTime.HasValue
                ? "importTime"
                : "待接入";
        var detailSummary = BuildDetailSummary(detail.IsOnline, coordinate);

        MapPointSourceDiagnostics.Write(
            "PointStatus",
            $"ctyun status mapped: pointId = {pointId}, online = {onlineText}, coordinate = {coordinate.StatusText}, registeredSystem = {coordinate.RegisteredCoordinateSystem}, mapSystem = {coordinate.MapCoordinateSystem}, converted = {coordinate.IsConverted}, mapSource = {coordinate.MapSource}, lastSync = {(lastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "待接入")}, lastSyncSource = {lastSyncSource}");
        MapPointSourceDiagnostics.Write(
            "PointDetailRaw",
            $"pointId = {pointId}, rawLongitude = {NormalizeCoordinateLogValue(detail.Longitude)}, rawLatitude = {NormalizeCoordinateLogValue(detail.Latitude)}, parsedLongitude = {FormatCoordinateLogValue(parsedCoordinate.Coordinate?.Longitude)}, parsedLatitude = {FormatCoordinateLogValue(parsedCoordinate.Coordinate?.Latitude)}, mapLongitude = {FormatCoordinateLogValue(coordinate.MapCoordinate?.Longitude)}, mapLatitude = {FormatCoordinateLogValue(coordinate.MapCoordinate?.Latitude)}, coordinateStatusEnum = {coordinate.Status}, coordinateStatusText = {NormalizeCoordinateLogValue(coordinate.StatusText)}, canRenderOnMap = {coordinate.CanRenderOnMap}");

        return ServiceResponse<DevicePointDetailModel>.Success(new DevicePointDetailModel(
            PointIdentity.CreatePointId(pointId),
            pointId,
            pointName,
            string.IsNullOrWhiteSpace(detail.DeviceType) ? "CTYun设备" : detail.DeviceType,
            unitName,
            unitName,
            unitName,
            coordinate,
            detail.IsOnline,
            onlineText,
            "待接入",
            "待接入",
            lastSyncTime,
            lastSyncSource,
            detailSummary,
            "CTYun"));
    }

    private static PointCoordinateModel ResolveDetailCoordinate(
        ParsedRegisteredCoordinateModel parsedCoordinate)
    {
        return PointCoordinateParser.FromParsedRegistered(parsedCoordinate);
    }

    private static string NormalizeCoordinateLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
    }

    private static string FormatCoordinateLogValue(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
            : "null";
    }

    private static DevicePointDetailModel Empty(string pointId)
    {
        return new DevicePointDetailModel(
            pointId,
            pointId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            PointCoordinateParser.Missing(),
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            "CTYun");
    }

    private static string BuildDetailSummary(bool? isOnline, PointCoordinateModel coordinate)
    {
        var onlineStatus = isOnline switch
        {
            true => "在线",
            false => "离线",
            _ => "未知"
        };

        var coordinateStatus = PointBusinessSummaryFactory.ResolveCoordinateStatus(coordinate);

        return $"{onlineStatus} / {coordinateStatus} / 待接入";
    }
}
