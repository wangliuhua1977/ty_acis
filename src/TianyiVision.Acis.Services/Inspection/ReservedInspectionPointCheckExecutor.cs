using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Integrations.Ctyun;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class ReservedInspectionPointCheckExecutor : IInspectionPointCheckExecutor
{
    private static readonly string PreviewRootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TianyiVision.Acis",
        "inspection",
        "preview-hosts");

    private readonly CtyunOpenPlatformClient? _openPlatformClient;

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
                "点位在线状态待确认，本轮暂不继续加载预览。",
                reservedSteps)
            {
                OnlineCheckResult = "在线状态待确认",
                FinalPlaybackResult = "在线检查失败"
            };
        }

        if (request.Policy.RequireOnlineStatusCheck && point.IsOnline == false)
        {
            return new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Failed,
                InspectionPointFailureCategoryModel.DeviceOffline,
                "点位离线，本轮未建立实时预览。",
                reservedSteps)
            {
                OnlineCheckResult = "在线检查未通过",
                FinalPlaybackResult = "在线检查失败"
            };
        }

        var previewResolution = await ResolvePreviewAsync(point.DeviceCode, cancellationToken).ConfigureAwait(false);
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
                FinalPlaybackResult = previewResolution.FinalPlaybackResult
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
            ScreenshotPlannedCount = request.PreviewOnly ? 0 : Math.Max(1, request.VideoInspection.ScreenshotCount),
            ScreenshotIntervalSeconds = request.PreviewOnly ? 0 : Math.Max(1, request.VideoInspection.ScreenshotIntervalSeconds),
            EvidenceCaptureState = request.PreviewOnly
                ? InspectionEvidenceValueKeys.CaptureStateNone
                : InspectionEvidenceValueKeys.CaptureStatePending,
            EvidenceSummary = request.PreviewOnly ? string.Empty : "待截图取证",
            EvidenceRetentionMode = request.PreviewOnly ? string.Empty : request.VideoInspection.EvidenceRetentionMode,
            EvidenceRetentionDays = request.PreviewOnly ? 0 : Math.Max(0, request.VideoInspection.EvidenceRetentionDays),
            AllowManualSupplementScreenshot = !request.PreviewOnly && request.VideoInspection.AllowManualSupplementScreenshot
        };
    }

    private async Task<PreviewResolution> ResolvePreviewAsync(string deviceCode, CancellationToken cancellationToken)
    {
        if (_openPlatformClient is null)
        {
            return PreviewResolution.Failed(
                InspectionPointFailureCategoryModel.NoStreamAddress,
                "当前环境未接入实时预览能力。",
                "未接入实时预览能力",
                0,
                false,
                "无流地址");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var attempts = 0;

        attempts++;
        var hlsResponse = _openPlatformClient.GetPreviewMediaUrl(deviceCode, "/open/token/cloud/getDeviceMediaUrlHls");
        if (TryBuildHostedPreview(deviceCode, "hls", hlsResponse, out var hlsPreview))
        {
            return hlsPreview with
            {
                PlaybackAttemptCount = attempts,
                ProtocolFallbackUsed = false
            };
        }

        attempts++;
        var flvResponse = _openPlatformClient.GetPreviewMediaUrl(deviceCode, "/open/token/cloud/getDeviceMediaUrlFlv");
        if (TryBuildHostedPreview(deviceCode, "flv", flvResponse, out var flvPreview))
        {
            return flvPreview with
            {
                PlaybackAttemptCount = attempts,
                ProtocolFallbackUsed = true
            };
        }

        attempts++;
        var h5Response = _openPlatformClient.GetH5PreviewStreamSet(deviceCode);
        var h5Summary = h5Response.IsSuccess && h5Response.Data.StreamUrls.Count > 0
            ? "已获取点位流地址，但当前宿主暂不直接承载该协议。"
            : "当前点位未返回可直接预览的视频流。";

        return PreviewResolution.Failed(
            h5Response.IsSuccess
                ? InspectionPointFailureCategoryModel.ProtocolFallbackStillFailed
                : InspectionPointFailureCategoryModel.NoStreamAddress,
            h5Summary,
            BuildStreamAcquireResult(hlsResponse, flvResponse, h5Response),
            attempts,
            true,
            h5Response.IsSuccess ? "协议切换后仍失败" : "无流地址");
    }

    private static bool TryBuildHostedPreview(
        string deviceCode,
        string mediaKind,
        ServiceResponse<CtyunPreviewMediaUrlDto> response,
        out PreviewResolution preview)
    {
        preview = default;
        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Data.Url))
        {
            return false;
        }

        var previewUri = BuildPreviewHostUri(deviceCode, mediaKind, response.Data.Url);
        preview = PreviewResolution.Succeeded(
            previewUri.AbsoluteUri,
            mediaKind,
            $"{mediaKind.ToUpperInvariant()} 实时预览已建立",
            "播放成功");
        return true;
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
        var script = mediaKind switch
        {
            "flv" => $$"""
                <script src="https://cdn.jsdelivr.net/npm/flv.js@latest/dist/flv.min.js"></script>
                <script>
                const video = document.getElementById('player');
                const sourceUrl = '{{encodedUrl}}';
                if (window.flvjs && flvjs.isSupported()) {
                  const player = flvjs.createPlayer({ type: 'flv', url: sourceUrl });
                  player.attachMediaElement(video);
                  player.load();
                  player.play().catch(() => {});
                } else {
                  document.body.setAttribute('data-state', 'error');
                }
                </script>
                """,
            _ => $$"""
                <script src="https://cdn.jsdelivr.net/npm/hls.js@latest"></script>
                <script>
                const video = document.getElementById('player');
                const sourceUrl = '{{encodedUrl}}';
                if (video.canPlayType('application/vnd.apple.mpegurl')) {
                  video.src = sourceUrl;
                  video.play().catch(() => {});
                } else if (window.Hls && Hls.isSupported()) {
                  const hls = new Hls();
                  hls.loadSource(sourceUrl);
                  hls.attachMedia(video);
                  video.play().catch(() => {});
                } else {
                  document.body.setAttribute('data-state', 'error');
                }
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
              {{script}}
            </body>
            </html>
            """;
    }

    private static string BuildStreamAcquireResult(
        ServiceResponse<CtyunPreviewMediaUrlDto> hlsResponse,
        ServiceResponse<CtyunPreviewMediaUrlDto> flvResponse,
        ServiceResponse<CtyunPreviewStreamSetDto> h5Response)
    {
        if (hlsResponse.IsSuccess && !string.IsNullOrWhiteSpace(hlsResponse.Data.Url))
        {
            return "已获取 HLS 流地址";
        }

        if (flvResponse.IsSuccess && !string.IsNullOrWhiteSpace(flvResponse.Data.Url))
        {
            return "已获取 FLV 流地址";
        }

        if (h5Response.IsSuccess && h5Response.Data.StreamUrls.Count > 0)
        {
            return "已获取备选流地址";
        }

        return "未获取到可用流地址";
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
                    ? "当前已启用协议切换重试，会在 HLS 与 FLV 之间降级重试。"
                    : "当前未启用协议切换重试。"),
            new(
                InspectionExecutionReservationStepModel.AutoScreenshot,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 自动截图",
                request.PreviewOnly
                    ? "选中点位仅建立实时预览，本轮不触发截图取证。"
                    : $"当前计划自动截图 {request.VideoInspection.ScreenshotCount} 张。"),
            new(
                InspectionExecutionReservationStepModel.ManualSupplementScreenshot,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 人工补截图",
                request.PreviewOnly || !request.VideoInspection.AllowManualSupplementScreenshot
                    ? "本轮未启用人工补截图。"
                    : "当前允许人工补截图。"),
            new(
                InspectionExecutionReservationStepModel.AiDecision,
                "IInspectionPointCheckExecutor.ExecuteAsync -> AI 判定",
                request.PreviewOnly
                    ? "选中点位预览不触发 AI 判定。"
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

    private readonly record struct PreviewResolution(
        bool IsSuccess,
        string PreviewUrl,
        string PreviewHostKind,
        string ResultSummary,
        string StreamUrlAcquireResult,
        int PlaybackAttemptCount,
        bool ProtocolFallbackUsed,
        string FinalPlaybackResult,
        InspectionPointFailureCategoryModel FailureCategory)
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
                InspectionPointFailureCategoryModel.PlaybackSucceeded);
        }

        public static PreviewResolution Failed(
            InspectionPointFailureCategoryModel failureCategory,
            string resultSummary,
            string streamUrlAcquireResult,
            int playbackAttemptCount,
            bool protocolFallbackUsed,
            string finalPlaybackResult)
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
                failureCategory);
        }
    }
}
