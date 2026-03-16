using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Diagnostics;

namespace TianyiVision.Acis.Services.Maps;

public sealed record AmapCoordinateConvertRequest(
    string RequestId,
    string DeviceCode,
    CoordinateValueModel Coordinate);

public sealed record AmapCoordinateConvertItemResult(
    string RequestId,
    string DeviceCode,
    CoordinateValueModel SourceCoordinate,
    CoordinateValueModel? TargetCoordinate,
    bool IsSuccess,
    bool IsFromCache,
    string FailureReason);

public interface IAmapCoordinateConverter
{
    AmapCoordinateConvertItemResult ConvertFromBaidu(
        AmapCoordinateConvertRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyList<AmapCoordinateConvertItemResult> ConvertFromBaidu(
        IReadOnlyList<AmapCoordinateConvertRequest> requests,
        CancellationToken cancellationToken = default);
}

public sealed class AmapCoordinateConverter : IAmapCoordinateConverter
{
    private const string ConvertEndpoint = "https://restapi.amap.com/v3/assistant/coordinate/convert";
    private const int MaxBatchSize = 40;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);

    private readonly HttpClient _httpClient;
    private readonly MapProviderSettings _settings;
    private readonly object _cacheSyncRoot = new();
    private readonly Dictionary<string, CachedCoordinateEntry> _cache = new(StringComparer.Ordinal);

    public AmapCoordinateConverter(HttpClient httpClient, MapProviderSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public AmapCoordinateConvertItemResult ConvertFromBaidu(
        AmapCoordinateConvertRequest request,
        CancellationToken cancellationToken = default)
    {
        return ConvertFromBaidu([request], cancellationToken)[0];
    }

    public IReadOnlyList<AmapCoordinateConvertItemResult> ConvertFromBaidu(
        IReadOnlyList<AmapCoordinateConvertRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        var results = new AmapCoordinateConvertItemResult[requests.Count];
        var pending = new Dictionary<string, PendingBatchEntry>(StringComparer.Ordinal);

        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var immediateResult = TryBuildImmediateResult(request);
            if (immediateResult is not null)
            {
                results[index] = immediateResult;
                LogItem(immediateResult, null, null);
                continue;
            }

            var cacheKey = BuildCacheKey(request.Coordinate);
            if (TryGetCached(cacheKey, out var cachedCoordinate))
            {
                var cachedResult = new AmapCoordinateConvertItemResult(
                    request.RequestId,
                    request.DeviceCode,
                    request.Coordinate,
                    cachedCoordinate,
                    true,
                    true,
                    string.Empty);
                results[index] = cachedResult;
                LogItem(cachedResult, null, null);
                continue;
            }

            if (!pending.TryGetValue(cacheKey, out var entry))
            {
                entry = new PendingBatchEntry(cacheKey, request);
                pending.Add(cacheKey, entry);
            }

            entry.Indexes.Add(index);
        }

        if (pending.Count == 0)
        {
            return results;
        }

        var batchEntries = pending.Values.ToList();
        var totalBatchCount = (int)Math.Ceiling(batchEntries.Count / (double)MaxBatchSize);

        for (var batchIndex = 0; batchIndex < totalBatchCount; batchIndex++)
        {
            var currentBatch = batchEntries
                .Skip(batchIndex * MaxBatchSize)
                .Take(MaxBatchSize)
                .ToList();
            ExecuteBatch(currentBatch, batchIndex + 1, totalBatchCount, results, cancellationToken);
        }

        return results;
    }

    private AmapCoordinateConvertItemResult? TryBuildImmediateResult(AmapCoordinateConvertRequest request)
    {
        if (!IsCoordinateInRange(request.Coordinate.Longitude, request.Coordinate.Latitude))
        {
            return new AmapCoordinateConvertItemResult(
                request.RequestId,
                request.DeviceCode,
                request.Coordinate,
                null,
                false,
                false,
                "原始坐标不合法，已跳过高德转换。");
        }

        if (request.Coordinate.CoordinateSystem == CoordinateSystemKind.GCJ02)
        {
            return new AmapCoordinateConvertItemResult(
                request.RequestId,
                request.DeviceCode,
                request.Coordinate,
                request.Coordinate,
                true,
                false,
                string.Empty);
        }

        if (request.Coordinate.CoordinateSystem != CoordinateSystemKind.BD09)
        {
            return new AmapCoordinateConvertItemResult(
                request.RequestId,
                request.DeviceCode,
                request.Coordinate,
                null,
                false,
                false,
                $"暂不支持 {request.Coordinate.CoordinateSystem} 转为 GCJ-02。");
        }

        if (!_settings.EnableCoordinateConversion)
        {
            return new AmapCoordinateConvertItemResult(
                request.RequestId,
                request.DeviceCode,
                request.Coordinate,
                null,
                false,
                false,
                "坐标转换功能已关闭。");
        }

        if (IsPlaceholderValue(_settings.AmapWebServiceApiKey))
        {
            return new AmapCoordinateConvertItemResult(
                request.RequestId,
                request.DeviceCode,
                request.Coordinate,
                null,
                false,
                false,
                "缺少可用的高德 Web 服务 Key。");
        }

        return null;
    }

