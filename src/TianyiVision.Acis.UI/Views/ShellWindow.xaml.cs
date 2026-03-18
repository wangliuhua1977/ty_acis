using System.IO;
using System.Windows;
using System.Windows.Media;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.UI.States;
using TianyiVision.Acis.UI.ViewModels;
using TianyiVision.Acis.UI.Views.Controls;
using TianyiVision.Acis.UI.Views.Pages;

namespace TianyiVision.Acis.UI.Views;

public partial class ShellWindow : Window
{
    private static readonly string[] AcceptancePreferredDeviceCodes =
    [
        "51110209021322000002",
        "3KSCP284337JYTK",
        "3KSCP29354HL56W",
        "51110201021322000020",
        "51110200441324008961"
    ];

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel viewModel)
        {
            return;
        }

        var exportDirectory = Environment.GetEnvironmentVariable("ACIS_EXPORT_SCREENSHOTS_DIR");
        if (!string.IsNullOrWhiteSpace(exportDirectory))
        {
            await ExportMapScreenshotsAsync(viewModel, exportDirectory);
        }

        if (string.Equals(Environment.GetEnvironmentVariable("ACIS_RUNTIME_ACCEPTANCE_MODE"), "1", StringComparison.Ordinal))
        {
            await RunRuntimeAcceptanceAsync(viewModel);
        }
    }

    private async Task ExportMapScreenshotsAsync(ShellViewModel viewModel, string exportDirectory)
    {
        var statusPath = Path.Combine(exportDirectory, "export-status.txt");

        try
        {
            Directory.CreateDirectory(exportDirectory);
            File.AppendAllText(statusPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] export started{Environment.NewLine}");

            var homePath = Path.Combine(exportDirectory, "home-map.png");
            var homeCaptured = await CaptureMapAsync(homePath, typeof(HomePageViewModel));
            File.AppendAllText(statusPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] home captured={homeCaptured}{Environment.NewLine}");

            var inspectionItem = viewModel.NavigationItems.FirstOrDefault(item => item.SectionId == AppSectionId.Inspection);
            inspectionItem?.SelectCommand.Execute(null);
            await WaitForPageAsync(typeof(InspectionPageViewModel));

            var inspectionPath = Path.Combine(exportDirectory, "inspection-map.png");
            var inspectionCaptured = await CaptureMapAsync(inspectionPath, typeof(InspectionPageViewModel));
            File.AppendAllText(statusPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] inspection captured={inspectionCaptured}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            // Screenshot export is diagnostics-only and must never affect app startup.
            File.AppendAllText(statusPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] export failed: {ex.Message}{Environment.NewLine}");
        }
    }

    private async Task RunRuntimeAcceptanceAsync(ShellViewModel viewModel)
    {
        var autoClose = !string.Equals(
            Environment.GetEnvironmentVariable("ACIS_RUNTIME_ACCEPTANCE_AUTO_CLOSE"),
            "0",
            StringComparison.Ordinal);

        try
        {
            MapPointSourceDiagnostics.Write(
                "AcceptanceRun",
                $"runtime acceptance started: logFile = {MapPointSourceDiagnostics.LogFilePath}");

            var inspectionItem = viewModel.NavigationItems.FirstOrDefault(item => item.SectionId == AppSectionId.Inspection);
            inspectionItem?.SelectCommand.Execute(null);
            await WaitForPageAsync(typeof(InspectionPageViewModel));
            await Task.Delay(1500);

            if (viewModel.CurrentPageViewModel is not InspectionPageViewModel inspectionViewModel)
            {
                MapPointSourceDiagnostics.Write("AcceptanceRun", "runtime acceptance aborted: inspection page view model unavailable");
                return;
            }

            if (!await WaitForInspectionPointsAsync(inspectionViewModel))
            {
                MapPointSourceDiagnostics.Write("AcceptanceRun", "runtime acceptance aborted: inspection points not ready");
                return;
            }

            var firstPoint = ResolvePreferredAcceptancePoint(inspectionViewModel.Points);
            var secondPoint = ResolveSecondAcceptancePoint(inspectionViewModel.Points, firstPoint);

            if (firstPoint is null)
            {
                MapPointSourceDiagnostics.Write("AcceptanceRun", "runtime acceptance aborted: no inspection points available");
                return;
            }

            MapPointSourceDiagnostics.Write(
                "AcceptanceRun",
                $"inspection page ready: totalPoints = {inspectionViewModel.Points.Count}, firstPoint = {firstPoint.DeviceCode}, secondPoint = {secondPoint?.DeviceCode ?? "none"}");

            await SelectPointForAcceptanceAsync(inspectionViewModel, firstPoint, "first_point");

            if (secondPoint is not null)
            {
                await SelectPointForAcceptanceAsync(inspectionViewModel, secondPoint, "switch_point");
            }

            var singlePoint = secondPoint ?? firstPoint;
            await SelectPointForAcceptanceAsync(inspectionViewModel, singlePoint, "single_point");
            await RunSinglePointInspectionAsync(inspectionViewModel, singlePoint);

            MapPointSourceDiagnostics.Write(
                "AcceptanceRun",
                $"runtime acceptance completed: selectedPoint = {singlePoint.DeviceCode}, inspectionStatus = {inspectionViewModel.SinglePointInspectionTaskStatus}, inspectionSummary = {inspectionViewModel.SinglePointInspectionResultSummary}");
        }
        catch (Exception ex)
        {
            MapPointSourceDiagnostics.Write(
                "AcceptanceRun",
                $"runtime acceptance failed: type = {ex.GetType().Name}, message = {ex.Message}");
        }
        finally
        {
            if (autoClose)
            {
                await Task.Delay(1000);
                Close();
            }
        }
    }

    private async Task<bool> CaptureMapAsync(string outputPath, Type expectedPageViewModelType)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            UpdateLayout();

            if (DataContext is not ShellViewModel viewModel
                || viewModel.CurrentPageViewModel.GetType() != expectedPageViewModelType)
            {
                await Task.Delay(300);
                continue;
            }

            var mapHost = FindDescendant<RealMapHost>(CurrentPageHost);
            if (mapHost is not null
                && HasRenderablePoints(mapHost)
                && await mapHost.CapturePreviewAsync(outputPath))
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private async Task<bool> WaitForInspectionPointsAsync(InspectionPageViewModel viewModel)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (viewModel.Points.Count > 0)
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private async Task SelectPointForAcceptanceAsync(
        InspectionPageViewModel viewModel,
        InspectionPointState point,
        string actionName)
    {
        MapPointSourceDiagnostics.Write(
            "AcceptanceRun",
            $"select point: action = {actionName}, pointId = {point.Id}, deviceCode = {point.DeviceCode}, pointName = {point.Name}");

        viewModel.SelectPointCommand.Execute(point);
        await WaitForPointPreviewAsync(viewModel, point.Id);
    }

    private async Task WaitForPointPreviewAsync(InspectionPageViewModel viewModel, string pointId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var detail = viewModel.SelectedPointDetail;
            if (viewModel.SelectedPoint?.Id == pointId
                && detail?.PointId == pointId
                && (!string.IsNullOrWhiteSpace(detail.StreamUrlAcquireResult)
                    || !string.IsNullOrWhiteSpace(detail.FinalPlaybackResult)
                    || detail.PreviewHostUri is not null))
            {
                MapPointSourceDiagnostics.Write(
                    "AcceptanceRun",
                    $"preview prepared: pointId = {pointId}, streamAcquireResult = {detail.StreamUrlAcquireResult}, finalPlaybackResult = {detail.FinalPlaybackResult}, previewHostUri = {detail.PreviewHostUri?.AbsoluteUri ?? "none"}");
                await Task.Delay(detail.PreviewHostUri is null ? 2500 : 8000);
                return;
            }

            await Task.Delay(500);
        }

        MapPointSourceDiagnostics.Write(
            "AcceptanceRun",
            $"preview wait timeout: pointId = {pointId}, selectedPoint = {viewModel.SelectedPoint?.Id ?? "none"}");
    }

    private async Task RunSinglePointInspectionAsync(InspectionPageViewModel viewModel, InspectionPointState point)
    {
        if (!viewModel.StartSinglePointInspectionCommand.CanExecute(null))
        {
            MapPointSourceDiagnostics.Write(
                "AcceptanceRun",
                $"single inspection skipped: pointId = {point.Id}, deviceCode = {point.DeviceCode}, reason = command_disabled");
            return;
        }

        MapPointSourceDiagnostics.Write(
            "AcceptanceRun",
            $"single inspection started: pointId = {point.Id}, deviceCode = {point.DeviceCode}");

        var pendingSummary = viewModel.SinglePointInspectionResultSummary;
        viewModel.StartSinglePointInspectionCommand.Execute(null);

        for (var attempt = 0; attempt < 80; attempt++)
        {
            if (!string.Equals(viewModel.SinglePointInspectionResultSummary, pendingSummary, StringComparison.Ordinal)
                && !viewModel.SinglePointInspectionTaskStatus.Contains("运行", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await Task.Delay(1000);
        }

        await Task.Delay(5000);
        MapPointSourceDiagnostics.Write(
            "AcceptanceRun",
            $"single inspection finished: pointId = {point.Id}, taskStatus = {viewModel.SinglePointInspectionTaskStatus}, lastTime = {viewModel.SinglePointInspectionLastTime}, resultSummary = {viewModel.SinglePointInspectionResultSummary}");
    }

    private async Task WaitForPageAsync(Type expectedPageViewModelType)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            UpdateLayout();
            if (DataContext is ShellViewModel viewModel
                && viewModel.CurrentPageViewModel.GetType() == expectedPageViewModelType)
            {
                await Task.Delay(1200);
                return;
            }

            await Task.Delay(250);
        }
    }

    private static InspectionPointState? ResolvePreferredAcceptancePoint(IEnumerable<InspectionPointState> points)
    {
        return points.FirstOrDefault(point => AcceptancePreferredDeviceCodes.Contains(point.DeviceCode, StringComparer.OrdinalIgnoreCase))
            ?? points.FirstOrDefault(IsLikelyOnlinePoint)
            ?? points.FirstOrDefault(point => point.CanRenderOnMap)
            ?? points.FirstOrDefault();
    }

    private static InspectionPointState? ResolveSecondAcceptancePoint(
        IEnumerable<InspectionPointState> points,
        InspectionPointState? firstPoint)
    {
        if (firstPoint is null)
        {
            return null;
        }

        return points.FirstOrDefault(point =>
                   !string.Equals(point.Id, firstPoint.Id, StringComparison.Ordinal)
                   && AcceptancePreferredDeviceCodes.Contains(point.DeviceCode, StringComparer.OrdinalIgnoreCase))
               ?? points.FirstOrDefault(point =>
                   !string.Equals(point.Id, firstPoint.Id, StringComparison.Ordinal)
                   && IsLikelyOnlinePoint(point))
               ?? points.FirstOrDefault(point =>
                   !string.Equals(point.Id, firstPoint.Id, StringComparison.Ordinal)
                   && point.CanRenderOnMap)
               ?? points.FirstOrDefault(point => !string.Equals(point.Id, firstPoint.Id, StringComparison.Ordinal));
    }

    private static bool IsLikelyOnlinePoint(InspectionPointState point)
    {
        return point.BusinessSummary.SourceType.Equals("CTYun", StringComparison.OrdinalIgnoreCase)
            && point.OnlineStatus.Contains("在线", StringComparison.OrdinalIgnoreCase)
            && !point.OnlineStatus.Contains("离线", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRenderablePoints(RealMapHost mapHost)
    {
        return mapHost.ItemsSource?.Any(point => point.CanRenderOnMap) == true;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var nestedChild = FindDescendant<T>(child);
            if (nestedChild is not null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}
