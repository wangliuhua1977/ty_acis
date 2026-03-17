using System.Globalization;
using System.Net.Http;
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
    private readonly HttpClient _httpClient;
    private readonly PlatformIntegrationSettings _settings;
    private readonly ICtyunAccessTokenService _accessTokenService;
    private readonly object _deviceDetailCacheSyncRoot = new();
    private readonly Dictionary<string, CachedDeviceDetailEntry> _deviceDetailCache = new(StringComparer.OrdinalIgnoreCase);

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

        if (TryGetCachedDeviceDetail(normalizedDeviceCode, out var cachedDetail))
        {
            MapPointSourceDiagnostics.Write(
                "PointDetail",
                $"Using cached device detail: deviceCode = {normalizedDeviceCode}");
            return ServiceResponse<CtyunDeviceDetailDto>.Success(cachedDetail, "使用内存缓存设备详情。");
        }

        ServiceResponse<CtyunDeviceDetailDto>? firstFailure = null;
        CtyunDeviceDetailDto? mergedDetail = null;

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

            if (HasUsableRegisteredCoordinate(mergedDetail))
            {
                CacheDeviceDetail(normalizedDeviceCode, mergedDetail);
                MapPointSourceDiagnostics.Write(
                    "PointDetail",
                    $"Device detail resolved with usable registered coordinate: deviceCode = {normalizedDeviceCode}, path = {path}");
                return ServiceResponse<CtyunDeviceDetailDto>.Success(mergedDetail, response.Message);
            }
        }

        if (mergedDetail is not null)
        {
            CacheDeviceDetail(normalizedDeviceCode, mergedDetail);
            var parsedCoordinate = PointCoordinateParser.ParseRegistered(mergedDetail.Longitude, mergedDetail.Latitude, CoordinateSystemKind.BD09);
            MapPointSourceDiagnostics.Write(
                "PointDetail",
                $"Device detail resolved without usable registered coordinate: deviceCode = {normalizedDeviceCode}, coordinateStatus = {parsedCoordinate.Status}");
            return ServiceResponse<CtyunDeviceDetailDto>.Success(mergedDetail, "已合并设备详情接口返回。");
        }

        if (firstFailure is not null)
        {
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
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("accessToken", GetTokenOrThrow()),
            new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
            new("deviceCode", query.DeviceCode),
            new("pageNo", query.PageNo.ToString(CultureInfo.InvariantCulture)),
            new("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture))
        };

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

        var response = SendProtectedRequest(_settings.OpenPlatform.AlarmApi.DeviceAlertListPath, parameters);
        if (!response.IsSuccess)
        {
            return ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Failure([], response.Message);
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
                    query.AlertSource ?? _settings.OpenPlatform.AlarmApi.DeviceAlertSource))
                .ToList();

            return ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Success(alerts);
        }
        catch (Exception ex)
        {
            return ServiceResponse<IReadOnlyList<CtyunDeviceAlertDto>>.Failure([], $"CTYun 设备告警解析失败：{ex.Message}");
        }
    }

    public ServiceResponse<CtyunPreviewStreamSetDto> GetH5PreviewStreamSet(string deviceCode)
    {
        var normalizedDeviceCode = deviceCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDeviceCode))
        {
            return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                new CtyunPreviewStreamSetDto(null, null, []),
                "设备编码为空，无法获取 H5 预览地址。");
        }

        var response = SendProtectedRequest(
            "/open/token/vpaas/getH5StreamUrl",
            [
                new("accessToken", GetTokenOrThrow()),
                new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
                new("deviceCode", normalizedDeviceCode),
                new("mediaType", "0"),
                new("mute", "0"),
                new("playerType", "1"),
                new("wasm", "1"),
                new("allLiveUrl", "1"),
                .. BuildParentUserParameter()
            ]);
        if (!response.IsSuccess)
        {
            return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                new CtyunPreviewStreamSetDto(null, null, []),
                response.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Data);
            if (!TryReadPayloadData(document.RootElement, out var data))
            {
                return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                    new CtyunPreviewStreamSetDto(null, null, []),
                    "H5 预览接口未返回可解析的数据。");
            }

            var streamUrls = new List<CtyunPreviewStreamUrlDto>();
            if (data.TryGetProperty("streamUrls", out var streamUrlsElement)
                && streamUrlsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in streamUrlsElement.EnumerateArray())
                {
                    var streamUrl = ReadOptionalTextLocal(item, "streamUrl") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(streamUrl))
                    {
                        continue;
                    }

                    streamUrls.Add(new CtyunPreviewStreamUrlDto(
                        item.TryGetProperty("protocol", out var protocolElement) && protocolElement.ValueKind == JsonValueKind.Number
                            ? protocolElement.GetInt32()
                            : 0,
                        streamUrl,
                        ReadOptionalTextLocal(item, "ipv6StreamUrl"),
                        item.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.Number
                            ? levelElement.GetInt32()
                            : null));
                }
            }

            return ServiceResponse<CtyunPreviewStreamSetDto>.Success(
                new CtyunPreviewStreamSetDto(
                    data.TryGetProperty("expireIn", out var expireInElement) && expireInElement.ValueKind == JsonValueKind.Number
                        ? expireInElement.GetInt32()
                        : null,
                    data.TryGetProperty("videoEnc", out var videoEncElement) && videoEncElement.ValueKind == JsonValueKind.Number
                        ? videoEncElement.GetInt32()
                        : null,
                    streamUrls));
        }
        catch (Exception ex)
        {
            return ServiceResponse<CtyunPreviewStreamSetDto>.Failure(
                new CtyunPreviewStreamSetDto(null, null, []),
                $"H5 预览地址解析失败：{ex.Message}");
        }
    }

    public ServiceResponse<CtyunPreviewMediaUrlDto> GetPreviewMediaUrl(string deviceCode, string path)
    {
        var normalizedDeviceCode = deviceCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDeviceCode))
        {
            return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                new CtyunPreviewMediaUrlDto(string.Empty, null),
                "设备编码为空，无法获取预览地址。");
        }

        var response = SendProtectedRequest(
            path,
            [
                new("accessToken", GetTokenOrThrow()),
                new("enterpriseUser", _settings.OpenPlatform.EnterpriseUser),
                new("deviceCode", normalizedDeviceCode),
                new("mediaType", "0"),
                new("supportDomain", "1"),
                new("mute", "0"),
                new("netType", "0"),
                new("expire", "300"),
                .. BuildParentUserParameter()
            ]);
        if (!response.IsSuccess)
        {
            return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                new CtyunPreviewMediaUrlDto(string.Empty, null),
                response.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Data);
            if (!TryReadPayloadData(document.RootElement, out var data))
            {
                return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                    new CtyunPreviewMediaUrlDto(string.Empty, null),
                    "预览地址接口未返回可解析的数据。");
            }

            return ServiceResponse<CtyunPreviewMediaUrlDto>.Success(
                new CtyunPreviewMediaUrlDto(
                    ReadOptionalTextLocal(data, "url") ?? string.Empty,
                    data.TryGetProperty("expireTime", out var expireTimeElement) && expireTimeElement.ValueKind == JsonValueKind.Number
                        ? expireTimeElement.GetInt32()
                        : null));
        }
        catch (Exception ex)
        {
            return ServiceResponse<CtyunPreviewMediaUrlDto>.Failure(
                new CtyunPreviewMediaUrlDto(string.Empty, null),
                $"预览地址解析失败：{ex.Message}");
        }
    }

    private ServiceResponse<string> SendProtectedRequest(string path, IReadOnlyList<KeyValuePair<string, string>> businessParameters)
    {
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
                return ServiceResponse<string>.Failure(
                    string.Empty,
                    $"CTYun 接口响应缺少 code 字段：path = {path}，顶层字段 = {topLevelProperties}");
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
                return ServiceResponse<string>.Failure(
                    string.Empty,
                    $"CTYun 接口调用失败：path = {path}，message = {message}");
            }

            return ServiceResponse<string>.Success(payload);
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "CTYunHttp",
                $"Protected API exception: path = {path}, type = {ex.GetType().Name}, message = {ex.Message}");
            return ServiceResponse<string>.Failure(string.Empty, $"CTYun 接口请求异常：path = {path}，{ex.Message}");
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

    private static bool TryReadPayloadData(JsonElement root, out JsonElement data)
    {
        data = default;
        if (!root.TryGetProperty("data", out var rawData)
            || rawData.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (rawData.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            data = rawData;
            return true;
        }

        if (rawData.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = rawData.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal)
            && !trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        using var nested = JsonDocument.Parse(trimmed);
        data = nested.RootElement.Clone();
        return true;
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

    private bool TryGetCachedDeviceDetail(string deviceCode, out CtyunDeviceDetailDto detail)
    {
        lock (_deviceDetailCacheSyncRoot)
        {
            if (_deviceDetailCache.TryGetValue(deviceCode, out var cached)
                && cached.ExpiresAt > DateTime.UtcNow)
            {
                detail = cached.Detail;
                return true;
            }

            _deviceDetailCache.Remove(deviceCode);
        }

        detail = EmptyDeviceDetail(deviceCode);
        return false;
    }

    private void CacheDeviceDetail(string deviceCode, CtyunDeviceDetailDto detail)
    {
        lock (_deviceDetailCacheSyncRoot)
        {
            _deviceDetailCache[deviceCode] = new CachedDeviceDetailEntry(
                detail,
                DateTime.UtcNow.Add(DeviceDetailCacheLifetime));
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

    private sealed record CachedDeviceDetailEntry(CtyunDeviceDetailDto Detail, DateTime ExpiresAt);
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
