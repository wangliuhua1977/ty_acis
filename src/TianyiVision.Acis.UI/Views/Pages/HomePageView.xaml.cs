using System.ComponentModel;
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
    private HomePageViewModel? _viewModel;

    public HomePageView()
    {
        InitializeComponent();
        DataContextChanged += HomePageView_OnDataContextChanged;
    }

    private void HomePageView_OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncOverlayLayout();
    }

    private void HomePageView_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SyncOverlayLayout();
    }

    private void HomePageView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            UnsubscribePanelChanges(_viewModel);
        }

        _viewModel = e.NewValue as HomePageViewModel;

        if (_viewModel is not null)
        {
            SubscribePanelChanges(_viewModel);
            SyncOverlayLayout();
        }
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
        ApplyOverlayPanelStates();
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

    private void RestoreTaskPanel_OnClick(object sender, RoutedEventArgs e)
    {
        RestoreOverlayPanel(_viewModel?.TaskPanel);
        e.Handled = true;
    }

    private void RestoreFaultPanel_OnClick(object sender, RoutedEventArgs e)
    {
        RestoreOverlayPanel(_viewModel?.FaultPanel);
        e.Handled = true;
    }

    private void RestorePointPanel_OnClick(object sender, RoutedEventArgs e)
    {
        RestoreOverlayPanel(_viewModel?.PointPanel);
        e.Handled = true;
    }

    private void RestoreLegendPanel_OnClick(object sender, RoutedEventArgs e)
    {
        RestoreOverlayPanel(_viewModel?.LegendPanel);
        e.Handled = true;
    }

    private void SyncOverlayLayout()
    {
        if (DataContext is HomePageViewModel viewModel
            && OverlayCanvas.ActualWidth > 0
            && OverlayCanvas.ActualHeight > 0)
        {
            viewModel.InitializeOverlayLayout(OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);
            ApplyOverlayPanelStates();
        }
    }

    private void SubscribePanelChanges(HomePageViewModel viewModel)
    {
        foreach (var panel in viewModel.OverlayPanels)
        {
            panel.PropertyChanged += OverlayPanel_OnPropertyChanged;
        }
    }

    private void UnsubscribePanelChanges(HomePageViewModel viewModel)
    {
        foreach (var panel in viewModel.OverlayPanels)
        {
            panel.PropertyChanged -= OverlayPanel_OnPropertyChanged;
        }
    }

    private void OverlayPanel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeOverlayPanelState.X)
            or nameof(HomeOverlayPanelState.Y)
            or nameof(HomeOverlayPanelState.IsVisible))
        {
            Dispatcher.Invoke(ApplyOverlayPanelStates);
        }
    }

    private void ApplyOverlayPanelStates()
    {
        if (DataContext is not HomePageViewModel viewModel)
        {
            return;
        }

        ApplyOverlayPanelState(TaskPanelBorder, viewModel.TaskPanel);
        ApplyOverlayPanelState(FaultPanelBorder, viewModel.FaultPanel);
        ApplyOverlayPanelState(PointPanelBorder, viewModel.PointPanel);
        ApplyOverlayPanelState(LegendPanelBorder, viewModel.LegendPanel);
    }

    private static void ApplyOverlayPanelState(Border border, HomeOverlayPanelState panel)
    {
        Canvas.SetLeft(border, panel.X);
        Canvas.SetTop(border, panel.Y);
        Panel.SetZIndex(border, 30);
        border.Visibility = panel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RestoreOverlayPanel(HomeOverlayPanelState? panel)
    {
        if (_viewModel is null || panel is null)
        {
            return;
        }

        _viewModel.RestoreOverlayPanel(panel);
        ApplyOverlayPanelStates();
        UpdateLayout();
    }
}
