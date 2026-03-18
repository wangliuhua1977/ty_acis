using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Inspection;
using TianyiVision.Acis.UI.States;
using TianyiVision.Acis.UI.ViewModels;
using TianyiVision.Acis.UI.Views.Controls;

namespace TianyiVision.Acis.UI.Views.Pages;

public partial class InspectionPageView : UserControl
{
    private InspectionPageViewModel? _viewModel;
    private CancellationTokenSource? _evidenceCaptureCts;
    private CancellationTokenSource? _previewNavigationCts;
    private string _activeEvidenceKey = string.Empty;
    private string _activePreviewUri = string.Empty;
    private string _activePreviewProbeFingerprint = string.Empty;

    public InspectionPageView()
    {
        InitializeComponent();
        RealMapHost.AvailabilityChanged += RealMapHost_OnAvailabilityChanged;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is InspectionPageViewModel viewModel)
        {
            AttachViewModel(viewModel);
            _ = UpdatePreviewHostAsync(viewModel.SelectedPointDetail);
            ScheduleEvidenceCapture(viewModel.SelectedPointDetail);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
        CancelEvidenceCapture();
        CancelPreviewNavigation();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();

        if (e.NewValue is InspectionPageViewModel viewModel)
        {
            AttachViewModel(viewModel);
            _ = UpdatePreviewHostAsync(viewModel.SelectedPointDetail);
            ScheduleEvidenceCapture(viewModel.SelectedPointDetail);
        }
    }

