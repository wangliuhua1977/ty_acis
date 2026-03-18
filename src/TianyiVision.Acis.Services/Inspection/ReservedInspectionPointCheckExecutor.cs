using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Integrations.Ctyun;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class ReservedInspectionPointCheckExecutor : IInspectionPointCheckExecutor
{
    private const string WebRtcMediaApiPath = "/open/token/vpaas/getDeviceMediaWebrtcUrl";
    private const string HlsMediaApiPath = "/open/token/cloud/getDeviceMediaUrlHls";
    private const string FlvMediaApiPath = "/open/token/cloud/getDeviceMediaUrlFlv";
    private const string H5MediaApiPath = "/open/token/vpaas/getH5StreamUrl";
    private static readonly TimeSpan PreviewSuccessCacheLifetime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PreviewFailureCacheLifetime = TimeSpan.FromSeconds(4);
    private static readonly ProtocolAttemptPlanEntry[] ClickPreviewProtocolPlan =
    [
        new(InspectionPreviewProtocols.Flv, FlvMediaApiPath, false),
        new(InspectionPreviewProtocols.Hls, HlsMediaApiPath, false),
        new(InspectionPreviewProtocols.WebRtc, WebRtcMediaApiPath, false)
    ];
    private static readonly ProtocolAttemptPlanEntry[] BackgroundInspectionProtocolPlan =
    [
        new(InspectionPreviewProtocols.Flv, FlvMediaApiPath, false),
        new(InspectionPreviewProtocols.Hls, HlsMediaApiPath, false)
    ];

    private static readonly string PreviewRootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TianyiVision.Acis",
        "inspection",
        "preview-hosts");

    private readonly CtyunOpenPlatformClient? _openPlatformClient;
    private readonly object _previewAttemptSyncRoot = new();
    private readonly Dictionary<string, CachedProtocolAttempt> _protocolAttemptCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<PreviewResolution>> _protocolAttemptInFlight = new(StringComparer.Ordinal);

    public ReservedInspectionPointCheckExecutor(CtyunOpenPlatformClient? openPlatformClient = null)
    {
        _openPlatformClient = openPlatformClient;
    }

    public async Task<InspectionPointCheckResult> ExecuteAsync(
        InspectionPointCheckRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var reservedSteps = BuildReservedSteps(request);
        var point = request.Point;

        if (request.Policy.RequireOnlineStatusCheck && point.IsOnline is null)
        {
            return new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Skipped,
                InspectionPointFailureCategoryModel.OnlineStatusPending,
                "AI未分析/证据不足",
                reservedSteps)
            {
                OnlineCheckResult = "在线状态待确认",
                FinalPlaybackResult = "在线检查失败",
                FailureReason = "在线状态待确认，未进入取流阶段。"
            };
        }

        if (request.Policy.RequireOnlineStatusCheck && point.IsOnline == false)
        {
            return new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Failed,
                InspectionPointFailureCategoryModel.DeviceOffline,
                "设备离线",
                reservedSteps)
            {
                OnlineCheckResult = "在线检查未通过",
                FinalPlaybackResult = "在线检查失败",
                FailureReason = "设备离线，未进入取流阶段。"
            };
        }

        var previewResolution = await ResolvePreviewAsync(request, cancellationToken).ConfigureAwait(false);
        if (!previewResolution.IsSuccess)
        {
            return new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Failed,
                previewResolution.FailureCategory,
                previewResolution.ResultSummary,
                reservedSteps)
            {
                OnlineCheckResult = "在线检查通过",
                StreamUrlAcquireResult = previewResolution.StreamUrlAcquireResult,
                PlaybackAttemptCount = previewResolution.PlaybackAttemptCount,
                ProtocolFallbackUsed = previewResolution.ProtocolFallbackUsed,
                FinalPlaybackResult = previewResolution.FinalPlaybackResult,
                PreviewFailureClassification = previewResolution.PreviewFailureClassification,
                FailureReason = previewResolution.FailureReason
            };
        }

        var resultStatus = InspectionPointExecutionStatusModel.Succeeded;
        var resultCategory = InspectionPointFailureCategoryModel.PlaybackSucceeded;
        var resultSummary = "播放成功";

        if (LooksLikeImageAbnormal(point))
        {
            resultStatus = InspectionPointExecutionStatusModel.Failed;
            resultCategory = InspectionPointFailureCategoryModel.ImageAbnormalDetected;
            resultSummary = "画面异常";
        }

        return new InspectionPointCheckResult(
            resultStatus,
            resultCategory,
            resultSummary,
            reservedSteps)
        {
            OnlineCheckResult = "在线检查通过",
            StreamUrlAcquireResult = previewResolution.StreamUrlAcquireResult,
            PlaybackAttemptCount = previewResolution.PlaybackAttemptCount,
            ProtocolFallbackUsed = previewResolution.ProtocolFallbackUsed,
            FinalPlaybackResult = previewResolution.FinalPlaybackResult,
            PreviewUrl = previewResolution.PreviewUrl,
            PreviewHostKind = previewResolution.PreviewHostKind,
            PreviewFailureClassification = previewResolution.PreviewFailureClassification,
            ScreenshotPlannedCount = request.PreviewOnly ? 0 : Math.Max(1, request.VideoInspection.ScreenshotCount),
            ScreenshotIntervalSeconds = request.PreviewOnly ? 0 : Math.Max(1, request.VideoInspection.ScreenshotIntervalSeconds),
            EvidenceCaptureState = request.PreviewOnly
                ? InspectionEvidenceValueKeys.CaptureStateNone
                : InspectionEvidenceValueKeys.CaptureStatePending,
            EvidenceSummary = request.PreviewOnly ? string.Empty : "待截图取证。",
            EvidenceRetentionMode = request.PreviewOnly ? string.Empty : request.VideoInspection.EvidenceRetentionMode,
            EvidenceRetentionDays = request.PreviewOnly ? 0 : Math.Max(0, request.VideoInspection.EvidenceRetentionDays),
            AllowManualSupplementScreenshot = !request.PreviewOnly && request.VideoInspection.AllowManualSupplementScreenshot
        };
    }

    private async Task<PreviewResolution> ResolvePreviewAsync(
        InspectionPointCheckRequest request,
        CancellationToken cancellationToken)
    {
        var pointId = request.Point.PointId;
        var deviceCode = request.Point.DeviceCode;

        if (_openPlatformClient is null)
        {
            WritePreviewDiagnostic(
                pointId,
                deviceCode,
                "none",
                [],
                "not_called",
                "runtime_unavailable",
                0,
                "preview_client_unavailable",
                string.Empty,
                string.Empty,
                "未调取流接口",
                "未开始播放器探测",
                "当前环境未接入实时预览能力。");

            return PreviewResolution.Failed(
                InspectionPointFailureCategoryModel.NoStreamAddress,
                "在线但无流地址",
                "未调取流接口",
                0,
                false,
                "无流地址",
                "当前环境未接入实时预览能力，未调用取流接口。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var attemptedProtocols = new List<string>();
        PreviewResolution? lastFailure = null;

        foreach (var attempt in BuildProtocolPlan(request))
        {
            cancellationToken.ThrowIfCancellationRequested();

            attemptedProtocols.Add(attempt.Protocol);
            var attemptResult = await ExecuteProtocolAttemptAsync(request, attempt, attemptedProtocols, cancellationToken)
                .ConfigureAwait(false);
            if (attemptResult.IsSuccess)
            {
                return attemptResult with
                {
                    PlaybackAttemptCount = attemptedProtocols.Count,
                    ProtocolFallbackUsed = attemptedProtocols.Count > 1
                };
            }

            lastFailure = ChoosePreferredFailure(lastFailure, attemptResult);
            if (!request.PreviewOnly && !request.VideoInspection.EnableProtocolFallbackRetry)
            {
                break;
            }
        }

        return (lastFailure ?? PreviewResolution.Failed(
            InspectionPointFailureCategoryModel.NoStreamAddress,
            "在线但无流地址",
            "未获取到可用流地址",
            attemptedProtocols.Count,
            attemptedProtocols.Count > 1,
            "无流地址",
            "所有协议均未返回可用流地址。")) with
        {
            PlaybackAttemptCount = attemptedProtocols.Count,
            ProtocolFallbackUsed = attemptedProtocols.Count > 1
        };
    }

    private IEnumerable<ProtocolAttemptPlanEntry> BuildProtocolPlan(InspectionPointCheckRequest request)
    {
        if (request.PreviewOnly)
        {
            foreach (var attempt in ClickPreviewProtocolPlan)
            {
                yield return attempt;
            }

            yield break;
        }

        yield return BackgroundInspectionProtocolPlan[0];
        if (request.VideoInspection.EnableProtocolFallbackRetry)
        {
            yield return BackgroundInspectionProtocolPlan[1];
        }
    }

    private async Task<PreviewResolution> ExecuteProtocolAttemptAsync(
        InspectionPointCheckRequest request,
        ProtocolAttemptPlanEntry attempt,
        IReadOnlyList<string> attemptedProtocols,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{request.Point.DeviceCode}|{attempt.Protocol}";
        if (TryGetCachedProtocolAttempt(cacheKey, out var cachedAttempt))
        {
            var cachedResolution = cachedAttempt.Resolution;
            WritePreviewDiagnostic(
                request.Point.PointId,
                request.Point.DeviceCode,
                attempt.Protocol,
                attemptedProtocols,
                attempt.MediaApiPath,
                "cached",
                0,
                "cached_protocol_attempt",
                string.Empty,
                cachedResolution.PreviewUrl,
                cachedResolution.IsSuccess ? "缓存命中并复用预览地址" : "缓存命中并进入短失败冷却",
                "未开始播放器探测",
                cachedResolution.FailureReason);
            return cachedResolution;
        }

        Task<PreviewResolution> inFlightTask;
        var created = false;
        lock (_previewAttemptSyncRoot)
        {
            if (!_protocolAttemptInFlight.TryGetValue(cacheKey, out inFlightTask!))
            {
                inFlightTask = Task.Run(
                    () => ExecuteProtocolAttemptCore(request, attempt, attemptedProtocols),
                    CancellationToken.None);
                _protocolAttemptInFlight[cacheKey] = inFlightTask;
                created = true;
            }
        }

        try
        {
            var resolution = await inFlightTask.ConfigureAwait(false);
            if (created)
            {
                CacheProtocolAttempt(cacheKey, resolution);
            }

            return resolution;
        }
        finally
        {
            if (created)
            {
                lock (_previewAttemptSyncRoot)
                {
                    if (_protocolAttemptInFlight.TryGetValue(cacheKey, out var current)
                        && ReferenceEquals(current, inFlightTask))
                    {
                        _protocolAttemptInFlight.Remove(cacheKey);
                    }
                }
            }
        }
    }

    private PreviewResolution ExecuteProtocolAttemptCore(
        InspectionPointCheckRequest request,
        ProtocolAttemptPlanEntry attempt,
        IReadOnlyList<string> attemptedProtocols)
    {
        if (attempt.UsesStreamSet)
        {
            var h5Response = _openPlatformClient!.GetH5PreviewStreamSet(request.Point.DeviceCode);
            return EvaluateStreamSetAttempt(request, attemptedProtocols, h5Response);
        }

        var response = _openPlatformClient!.GetPreviewMediaUrl(request.Point.DeviceCode, attempt.MediaApiPath);
        return EvaluateMediaAttempt(request, attempt.Protocol, attemptedProtocols, response);
    }

    private bool TryGetCachedProtocolAttempt(string cacheKey, out CachedProtocolAttempt cachedAttempt)
    {
        lock (_previewAttemptSyncRoot)
        {
            if (_protocolAttemptCache.TryGetValue(cacheKey, out var cached)
                && cached.ExpiresAt > DateTime.UtcNow)
            {
                cachedAttempt = cached;
                return true;
            }

            _protocolAttemptCache.Remove(cacheKey);
        }

        cachedAttempt = null!;
        return false;
    }

    private void CacheProtocolAttempt(string cacheKey, PreviewResolution resolution)
    {
        var expiresAt = DateTime.UtcNow + (resolution.IsSuccess ? PreviewSuccessCacheLifetime : PreviewFailureCacheLifetime);
        lock (_previewAttemptSyncRoot)
        {
            _protocolAttemptCache[cacheKey] = new CachedProtocolAttempt(resolution, expiresAt);
        }
    }

    private static PreviewResolution ChoosePreferredFailure(
        PreviewResolution? current,
        PreviewResolution candidate)
    {
        if (candidate.IsSuccess)
        {
            return candidate;
        }

        if (current is null)
        {
            return candidate;
        }

        return GetFailurePriority(candidate.FailureCategory) >= GetFailurePriority(current.Value.FailureCategory)
            ? candidate
            : current.Value;
    }

    private static int GetFailurePriority(InspectionPointFailureCategoryModel category)
    {
        return category switch
        {
            InspectionPointFailureCategoryModel.StreamUrlResolvedPlaybackFailed => 5,
            InspectionPointFailureCategoryModel.StreamUrlParseFailed => 4,
            InspectionPointFailureCategoryModel.ProtocolFallbackStillFailed => 3,
            InspectionPointFailureCategoryModel.NoStreamAddress => 2,
            _ => 1
        };
    }

    private static bool TryCreateHostedPreview(
        string deviceCode,
        string mediaKind,
        string sourceUrl,
        out Uri previewUri,
        out string hostFailureCategory)
    {
        previewUri = null!;
        hostFailureCategory = string.Empty;
        var normalizedSourceUrl = sourceUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSourceUrl)
            || !Uri.TryCreate(normalizedSourceUrl, UriKind.Absolute, out var uri))
        {
            hostFailureCategory = InspectionPreviewFailureClassifications.UrlUnloadable;
            return false;
        }

        if (string.Equals(mediaKind, InspectionPreviewProtocols.WebRtc, StringComparison.Ordinal))
        {
            hostFailureCategory = InspectionPreviewFailureClassifications.PlayerProtocolNotSupported;
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("http" or "https" or "ws" or "wss"))
        {
            hostFailureCategory = InspectionPreviewFailureClassifications.UrlUnloadable;
            return false;
        }

        previewUri = BuildPreviewHostUri(deviceCode, mediaKind, normalizedSourceUrl);
        return true;
    }

    private static IEnumerable<PreviewCandidate> EnumerateH5PreviewCandidates(IReadOnlyList<CtyunPreviewStreamUrlDto> streamUrls)
    {
        var candidates = new List<PreviewCandidate>();
        foreach (var stream in streamUrls)
        {
            var candidate = CreatePreviewCandidate(stream);
            if (candidate is not null)
            {
                candidates.Add(candidate.Value);
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal);
    }

    private static PreviewCandidate? CreatePreviewCandidate(CtyunPreviewStreamUrlDto stream)
    {
        var sourceUrl = stream.StreamUrl?.Trim();
        if (string.IsNullOrWhiteSpace(sourceUrl)
            || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var path = uri.AbsolutePath;

        if (stream.Protocol == 3
            || string.Equals(stream.ParsedProtocolType, InspectionPreviewProtocols.WebRtc, StringComparison.OrdinalIgnoreCase)
            || sourceUrl.Contains("webrtc", StringComparison.OrdinalIgnoreCase)
            || sourceUrl.Contains("rtc", StringComparison.OrdinalIgnoreCase))
        {
            return new PreviewCandidate(sourceUrl, InspectionPreviewProtocols.WebRtc, "H5 WebRTC", 2);
        }

        if (stream.Protocol == 2
            || string.Equals(stream.ParsedProtocolType, InspectionPreviewProtocols.Flv, StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".flv", StringComparison.OrdinalIgnoreCase))
        {
            return new PreviewCandidate(sourceUrl, InspectionPreviewProtocols.Flv, "H5 FLV", 0);
        }

        if (stream.Protocol == 1
            || string.Equals(stream.ParsedProtocolType, InspectionPreviewProtocols.Hls, StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return new PreviewCandidate(sourceUrl, InspectionPreviewProtocols.Hls, "H5 HLS", 1);
        }

        if ((scheme is "http" or "https")
            && path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return new PreviewCandidate(sourceUrl, InspectionPreviewProtocols.Direct, "H5 MP4", 3);
        }

        return null;
    }

    private PreviewResolution EvaluateMediaAttempt(
        InspectionPointCheckRequest request,
        string selectedProtocol,
        IReadOnlyList<string> attemptedProtocols,
        ServiceResponse<CtyunPreviewMediaUrlDto> response)
    {
        var dto = response.Data ?? new CtyunPreviewMediaUrlDto(string.Empty, null);
        var mediaApiPath = string.IsNullOrWhiteSpace(dto.MediaApiPath) ? SelectedProtocolToPath(selectedProtocol) : dto.MediaApiPath;
        var responseUrlFieldRaw = dto.ResponseUrlFieldRaw ?? string.Empty;
        var parsedPreviewUrl = dto.ParsedPreviewUrl ?? string.Empty;

        if (!response.IsSuccess)
        {
            var failureReason = string.IsNullOrWhiteSpace(dto.ResponseMessage)
                ? response.Message
                : dto.ResponseMessage;
            WritePreviewDiagnostic(
                request.Point.PointId,
                request.Point.DeviceCode,
                selectedProtocol,
                attemptedProtocols,
                mediaApiPath,
                dto.RequestPayloadSummary,
                dto.ResponseCode,
                dto.ResponseMessage,
                responseUrlFieldRaw,
                parsedPreviewUrl,
                "取流接口业务失败",
                "未开始播放器探测",
                failureReason,
                dto);

            return PreviewResolution.Failed(
                InspectionPointFailureCategoryModel.NoStreamAddress,
                "在线但无流地址",
                "取流接口业务失败",
                attemptedProtocols.Count,
                attemptedProtocols.Count > 1,
                "无流地址",
                failureReason);
        }

        if (string.IsNullOrWhiteSpace(responseUrlFieldRaw))
        {
            var streamAcquireResult = !string.IsNullOrWhiteSpace(dto.OriginalDataRaw)
                ? "接口成功但 data 仍是密文"
                : "接口成功但URL字段为空";
            var failureReason = !string.IsNullOrWhiteSpace(dto.OriginalDataRaw)
                ? "取流接口返回成功，但 data 仍是密文或多层包装，暂未解析出可用 URL。"
                : "取流接口返回成功，但未返回可用流地址字段。";
            WritePreviewDiagnostic(
                request.Point.PointId,
                request.Point.DeviceCode,
                selectedProtocol,
                attemptedProtocols,
                mediaApiPath,
                dto.RequestPayloadSummary,
                dto.ResponseCode,
                dto.ResponseMessage,
                responseUrlFieldRaw,
                parsedPreviewUrl,
                streamAcquireResult,
                "未开始播放器探测",
                failureReason,
                dto);

            return PreviewResolution.Failed(
                InspectionPointFailureCategoryModel.NoStreamAddress,
                "在线但无流地址",
                streamAcquireResult,
                attemptedProtocols.Count,
                attemptedProtocols.Count > 1,
                "无流地址",
                failureReason);
        }

        if (!TryCreateHostedPreview(
                request.Point.DeviceCode,
                selectedProtocol,
                parsedPreviewUrl,
                out var previewUri,
                out var hostFailureCategory))
        {
            var isPlayerUnsupported = string.Equals(
                hostFailureCategory,
                InspectionPreviewFailureClassifications.PlayerProtocolNotSupported,
                StringComparison.Ordinal);
            var failureCategory = isPlayerUnsupported
                ? InspectionPointFailureCategoryModel.StreamUrlResolvedPlaybackFailed
                : InspectionPointFailureCategoryModel.StreamUrlParseFailed;
            var streamAcquireResult = isPlayerUnsupported
                ? $"已获取{selectedProtocol.ToUpperInvariant()}流地址但当前宿主不支持"
                : "已获取流地址但解析失败";
            var finalPlaybackResult = isPlayerUnsupported ? "播放器不支持" : "播放失败";
            var failureReason = isPlayerUnsupported
                ? $"已拿到 {selectedProtocol.ToUpperInvariant()} 真实 URL，但当前播放器宿主暂不支持该协议。"
                : $"取流地址存在，但协议或地址格式不可用于预览。failureCategory={hostFailureCategory}";
            WritePreviewDiagnostic(
                request.Point.PointId,
                request.Point.DeviceCode,
                selectedProtocol,
                attemptedProtocols,
                mediaApiPath,
                dto.RequestPayloadSummary,
                dto.ResponseCode,
                dto.ResponseMessage,
                responseUrlFieldRaw,
                parsedPreviewUrl,
                streamAcquireResult,
                "未开始播放器探测",
                failureReason,
                dto,
                hostFailureCategory);

            return PreviewResolution.Failed(
                failureCategory,
                isPlayerUnsupported ? "已获取流地址但播放器失败" : "已获取流地址但播放失败",
                streamAcquireResult,
                attemptedProtocols.Count,
                attemptedProtocols.Count > 1,
                finalPlaybackResult,
                failureReason,
                hostFailureCategory);
        }

        var successResult = $"已获取{selectedProtocol.ToUpperInvariant()}流地址";
        WritePreviewDiagnostic(
            request.Point.PointId,
            request.Point.DeviceCode,
            selectedProtocol,
            attemptedProtocols,
            mediaApiPath,
            dto.RequestPayloadSummary,
            dto.ResponseCode,
            dto.ResponseMessage,
            responseUrlFieldRaw,
            parsedPreviewUrl,
            successResult,
            "待WebView播放器探测",
            "none",
            dto,
            InspectionPreviewFailureClassifications.None);

        return PreviewResolution.Succeeded(
            previewUri.AbsoluteUri,
            selectedProtocol,
            successResult,
            "播放成功");
    }

    private PreviewResolution EvaluateStreamSetAttempt(
        InspectionPointCheckRequest request,
        IReadOnlyList<string> attemptedProtocols,
        ServiceResponse<CtyunPreviewStreamSetDto> response)
    {
        var dto = response.Data ?? new CtyunPreviewStreamSetDto(null, null, []);
        var mediaApiPath = string.IsNullOrWhiteSpace(dto.MediaApiPath) ? H5MediaApiPath : dto.MediaApiPath;
        var responseUrlFieldRaw = dto.ResponseUrlFieldRaw ?? string.Empty;

        if (!response.IsSuccess)
        {
            var failureReason = string.IsNullOrWhiteSpace(dto.ResponseMessage)
                ? response.Message
                : dto.ResponseMessage;
            WritePreviewDiagnostic(
                request.Point.PointId,
                request.Point.DeviceCode,
                "h5",
                attemptedProtocols,
                mediaApiPath,
                dto.RequestPayloadSummary,
                dto.ResponseCode,
                dto.ResponseMessage,
                responseUrlFieldRaw,
                string.Empty,
                "取流接口业务失败",
                "未开始播放器探测",
                failureReason,
                dto);

            return PreviewResolution.Failed(
                InspectionPointFailureCategoryModel.NoStreamAddress,
                "在线但无流地址",
                "取流接口业务失败",
                attemptedProtocols.Count,
                attemptedProtocols.Count > 1,
                "无流地址",
                failureReason);
        }

        var candidates = EnumerateH5PreviewCandidates(dto.StreamUrls).ToList();
        if (candidates.Count == 0)
        {
            var hasRawUrl = dto.StreamUrls.Any(item => !string.IsNullOrWhiteSpace(item.RawStreamUrl) || !string.IsNullOrWhiteSpace(item.StreamUrl))
                || !string.IsNullOrWhiteSpace(responseUrlFieldRaw);
            var hasEncryptedData = !string.IsNullOrWhiteSpace(dto.OriginalDataRaw);
            var streamAcquireResult = hasEncryptedData
                ? "接口成功但 data 仍是密文"
                : hasRawUrl
                    ? "已获取流地址但解析失败"
                    : "接口成功但URL字段为空";
            var failureCategory = hasRawUrl
                ? InspectionPointFailureCategoryModel.StreamUrlParseFailed
                : InspectionPointFailureCategoryModel.NoStreamAddress;
            var failureReason = hasEncryptedData
                ? "H5 取流接口返回成功，但 data 仍未成功解密并提取出可用 URL。"
                : hasRawUrl
                    ? "H5取流接口返回了地址，但没有任何可用于预览的协议。"
                    : "H5取流接口返回成功，但未返回可用流地址字段。";

            WritePreviewDiagnostic(
                request.Point.PointId,
                request.Point.DeviceCode,
                "h5",
                attemptedProtocols,
                mediaApiPath,
                dto.RequestPayloadSummary,
                dto.ResponseCode,
                dto.ResponseMessage,
                responseUrlFieldRaw,
                string.Empty,
                streamAcquireResult,
                "未开始播放器探测",
                failureReason,
                dto);

            return PreviewResolution.Failed(
                failureCategory,
                failureCategory == InspectionPointFailureCategoryModel.NoStreamAddress
                    ? "在线但无流地址"
                    : "已获取流地址但播放失败",
                streamAcquireResult,
                attemptedProtocols.Count,
                attemptedProtocols.Count > 1,
                failureCategory == InspectionPointFailureCategoryModel.NoStreamAddress ? "无流地址" : "播放失败",
                failureReason);
        }

        PreviewResolution? lastFailure = null;
        foreach (var candidate in candidates)
        {
            if (!TryCreateHostedPreview(
                    request.Point.DeviceCode,
                    candidate.MediaKind,
                    candidate.SourceUrl,
                    out var previewUri,
                    out var hostFailureCategory))
            {
                var isPlayerUnsupported = string.Equals(
                    hostFailureCategory,
                    InspectionPreviewFailureClassifications.PlayerProtocolNotSupported,
                    StringComparison.Ordinal);
                var failureCategory = isPlayerUnsupported
                    ? InspectionPointFailureCategoryModel.StreamUrlResolvedPlaybackFailed
                    : InspectionPointFailureCategoryModel.StreamUrlParseFailed;
                var streamAcquireResult = isPlayerUnsupported
                    ? $"已获取{candidate.DisplayName}流地址但当前宿主不支持"
                    : "已获取流地址但解析失败";
                var finalPlaybackResult = isPlayerUnsupported ? "播放器不支持" : "播放失败";
                var failureReason = isPlayerUnsupported
                    ? $"已拿到 {candidate.DisplayName} 真实 URL，但当前播放器宿主暂不支持该协议。"
                    : $"H5取流返回了 URL，但宿主无法承载。failureCategory={hostFailureCategory}";

                WritePreviewDiagnostic(
                    request.Point.PointId,
                    request.Point.DeviceCode,
                    candidate.MediaKind,
                    attemptedProtocols,
                    mediaApiPath,
                    dto.RequestPayloadSummary,
                    dto.ResponseCode,
                    dto.ResponseMessage,
                    responseUrlFieldRaw,
                    candidate.SourceUrl,
                    streamAcquireResult,
                    "未开始播放器探测",
                    failureReason,
                    dto,
                    hostFailureCategory);

                lastFailure = ChoosePreferredFailure(
                    lastFailure,
                    PreviewResolution.Failed(
                        failureCategory,
                        isPlayerUnsupported ? "已获取流地址但播放器失败" : "已获取流地址但播放失败",
                        streamAcquireResult,
                        attemptedProtocols.Count,
                        attemptedProtocols.Count > 1,
                        finalPlaybackResult,
                        failureReason,
                        hostFailureCategory));
                continue;
            }

            var successResult = $"已获取{candidate.DisplayName}流地址";
            WritePreviewDiagnostic(
                request.Point.PointId,
                request.Point.DeviceCode,
                candidate.MediaKind,
                attemptedProtocols,
                mediaApiPath,
                dto.RequestPayloadSummary,
                dto.ResponseCode,
                dto.ResponseMessage,
                responseUrlFieldRaw,
                candidate.SourceUrl,
                successResult,
                "待WebView播放器探测",
                "none",
                dto,
                InspectionPreviewFailureClassifications.None);

            return PreviewResolution.Succeeded(
                previewUri.AbsoluteUri,
                candidate.MediaKind,
                successResult,
                "播放成功") with
            {
                PlaybackAttemptCount = attemptedProtocols.Count,
                ProtocolFallbackUsed = attemptedProtocols.Count > 1
            };
        }

        if (lastFailure is not null)
        {
            return lastFailure.Value with
            {
                PlaybackAttemptCount = attemptedProtocols.Count,
                ProtocolFallbackUsed = attemptedProtocols.Count > 1
            };
        }

        WritePreviewDiagnostic(
            request.Point.PointId,
            request.Point.DeviceCode,
            "h5",
            attemptedProtocols,
            mediaApiPath,
            dto.RequestPayloadSummary,
            dto.ResponseCode,
            dto.ResponseMessage,
            responseUrlFieldRaw,
            string.Empty,
            "已获取流地址但解析失败",
            "未开始播放器探测",
            "H5取流接口返回了地址，但未生成可用预览宿主。",
            dto);

        return PreviewResolution.Failed(
            InspectionPointFailureCategoryModel.StreamUrlParseFailed,
            "已获取流地址但播放失败",
            "已获取流地址但解析失败",
            attemptedProtocols.Count,
            attemptedProtocols.Count > 1,
            "播放失败",
            "H5取流接口返回了地址，但未生成可用预览宿主。");
    }

    private static Uri BuildPreviewHostUri(string deviceCode, string mediaKind, string sourceUrl)
    {
        Directory.CreateDirectory(PreviewRootDirectory);

        var fileName = $"{deviceCode.Trim()}-{mediaKind}-{ComputeHash(sourceUrl)}.html";
        var filePath = Path.Combine(PreviewRootDirectory, fileName);
        File.WriteAllText(filePath, BuildPreviewHostHtml(mediaKind, sourceUrl), Encoding.UTF8);
        return new Uri(filePath, UriKind.Absolute);
    }

    private static string BuildPreviewHostHtml(string mediaKind, string sourceUrl)
    {
        var encodedUrl = JavaScriptEncoder.Default.Encode(sourceUrl);
        var encodedProtocol = JavaScriptEncoder.Default.Encode(mediaKind);

        var commonScript = $$"""
            <script>
            const video = document.getElementById('player');
            const sourceUrl = '{{encodedUrl}}';
            const selectedProtocol = '{{encodedProtocol}}';
            let playbackReady = false;
            let playbackStalledTimer = null;

            function clearPlaybackStalledTimer() {
              if (playbackStalledTimer !== null) {
                window.clearTimeout(playbackStalledTimer);
                playbackStalledTimer = null;
              }
            }

            function armPlaybackStalledTimer(detail, timeoutMs) {
              if (selectedProtocol !== '{{InspectionPreviewProtocols.Hls}}' || playbackReady) {
                return;
              }

              clearPlaybackStalledTimer();
              playbackStalledTimer = window.setTimeout(() => {
                if (!playbackReady) {
                  reportPlayback('playback_stalled', detail);
                }
              }, timeoutMs);
            }

            function reportPlayback(state, detail) {
              document.body.setAttribute('data-state', state);
              if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                  kind: 'previewProbe',
                  state: state,
                  detail: detail || '',
                  protocol: selectedProtocol,
                  sourceUrl: sourceUrl
                });
              }
            }

            reportPlayback('probe_initialized', 'player_bootstrap');

            video.addEventListener('loadedmetadata', () => {
              reportPlayback('loaded_metadata', 'loadedmetadata');
              armPlaybackStalledTimer('hls_loadedmetadata_timeout', 8000);
            });
            video.addEventListener('playing', () => {
              playbackReady = true;
              clearPlaybackStalledTimer();
              reportPlayback('playback_ready', 'playing');
            });
            video.addEventListener('stalled', () => {
              reportPlayback('playback_warning', 'stalled');
              armPlaybackStalledTimer('stalled', 4000);
            });
            video.addEventListener('waiting', () => {
              reportPlayback('playback_warning', 'waiting');
              armPlaybackStalledTimer('waiting', 4000);
            });
            video.addEventListener('error', () => {
              clearPlaybackStalledTimer();
              const mediaError = video.error ? `media_error_${video.error.code}` : 'video_error';
              reportPlayback('playback_failed', mediaError);
            });
            </script>
            """;

        var protocolScript = mediaKind switch
        {
            InspectionPreviewProtocols.Flv => $$"""
                <script src="https://cdn.jsdelivr.net/npm/flv.js@latest/dist/flv.min.js"></script>
                <script>
                if (window.flvjs && flvjs.isSupported()) {
                  const player = flvjs.createPlayer({ type: 'flv', url: sourceUrl });
                  player.on(flvjs.Events.ERROR, (errorType, errorDetail) => {
                    reportPlayback('playback_failed', `flvjs:${errorType}:${errorDetail}`);
                  });
                  player.attachMediaElement(video);
                  player.load();
                  player.play().catch(error => reportPlayback('playback_warning', `play_promise:${error && error.message ? error.message : 'rejected'}`));
                } else {
                  reportPlayback('playback_failed', 'flvjs_not_supported');
                }
                </script>
                """,
            InspectionPreviewProtocols.Hls => $$"""
                <script src="https://cdn.jsdelivr.net/npm/hls.js@latest"></script>
                <script>
                if (video.canPlayType('application/vnd.apple.mpegurl')) {
                  video.src = sourceUrl;
                  armPlaybackStalledTimer('hls_native_playback_timeout', 8000);
                  video.play().catch(error => reportPlayback('playback_warning', `play_promise:${error && error.message ? error.message : 'rejected'}`));
                } else if (window.Hls && Hls.isSupported()) {
                  const hls = new Hls();
                  hls.on(Hls.Events.MANIFEST_PARSED, () => {
                    reportPlayback('manifest_parsed', 'hls_manifest_parsed');
                    armPlaybackStalledTimer('hls_manifest_parsed_timeout', 8000);
                  });
                  hls.on(Hls.Events.ERROR, (_, data) => {
                    const detail = `hls:${data.type}:${data.details}`;
                    reportPlayback(data.fatal ? 'playback_failed' : 'playback_warning', detail);
                  });
                  hls.loadSource(sourceUrl);
                  hls.attachMedia(video);
                  video.play().catch(error => reportPlayback('playback_warning', `play_promise:${error && error.message ? error.message : 'rejected'}`));
                } else {
                  reportPlayback('playback_failed', 'hls_not_supported');
                }
                </script>
                """,
            _ => $$"""
                <script>
                video.src = sourceUrl;
                video.play().catch(error => reportPlayback('playback_warning', `play_promise:${error && error.message ? error.message : 'rejected'}`));
                </script>
                """
        };

        return $$"""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8" />
              <meta http-equiv="X-UA-Compatible" content="IE=edge" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <style>
                html, body {
                  width: 100%;
                  height: 100%;
                  margin: 0;
                  background: #07111f;
                }
                body {
                  display: flex;
                  align-items: center;
                  justify-content: center;
                }
                video {
                  width: 100%;
                  height: 100%;
                  object-fit: contain;
                  background: #07111f;
                }
              </style>
            </head>
            <body>
              <video id="player" controls autoplay muted playsinline></video>
              {{commonScript}}
              {{protocolScript}}
            </body>
            </html>
            """;
    }

    private static IReadOnlyList<InspectionReservedStepModel> BuildReservedSteps(InspectionPointCheckRequest request)
    {
        return
        [
            new(
                InspectionExecutionReservationStepModel.StreamAddress,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 流地址获取",
                "优先获取当前点位可直接承载的预览流地址。"),
            new(
                InspectionExecutionReservationStepModel.PlaybackReachability,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 播放检查",
                request.Policy.RequirePlaybackCheck
                    ? "当前策略要求播放检查，执行器会优先尝试可直接预览的流协议。"
                    : "当前策略未强制播放检查，但仍会尝试建立当前点位预览。"),
            new(
                InspectionExecutionReservationStepModel.ProtocolFallbackRetry,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 协议切换重试",
                request.VideoInspection.EnableProtocolFallbackRetry
                    ? request.PreviewOnly
                        ? "当前已启用协议切换重试，点位点击预览将按 FLV、HLS、WebRTC 的顺序依次尝试。"
                        : "当前已启用协议切换重试，后台巡检将按 FLV、HLS 的顺序依次尝试。"
                    : "当前未启用协议切换重试。"),
            new(
                InspectionExecutionReservationStepModel.AutoScreenshot,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 自动截图",
                request.PreviewOnly
                    ? "当前仅准备实时预览，本次不触发截图取证。"
                    : $"当前计划自动截图 {request.VideoInspection.ScreenshotCount} 张。"),
            new(
                InspectionExecutionReservationStepModel.ManualSupplementScreenshot,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 人工补截图",
                request.PreviewOnly || !request.VideoInspection.AllowManualSupplementScreenshot
                    ? "本次未启用人工补截图。"
                    : "当前允许人工补截图。"),
            new(
                InspectionExecutionReservationStepModel.AiDecision,
                "IInspectionPointCheckExecutor.ExecuteAsync -> AI 判定",
                request.PreviewOnly
                    ? "当前仅准备实时预览，不触发 AI 判定。"
                    : request.Policy.EnableInterfaceAiDecision || request.Policy.EnableLocalScreenshotAnalysis
                        ? "当前执行完成后可继续进入 AI 判定。"
                        : "当前未启用 AI 判定。")
        ];
    }

    private static bool LooksLikeImageAbnormal(PointWorkspaceItemModel point)
    {
        return (!string.IsNullOrWhiteSpace(point.ImageStatusText)
                && point.ImageStatusText.Contains("异常", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(point.CurrentFaultType)
                && point.CurrentFaultType.Contains("画面", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(point.CurrentFaultSummary)
                && point.CurrentFaultSummary.Contains("画面", StringComparison.Ordinal));
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string SelectedProtocolToPath(string selectedProtocol)
    {
        return selectedProtocol switch
        {
            InspectionPreviewProtocols.WebRtc => WebRtcMediaApiPath,
            InspectionPreviewProtocols.Hls => HlsMediaApiPath,
            InspectionPreviewProtocols.Flv => FlvMediaApiPath,
            _ => H5MediaApiPath
        };
    }

    private static void WritePreviewDiagnostic(
        string pointId,
        string deviceCode,
        string selectedProtocol,
        IReadOnlyList<string> attemptedProtocols,
        string mediaApiPath,
        string requestPayloadSummary,
        int responseCode,
        string responseMessage,
        string responseUrlFieldRaw,
        string parsedPreviewUrl,
        string streamAcquireResult,
        string playbackProbeResult,
        string failureReason,
        CtyunPreviewMediaUrlDto dto,
        string previewFailureClassification = InspectionPreviewFailureClassifications.None)
    {
        WritePreviewDiagnostic(
            pointId,
            deviceCode,
            selectedProtocol,
            attemptedProtocols,
            mediaApiPath,
            requestPayloadSummary,
            responseCode,
            responseMessage,
            responseUrlFieldRaw,
            parsedPreviewUrl,
            streamAcquireResult,
            playbackProbeResult,
            failureReason,
            dto.ResponseContentType,
            dto.ResponseBodyPreviewFirst300,
            dto.ResponseEnvelopeShape,
            dto.ResponseJsonTopLevelKeys,
            dto.ResponseCandidateUrlKeys,
            dto.ResponseNestedPathTried,
            dto.OriginalDataRaw,
            dto.MatchedFieldPath,
            dto.DecryptMode,
            dto.ParsedProtocolType,
            previewFailureClassification);
    }

    private static void WritePreviewDiagnostic(
        string pointId,
        string deviceCode,
        string selectedProtocol,
        IReadOnlyList<string> attemptedProtocols,
        string mediaApiPath,
        string requestPayloadSummary,
        int responseCode,
        string responseMessage,
        string responseUrlFieldRaw,
        string parsedPreviewUrl,
        string streamAcquireResult,
        string playbackProbeResult,
        string failureReason,
        CtyunPreviewStreamSetDto dto,
        string previewFailureClassification = InspectionPreviewFailureClassifications.None)
    {
        WritePreviewDiagnostic(
            pointId,
            deviceCode,
            selectedProtocol,
            attemptedProtocols,
            mediaApiPath,
            requestPayloadSummary,
            responseCode,
            responseMessage,
            responseUrlFieldRaw,
            parsedPreviewUrl,
            streamAcquireResult,
            playbackProbeResult,
            failureReason,
            dto.ResponseContentType,
            dto.ResponseBodyPreviewFirst300,
            dto.ResponseEnvelopeShape,
            dto.ResponseJsonTopLevelKeys,
            dto.ResponseCandidateUrlKeys,
            dto.ResponseNestedPathTried,
            dto.OriginalDataRaw,
            dto.MatchedFieldPath,
            dto.DecryptMode,
            dto.ParsedProtocolType,
            previewFailureClassification);
    }

    private static void WritePreviewDiagnostic(
        string pointId,
        string deviceCode,
        string selectedProtocol,
        IReadOnlyList<string> attemptedProtocols,
        string mediaApiPath,
        string requestPayloadSummary,
        int responseCode,
        string responseMessage,
        string responseUrlFieldRaw,
        string parsedPreviewUrl,
        string streamAcquireResult,
        string playbackProbeResult,
        string failureReason,
        string responseContentType = "",
        string responseBodyPreviewFirst300 = "",
        string responseEnvelopeShape = "",
        string responseJsonTopLevelKeys = "",
        string responseCandidateUrlKeys = "",
        string responseNestedPathTried = "",
        string originalDataRaw = "",
        string matchedFieldPath = "",
        string decryptMode = "",
        string parsedProtocolType = "",
        string previewFailureClassification = InspectionPreviewFailureClassifications.None)
    {
        var payload = $"pointId = {NormalizeLogValue(pointId)}, deviceCode = {NormalizeLogValue(deviceCode)}, selectedProtocol = {NormalizeLogValue(selectedProtocol)}, attemptedProtocols = {NormalizeLogValue(string.Join(">", attemptedProtocols))}, mediaApiPath = {NormalizeLogValue(mediaApiPath)}, requestPayloadSummary = {NormalizeLogValue(requestPayloadSummary)}, responseCode = {responseCode}, responseMessage = {NormalizeLogValue(responseMessage)}, responseContentType = {NormalizeLogValue(responseContentType)}, responseBodyPreviewFirst300 = {NormalizeLogValue(responseBodyPreviewFirst300)}, responseEnvelopeShape = {NormalizeLogValue(responseEnvelopeShape)}, responseJsonTopLevelKeys = {NormalizeLogValue(responseJsonTopLevelKeys)}, responseCandidateUrlKeys = {NormalizeLogValue(responseCandidateUrlKeys)}, responseNestedPathTried = {NormalizeLogValue(responseNestedPathTried)}, responseUrlFieldRaw = {NormalizeLogValue(responseUrlFieldRaw)}, parsedPreviewUrl = {NormalizeLogValue(parsedPreviewUrl)}, matchedFieldPath = {NormalizeLogValue(matchedFieldPath)}, originalDataRaw = {NormalizeLogValue(originalDataRaw)}, decryptMode = {NormalizeLogValue(decryptMode)}, parsedProtocolType = {NormalizeLogValue(parsedProtocolType)}, previewFailureClassification = {NormalizeLogValue(previewFailureClassification)}, streamAcquireResult = {NormalizeLogValue(streamAcquireResult)}, playbackProbeResult = {NormalizeLogValue(playbackProbeResult)}, failureReason = {NormalizeLogValue(failureReason)}";
        MapPointSourceDiagnostics.Write("CTYunPreview", payload);
        MapPointSourceDiagnostics.Write(
            "InspectionPreviewStream",
            payload);
    }

    private static string NormalizeLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
    }

    private sealed record ProtocolAttemptPlanEntry(
        string Protocol,
        string MediaApiPath,
        bool UsesStreamSet);

    private sealed record CachedProtocolAttempt(
        PreviewResolution Resolution,
        DateTime ExpiresAt);

    private readonly record struct PreviewCandidate(
        string SourceUrl,
        string MediaKind,
        string DisplayName,
        int Priority);

    private readonly record struct PreviewResolution(
        bool IsSuccess,
        string PreviewUrl,
        string PreviewHostKind,
        string ResultSummary,
        string StreamUrlAcquireResult,
        int PlaybackAttemptCount,
        bool ProtocolFallbackUsed,
        string FinalPlaybackResult,
        InspectionPointFailureCategoryModel FailureCategory,
        string FailureReason,
        string PreviewFailureClassification)
    {
        public static PreviewResolution Succeeded(
            string previewUrl,
            string previewHostKind,
            string streamUrlAcquireResult,
            string finalPlaybackResult)
        {
            return new PreviewResolution(
                true,
                previewUrl,
                previewHostKind,
                finalPlaybackResult,
                streamUrlAcquireResult,
                1,
                false,
                finalPlaybackResult,
                InspectionPointFailureCategoryModel.PlaybackSucceeded,
                string.Empty,
                InspectionPreviewFailureClassifications.None);
        }

        public static PreviewResolution Failed(
            InspectionPointFailureCategoryModel failureCategory,
            string resultSummary,
            string streamUrlAcquireResult,
            int playbackAttemptCount,
            bool protocolFallbackUsed,
            string finalPlaybackResult,
            string failureReason,
            string previewFailureClassification = InspectionPreviewFailureClassifications.None)
        {
            return new PreviewResolution(
                false,
                string.Empty,
                string.Empty,
                resultSummary,
                streamUrlAcquireResult,
                playbackAttemptCount,
                protocolFallbackUsed,
                finalPlaybackResult,
                failureCategory,
                failureReason,
                previewFailureClassification);
        }
    }
}