    private void ExecuteBatch(
        IReadOnlyList<PendingBatchEntry> batch,
        int batchNumber,
        int totalBatchCount,
        AmapCoordinateConvertItemResult[] results,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var convertedCoordinates = ExecuteBatchRequest(batch, cancellationToken);
            stopwatch.Stop();

            foreach (var entry in batch)
            {
                if (!convertedCoordinates.TryGetValue(entry.CacheKey, out var convertedCoordinate))
                {
                    var failure = CreateBatchFailure(entry.Request, "高德返回坐标数量与请求不一致。");
                    ApplyResult(entry, failure, results, batchNumber, totalBatchCount, stopwatch.Elapsed);
                    continue;
                }

                Cache(entry.CacheKey, convertedCoordinate);
                var success = new AmapCoordinateConvertItemResult(
                    entry.Request.RequestId,
                    entry.Request.DeviceCode,
                    entry.Request.Coordinate,
                    convertedCoordinate,
                    true,
                    false,
                    string.Empty);
                ApplyResult(entry, success, results, batchNumber, totalBatchCount, stopwatch.Elapsed);
            }

            MapPointSourceDiagnostics.Write(
                "CoordinateConvert",
                $"amap coordinate convert batch succeeded: batch={batchNumber}/{totalBatchCount}, pointCount={batch.Count}, elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            foreach (var entry in batch)
            {
                var failure = CreateBatchFailure(entry.Request, ex.Message);
                ApplyResult(entry, failure, results, batchNumber, totalBatchCount, stopwatch.Elapsed);
            }

            MapPointSourceDiagnostics.Write(
                "CoordinateConvert",
                $"amap coordinate convert batch failed: batch={batchNumber}/{totalBatchCount}, pointCount={batch.Count}, elapsedMs={stopwatch.ElapsedMilliseconds}, reason={ex.Message}");
        }
    }

    private Dictionary<string, CoordinateValueModel> ExecuteBatchRequest(
        IReadOnlyList<PendingBatchEntry> batch,
        CancellationToken cancellationToken)
    {
        var locations = string.Join(
            "|",
            batch.Select(entry => FormatLocation(entry.Request.Coordinate)));
        var requestUrl =
            $"{ConvertEndpoint}?locations={Uri.EscapeDataString(locations)}&coordsys=baidu&output=json&key={Uri.EscapeDataString(_settings.AmapWebServiceApiKey)}";

        using var response = _httpClient.GetAsync(requestUrl, cancellationToken).GetAwaiter().GetResult();
        var payload = response.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var status = ReadText(root, "status");
        if (!string.Equals(status, "1", StringComparison.Ordinal))
        {
            var info = ReadText(root, "info") ?? "高德坐标转换返回失败状态。";
            var infoCode = ReadText(root, "infocode");
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(infoCode) ? info : $"{info} (infocode={infoCode})");
        }

        var locationsText = ReadText(root, "locations");
        if (string.IsNullOrWhiteSpace(locationsText))
        {
            throw new InvalidOperationException("高德坐标转换未返回 locations。");
        }

        var locationPairs = locationsText.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (locationPairs.Length != batch.Count)
        {
            throw new InvalidOperationException(
                $"高德坐标转换返回数量异常，请求 {batch.Count} 个，返回 {locationPairs.Length} 个。");
        }

        var converted = new Dictionary<string, CoordinateValueModel>(StringComparer.Ordinal);
        for (var index = 0; index < batch.Count; index++)
        {
            converted[batch[index].CacheKey] = ParseConvertedCoordinate(locationPairs[index]);
        }

