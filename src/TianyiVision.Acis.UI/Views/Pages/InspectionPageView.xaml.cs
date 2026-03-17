using System.ComponentModel;
using System.IO;
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
                await Dispatcher.InvokeAsync(() => PreviewWebView.Source = new Uri("about:blank"));
                return;
            }

            if (string.Equals(_activePreviewUri, previewUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await PreviewWebView.EnsureCoreWebView2Async();
            cancellationToken.ThrowIfCancellationRequested();

            await Dispatcher.InvokeAsync(() => PreviewWebView.Source = previewUri);
            _activePreviewUri = previewUri.AbsoluteUri;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "InspectionPreview",
                $"preview host navigation failed: reason={ex.Message}");
        }
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
