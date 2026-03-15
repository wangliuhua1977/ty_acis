using System.IO;
using System.Windows;
using System.Windows.Media;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.UI.ViewModels;
using TianyiVision.Acis.UI.Views.Controls;

namespace TianyiVision.Acis.UI.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var exportDirectory = Environment.GetEnvironmentVariable("ACIS_EXPORT_SCREENSHOTS_DIR");
        if (string.IsNullOrWhiteSpace(exportDirectory) || DataContext is not ShellViewModel viewModel)
        {
            return;
        }

        await ExportMapScreenshotsAsync(viewModel, exportDirectory);
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