    private void AttachViewModel(InspectionPageViewModel viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(InspectionPageViewModel.SelectedPointDetail), StringComparison.Ordinal))
        {
            return;
        }

        _ = UpdatePreviewHostAsync(_viewModel?.SelectedPointDetail);
        ScheduleEvidenceCapture(_viewModel?.SelectedPointDetail);
    }

    private void CancelPreviewNavigation()
    {
        if (_previewNavigationCts is null)
        {
            return;
        }

        _previewNavigationCts.Cancel();
        _previewNavigationCts.Dispose();
        _previewNavigationCts = null;
    }

    private async Task UpdatePreviewHostAsync(InspectionPointDetailState? detail)
    {
        CancelPreviewNavigation();
        _previewNavigationCts = new CancellationTokenSource();
        var cancellationToken = _previewNavigationCts.Token;

        try
        {
            var previewUri = detail?.IsPreviewAvailable == true ? detail.PreviewHostUri : null;
            if (previewUri is null)
            {
                _activePreviewUri = string.Empty;
                _activePreviewProbeFingerprint = string.Empty;
                await Dispatcher.InvokeAsync(() => PreviewWebView.Source = new Uri("about:blank"));
                return;
            }

            if (string.Equals(_activePreviewUri, previewUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                WritePreviewHostLog(
                    detail!,
                    detail!.PreviewHostKind,
                    detail.StreamUrlAcquireResult,
                    "navigation_skipped_same_url",
                    "reuse_existing_host",
                    "none",
                    "none",
                    previewUri.AbsoluteUri,
                    previewUri.AbsoluteUri);
                return;
            }

            await PreviewWebView.EnsureCoreWebView2Async();
            AttachPreviewProbeHandlers();
            cancellationToken.ThrowIfCancellationRequested();

            WritePreviewHostLog(
                detail!,
                detail!.PreviewHostKind,
                detail.StreamUrlAcquireResult,
                "navigation_requested",
                "probe_pending",
                "none",
                "none",
                previewUri.AbsoluteUri,
                previewUri.AbsoluteUri);
            await Dispatcher.InvokeAsync(() => PreviewWebView.Source = previewUri);
            _activePreviewUri = previewUri.AbsoluteUri;
            _activePreviewProbeFingerprint = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionPreviewHost",
                $"navigationResult = navigation_failed, failureCategory = navigation_failed, previewFailureClassification = {InspectionPreviewFailureClassifications.NavigationFailed}, failureReason = {NormalizeLogValue(ex.Message)}, actualLoadUrl = {NormalizeLogValue(detail?.PreviewHostUri?.AbsoluteUri)}");
        }
    }

    private void AttachPreviewProbeHandlers()
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        PreviewWebView.CoreWebView2.WebMessageReceived -= OnPreviewWebMessageReceived;
        PreviewWebView.CoreWebView2.WebMessageReceived += OnPreviewWebMessageReceived;
        PreviewWebView.CoreWebView2.NavigationCompleted -= OnPreviewNavigationCompleted;
        PreviewWebView.CoreWebView2.NavigationCompleted += OnPreviewNavigationCompleted;
    }

    private void OnPreviewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var detail = _viewModel?.SelectedPointDetail;
        if (detail is null)
        {
            return;
        }

        var failureReason = e.IsSuccess
            ? "none"
            : $"navigation_error:{e.WebErrorStatus}";
        var previewFailureClassification = e.IsSuccess
            ? InspectionPreviewFailureClassifications.None
            : InspectionPreviewFailureClassifications.NavigationFailed;
        WritePreviewHostLog(
            detail,
            detail.PreviewHostKind,
            detail.StreamUrlAcquireResult,
            e.IsSuccess ? "navigation_completed" : "navigation_failed",
            e.IsSuccess ? "probe_pending" : "probe_not_started",
            e.IsSuccess ? "none" : "navigation_failed",
            failureReason,
            detail.PreviewHostUri?.AbsoluteUri ?? string.Empty,
            detail.PreviewHostUri?.AbsoluteUri ?? string.Empty,
            previewFailureClassification);

        if (!e.IsSuccess)
        {
            WritePreviewProjectionLog(
                detail,
                detail.PreviewHostKind,
                "probe_not_started",
                previewFailureClassification,
                failureReason,
                detail.PreviewHostUri?.AbsoluteUri ?? string.Empty,
                detail.PreviewHostUri?.AbsoluteUri ?? string.Empty);
            _viewModel?.ApplyPreviewHostFailure(
                detail.PointId,
                detail.PreviewHostKind,
                previewFailureClassification,
                "播放失败",
                "已获取流地址但播放失败");
        }
    }

    private void OnPreviewWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var detail = _viewModel?.SelectedPointDetail;
        if (detail is null)
        {
            return;
        }

        try
        {
            using var document = ParsePreviewProbeMessage(e.WebMessageAsJson);
            if (document is null)
            {
                return;
            }

            var root = document.RootElement;
            if (!root.TryGetProperty("kind", out var kindElement)
                || !string.Equals(kindElement.GetString(), "previewProbe", StringComparison.Ordinal))
            {
                return;
            }

            var state = root.TryGetProperty("state", out var stateElement)
                ? stateElement.GetString() ?? string.Empty
                : string.Empty;
            var probeDetail = root.TryGetProperty("detail", out var detailElement)
                ? detailElement.GetString() ?? string.Empty
                : string.Empty;
            var protocol = root.TryGetProperty("protocol", out var protocolElement)
                ? protocolElement.GetString() ?? string.Empty
                : detail.PreviewHostKind;
            var sourceUrl = root.TryGetProperty("sourceUrl", out var sourceUrlElement)
                ? sourceUrlElement.GetString() ?? string.Empty
                : string.Empty;
            var fingerprint = $"{detail.PointId}|{state}|{probeDetail}|{protocol}|{sourceUrl}";
            if (string.Equals(_activePreviewProbeFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return;
            }

            _activePreviewProbeFingerprint = fingerprint;
            var failureCategory = state switch
            {
                "playback_failed" => "player_load_failed",
                "playback_warning" => "player_warning",
                "playback_stalled" => "playback_stalled",
                _ => "none"
            };
            var previewFailureClassification = ResolvePreviewFailureClassification(state, probeDetail);
            WritePreviewHostLog(
                detail,
                string.IsNullOrWhiteSpace(protocol) ? detail.PreviewHostKind : protocol,
                detail.StreamUrlAcquireResult,
                "navigation_completed",
                state,
                failureCategory,
                string.IsNullOrWhiteSpace(probeDetail) ? "none" : probeDetail,
                sourceUrl,
                detail.PreviewHostUri?.AbsoluteUri ?? sourceUrl,
                previewFailureClassification);

            if (IsFinalPreviewFailureClassification(previewFailureClassification))
            {
                WritePreviewProjectionLog(
                    detail,
                    string.IsNullOrWhiteSpace(protocol) ? detail.PreviewHostKind : protocol,
                    state,
                    previewFailureClassification,
                    string.IsNullOrWhiteSpace(probeDetail) ? "none" : probeDetail,
                    sourceUrl,
                    detail.PreviewHostUri?.AbsoluteUri ?? sourceUrl);
                _viewModel?.ApplyPreviewHostFailure(
                    detail.PointId,
                    string.IsNullOrWhiteSpace(protocol) ? detail.PreviewHostKind : protocol,
                    previewFailureClassification,
                    "播放失败",
                    "已获取流地址但播放失败");
            }
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionPreviewHost",
                $"navigationResult = navigation_completed, playbackProbeResult = probe_message_parse_failed, failureCategory = player_load_failed, previewFailureClassification = {InspectionPreviewFailureClassifications.PlayerLoadFailed}, failureReason = {NormalizeLogValue(ex.Message)}, actualLoadUrl = {NormalizeLogValue(detail?.PreviewHostUri?.AbsoluteUri)}");
        }
    }

    private static JsonDocument? ParsePreviewProbeMessage(string messageJson)
    {
        if (string.IsNullOrWhiteSpace(messageJson))
        {
            return null;
        }

        var document = JsonDocument.Parse(messageJson);
        if (document.RootElement.ValueKind != JsonValueKind.String)
        {
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document
                : DisposeAndReturnNull(document);
        }

        var nestedJson = document.RootElement.GetString();
        document.Dispose();
        if (string.IsNullOrWhiteSpace(nestedJson))
        {
            return null;
        }

        var nestedDocument = JsonDocument.Parse(nestedJson);
        return nestedDocument.RootElement.ValueKind == JsonValueKind.Object
            ? nestedDocument
            : DisposeAndReturnNull(nestedDocument);
    }

    private static JsonDocument? DisposeAndReturnNull(JsonDocument document)
    {
        document.Dispose();
        return null;
    }

    private static void WritePreviewHostLog(
        InspectionPointDetailState detail,
        string selectedProtocol,
        string streamAcquireResult,
        string navigationResult,
        string playbackProbeResult,
        string failureCategory,
        string failureReason,
        string parsedPreviewUrl,
        string actualLoadUrl,
        string previewFailureClassification = InspectionPreviewFailureClassifications.None)
    {
        var attemptedProtocols = detail.ProtocolFallbackUsed
            ? $"fallback>{NormalizeLogValue(selectedProtocol)}"
            : NormalizeLogValue(selectedProtocol);
        var payload = $"pointId = {NormalizeLogValue(detail.PointId)}, deviceCode = {NormalizeLogValue(detail.DeviceCode)}, selectedProtocol = {NormalizeLogValue(selectedProtocol)}, attemptedProtocols = {attemptedProtocols}, streamAcquireResult = {NormalizeLogValue(streamAcquireResult)}, navigationResult = {NormalizeLogValue(navigationResult)}, playbackProbeResult = {NormalizeLogValue(playbackProbeResult)}, failureCategory = {NormalizeLogValue(failureCategory)}, previewFailureClassification = {NormalizeLogValue(previewFailureClassification)}, failureReason = {NormalizeLogValue(failureReason)}, parsedPreviewUrl = {NormalizeLogValue(parsedPreviewUrl)}, actualLoadUrl = {NormalizeLogValue(actualLoadUrl)}";
        MapPointSourceDiagnostics.Write("InspectionPreviewHost", payload);
        MapPointSourceDiagnostics.Write("InspectionPreviewProbe", payload);
    }

    private static void WritePreviewProjectionLog(
        InspectionPointDetailState detail,
        string selectedProtocol,
        string playbackProbeResult,
        string previewFailureClassification,
        string failureReason,
        string parsedPreviewUrl,
        string actualLoadUrl)
    {
        var attemptedProtocols = detail.ProtocolFallbackUsed
            ? $"fallback>{NormalizeLogValue(selectedProtocol)}"
            : NormalizeLogValue(selectedProtocol);
        var payload = $"pointId = {NormalizeLogValue(detail.PointId)}, deviceCode = {NormalizeLogValue(detail.DeviceCode)}, selectedProtocol = {NormalizeLogValue(selectedProtocol)}, attemptedProtocols = {attemptedProtocols}, streamAcquireResult = {NormalizeLogValue(detail.StreamUrlAcquireResult)}, playbackProbeResult = {NormalizeLogValue(playbackProbeResult)}, previewFailureClassification = {NormalizeLogValue(previewFailureClassification)}, finalPlaybackResult = 播放失败, resultSummary = 已获取流地址但播放失败, failureReason = {NormalizeLogValue(failureReason)}, parsedPreviewUrl = {NormalizeLogValue(parsedPreviewUrl)}, actualLoadUrl = {NormalizeLogValue(actualLoadUrl)}";
        MapPointSourceDiagnostics.Write("InspectionPreviewStream", payload);
    }

    private static bool IsFinalPreviewFailureClassification(string previewFailureClassification)
    {
        return !string.IsNullOrWhiteSpace(previewFailureClassification)
            && !string.Equals(previewFailureClassification, InspectionPreviewFailureClassifications.None, StringComparison.Ordinal);
    }

    private static string ResolvePreviewFailureClassification(string playbackProbeResult, string failureReason)
    {
        return playbackProbeResult switch
        {
            "playback_stalled" => InspectionPreviewFailureClassifications.PlaybackStalled,
            "playback_failed" when failureReason.Contains("not_supported", StringComparison.OrdinalIgnoreCase)
                => InspectionPreviewFailureClassifications.PlayerProtocolNotSupported,
            "playback_failed" => InspectionPreviewFailureClassifications.PlayerLoadFailed,
            _ => InspectionPreviewFailureClassifications.None
        };
    }

    private static string NormalizeLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
    }

    private void ScheduleEvidenceCapture(InspectionPointDetailState? detail)
    {
        if (detail is null
            || string.IsNullOrWhiteSpace(detail.TaskId)
            || string.IsNullOrWhiteSpace(detail.PointId)
            || string.IsNullOrWhiteSpace(detail.DeviceCode)
            || !string.Equals(detail.EvidenceCaptureState, InspectionEvidenceValueKeys.CaptureStatePending, StringComparison.Ordinal))
        {
            _activeEvidenceKey = string.Empty;
            CancelEvidenceCapture();
            return;
        }

        var evidenceKey = $"{detail.TaskId}|{detail.PointId}|{detail.EvidenceCaptureState}|{detail.ScreenshotSuccessCount}";
        if (string.Equals(_activeEvidenceKey, evidenceKey, StringComparison.Ordinal))
        {
            return;
        }

        _activeEvidenceKey = evidenceKey;
        CancelEvidenceCapture();
        _evidenceCaptureCts = new CancellationTokenSource();

        if (detail.IsPreviewAvailable && detail.PreviewHostUri is not null)
        {
            _ = CapturePreviewEvidenceAsync(detail, _evidenceCaptureCts.Token);
            return;
        }

        _ = CaptureFailureEvidenceAsync(detail, _evidenceCaptureCts.Token);
    }

    private void CancelEvidenceCapture()
    {
        if (_evidenceCaptureCts is null)
        {
            return;
        }

        _evidenceCaptureCts.Cancel();
        _evidenceCaptureCts.Dispose();
        _evidenceCaptureCts = null;
    }

    private async Task CapturePreviewEvidenceAsync(InspectionPointDetailState detail, CancellationToken cancellationToken)
    {
        try
        {
            if (!await WaitForPreviewReadyAsync(detail.PreviewHostUri!, cancellationToken))
            {
                WriteEvidence(
                    detail,
                    [],
                    0,
                    InspectionEvidenceValueKeys.CaptureStateFailed,
                    "截图取证失败，当前保留人工补充入口。");
                return;
            }

            var plannedCount = Math.Max(1, detail.ScreenshotPlannedCount);
            var evidenceItems = new List<InspectionPointEvidenceMetadataModel>(plannedCount);

            for (var index = 0; index < plannedCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var captureTime = DateTime.Now;
                var outputPath = InspectionEvidencePathBuilder.BuildPlaybackScreenshotPath(detail.TaskId, detail.PointId, index + 1, captureTime);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                await PreviewWebView.EnsureCoreWebView2Async();
                if (PreviewWebView.CoreWebView2 is null)
                {
                    continue;
                }

                await using var stream = File.Create(outputPath);
                await PreviewWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                await stream.FlushAsync(cancellationToken);

                evidenceItems.Add(new InspectionPointEvidenceMetadataModel(
                    detail.TaskId,
                    detail.PointId,
                    detail.DeviceCode,
                    captureTime,
                    outputPath,
                    $"播放截图证据 {index + 1}/{plannedCount}")
                {
                    EvidenceKind = InspectionEvidenceValueKeys.EvidenceKindPlaybackScreenshot,
                    EvidenceSource = InspectionEvidenceValueKeys.EvidenceSourcePreviewHost,
                    AiAnalysisStatus = InspectionEvidenceValueKeys.AiAnalysisPending,
                    AiAnalysisSummary = "待下一轮 AI 画面分析。"
                });

                if (index < plannedCount - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, detail.ScreenshotIntervalSeconds)), cancellationToken);
                }
            }

            WriteEvidence(
                detail,
                evidenceItems,
                evidenceItems.Count,
                InspectionEvidenceValueKeys.CaptureStateCompleted,
                $"已生成截图证据 {evidenceItems.Count}/{plannedCount} 张。");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionEvidence",
                $"preview capture failed: taskId={detail.TaskId}, pointId={detail.PointId}, reason={ex.Message}");
            WriteEvidence(
                detail,
                [],
                0,
                InspectionEvidenceValueKeys.CaptureStateFailed,
                "截图取证失败，当前保留人工补充入口。");
        }
    }

    private async Task CaptureFailureEvidenceAsync(InspectionPointDetailState detail, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Dispatcher.InvokeAsync(() =>
            {
                UpdateLayout();
                PreviewFailureCard.UpdateLayout();
            });

            var captureTime = DateTime.Now;
            var outputPath = InspectionEvidencePathBuilder.BuildFailureSnapshotPath(detail.TaskId, detail.PointId, captureTime);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var saved = await Dispatcher.InvokeAsync(() => SaveFailureSnapshot(outputPath));
            if (!saved)
            {
                WriteEvidence(
                    detail,
                    [],
                    0,
                    InspectionEvidenceValueKeys.CaptureStateFailed,
                    "失败证据生成失败，当前保留人工补充入口。");
                return;
            }

            var evidenceItem = new InspectionPointEvidenceMetadataModel(
                detail.TaskId,
                detail.PointId,
                detail.DeviceCode,
                captureTime,
                outputPath,
                "播放失败说明页快照")
            {
                EvidenceKind = InspectionEvidenceValueKeys.EvidenceKindFailureSnapshot,
                EvidenceSource = InspectionEvidenceValueKeys.EvidenceSourceFailureCard,
                AiAnalysisStatus = InspectionEvidenceValueKeys.AiAnalysisPending,
                AiAnalysisSummary = "待下一轮 AI 画面分析。"
            };

            WriteEvidence(
                detail,
                [evidenceItem],
                1,
                InspectionEvidenceValueKeys.CaptureStateCompleted,
                "已生成失败证据：播放失败说明页快照。");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionEvidence",
                $"failure evidence capture failed: taskId={detail.TaskId}, pointId={detail.PointId}, reason={ex.Message}");
            WriteEvidence(
                detail,
                [],
                0,
                InspectionEvidenceValueKeys.CaptureStateFailed,
                "失败证据生成失败，当前保留人工补充入口。");
        }
    }

    private void WriteEvidence(
        InspectionPointDetailState detail,
        IReadOnlyList<InspectionPointEvidenceMetadataModel> evidenceItems,
        int screenshotSuccessCount,
        string captureState,
        string summary)
    {
        if (_viewModel is null)
        {
            return;
        }

        var success = _viewModel.TryWritePointEvidence(new InspectionPointEvidenceWriteRequest(
            detail.TaskId,
            detail.PointId,
            detail.DeviceCode,
            Math.Max(1, detail.ScreenshotPlannedCount),
            screenshotSuccessCount,
            captureState,
            summary,
            detail.EvidenceRetentionMode,
            detail.EvidenceRetentionDays,
            detail.AllowManualSupplementScreenshot,
            evidenceItems)
        {
            AiAnalysisStatus = InspectionEvidenceValueKeys.AiAnalysisPending,
            AiAnalysisSummary = "待下一轮 AI 画面分析。"
        });

        if (!success)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionEvidence",
                $"evidence write failed: taskId={detail.TaskId}, pointId={detail.PointId}");
        }
    }

    private async Task<bool> WaitForPreviewReadyAsync(Uri previewUri, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Dispatcher.InvokeAsync(UpdateLayout);

            if (PreviewWebView.Source is not null
                && string.Equals(PreviewWebView.Source.AbsoluteUri, previewUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                await PreviewWebView.EnsureCoreWebView2Async();
                if (PreviewWebView.CoreWebView2 is not null)
                {
                    await Task.Delay(1500, cancellationToken);
                    return true;
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        return false;
    }

    private bool SaveFailureSnapshot(string outputPath)
    {
        if (PreviewFailureCard.ActualWidth <= 0 || PreviewFailureCard.ActualHeight <= 0)
        {
            return false;
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(PreviewFailureCard.ActualWidth),
            (int)Math.Ceiling(PreviewFailureCard.ActualHeight),
            96,
            96,
            System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(PreviewFailureCard);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(outputPath);
        encoder.Save(stream);
        return true;
    }

    private void RealMapHost_OnAvailabilityChanged(object? sender, MapAvailabilityChangedEventArgs e)
    {
        FallbackMapLayer.Visibility = e.IsAvailable ? Visibility.Collapsed : Visibility.Visible;

        if (DataContext is InspectionPageViewModel viewModel)
        {
            viewModel.UpdateMapAvailability(e.IsAvailable);
        }
    }
}
