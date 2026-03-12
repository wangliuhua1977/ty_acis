using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TianyiVision.Acis.UI.States;
using TianyiVision.Acis.UI.ViewModels;

namespace TianyiVision.Acis.UI.Views.Pages;

public partial class HomePageView : UserControl
{
    private Point? _dragStart;
    private Point _panelStart;
    private HomeOverlayPanelState? _dragPanel;

    public HomePageView()
    {
        InitializeComponent();
    }

    private void HomePageView_OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncOverlayLayout();
    }

    private void HomePageView_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SyncOverlayLayout();
    }

    private void OverlayHeader_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not HomeOverlayPanelState panel)
        {
            return;
        }

        _dragPanel = panel;
        _dragStart = e.GetPosition(OverlayCanvas);
        _panelStart = new Point(panel.X, panel.Y);
        element.CaptureMouse();
        e.Handled = true;
    }

    private void OverlayHeader_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragPanel is null
            || _dragStart is null
            || DataContext is not HomePageViewModel viewModel
            || sender is not FrameworkElement element
            || !element.IsMouseCaptured)
        {
            return;
        }

        var current = e.GetPosition(OverlayCanvas);
        viewModel.UpdateOverlayPanelPosition(
            _dragPanel.Id,
            _panelStart.X + current.X - _dragStart.Value.X,
            _panelStart.Y + current.Y - _dragStart.Value.Y);
    }

    private void OverlayHeader_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        if (_dragPanel is not null && DataContext is HomePageViewModel viewModel)
        {
            viewModel.CommitOverlayLayout();
        }

        _dragStart = null;
        _dragPanel = null;
    }

    private void SyncOverlayLayout()
    {
        if (DataContext is HomePageViewModel viewModel
            && OverlayCanvas.ActualWidth > 0
            && OverlayCanvas.ActualHeight > 0)
        {
            viewModel.InitializeOverlayLayout(OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);
        }
    }
}