        return converted;
    }

    private void ApplyResult(
        PendingBatchEntry entry,
        AmapCoordinateConvertItemResult result,
        AmapCoordinateConvertItemResult[] results,
        int batchNumber,
        int totalBatchCount,
        TimeSpan elapsed)
    {
        foreach (var index in entry.Indexes)
        {
            var mappedResult = result with
            {
                RequestId = results[index].RequestId is { Length: > 0 } ? results[index].RequestId : entry.Request.RequestId,
                DeviceCode = entry.Request.DeviceCode
            };
            results[index] = mappedResult;
            LogItem(mappedResult, batchNumber, totalBatchCount, elapsed);
        }
    }

    private void LogItem(
        AmapCoordinateConvertItemResult result,
        int? batchNumber,
        int? totalBatchCount,
        TimeSpan? elapsed = null)
    {
        var batchText = batchNumber.HasValue && totalBatchCount.HasValue
            ? $"{batchNumber}/{totalBatchCount}"
            : "cache/direct";
        var elapsedText = elapsed.HasValue ? elapsed.Value.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) : "0";
        var targetText = result.TargetCoordinate is null
            ? "none"
            : $"{result.TargetCoordinate.Longitude.ToString("0.###############", CultureInfo.InvariantCulture)},{result.TargetCoordinate.Latitude.ToString("0.###############", CultureInfo.InvariantCulture)}";

        MapPointSourceDiagnostics.Write(
            "CoordinateConvert",
            $"amap coordinate convert item: requestId={result.RequestId}, deviceCode={result.DeviceCode}, batch={batchText}, source={result.SourceCoordinate.CoordinateSystem}, raw={FormatLocation(result.SourceCoordinate)}, target={targetText}, success={result.IsSuccess}, fromCache={result.IsFromCache}, elapsedMs={elapsedText}, reason={(string.IsNullOrWhiteSpace(result.FailureReason) ? "none" : result.FailureReason)}");
    }

    private static AmapCoordinateConvertItemResult CreateBatchFailure(
        AmapCoordinateConvertRequest request,
        string reason)
    {
        return new AmapCoordinateConvertItemResult(
            request.RequestId,
            request.DeviceCode,
            request.Coordinate,
            null,
            false,
            false,
            string.IsNullOrWhiteSpace(reason) ? "高德坐标转换失败。" : reason.Trim());
    }

    private bool TryGetCached(string cacheKey, out CoordinateValueModel coordinate)
    {
        lock (_cacheSyncRoot)
        {
            if (_cache.TryGetValue(cacheKey, out var cached)
                && cached.ExpiresAt > DateTime.UtcNow)
            {
                coordinate = cached.Coordinate;
                return true;
            }

            _cache.Remove(cacheKey);
        }

        coordinate = default!;
        return false;
    }

    private void Cache(string cacheKey, CoordinateValueModel coordinate)
    {
        lock (_cacheSyncRoot)
        {
            _cache[cacheKey] = new CachedCoordinateEntry(
                coordinate,
                DateTime.UtcNow.Add(CacheLifetime));
        }
    }

    private static CoordinateValueModel ParseConvertedCoordinate(string rawValue)
    {
        var parts = rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
        {
            throw new InvalidOperationException($"高德返回坐标格式异常：{rawValue}");
        }

        return new CoordinateValueModel(longitude, latitude, CoordinateSystemKind.GCJ02);
    }

    private static string FormatLocation(CoordinateValueModel coordinate)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{coordinate.Longitude:0.###############},{coordinate.Latitude:0.###############}");
    }

    private static string BuildCacheKey(CoordinateValueModel coordinate)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{coordinate.CoordinateSystem}:{coordinate.Longitude:R},{coordinate.Latitude:R}");
    }

    private static bool IsCoordinateInRange(double longitude, double latitude)
    {
        return longitude is >= -180d and <= 180d
            && latitude is >= -90d and <= 90d
            && !(longitude == 0d && latitude == 0d);
    }

    private static string? ReadText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => property.ToString()
        };
    }

    private static bool IsPlaceholderValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.Contains("your-", StringComparison.OrdinalIgnoreCase)
            || value.Contains("placeholder", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CachedCoordinateEntry(
        CoordinateValueModel Coordinate,
        DateTime ExpiresAt);

    private sealed class PendingBatchEntry
    {
        public PendingBatchEntry(string cacheKey, AmapCoordinateConvertRequest request)
        {
            CacheKey = cacheKey;
            Request = request;
        }

        public string CacheKey { get; }

        public AmapCoordinateConvertRequest Request { get; }

        public List<int> Indexes { get; } = [];
    }
}
