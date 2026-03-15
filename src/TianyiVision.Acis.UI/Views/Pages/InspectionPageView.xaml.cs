using System.Windows;
using System.Windows.Controls;
using TianyiVision.Acis.UI.ViewModels;
using TianyiVision.Acis.UI.Views.Controls;

namespace TianyiVision.Acis.UI.Views.Pages;

public partial class InspectionPageView : UserControl
{
    public InspectionPageView()
    {
        InitializeComponent();
        RealMapHost.AvailabilityChanged += RealMapHost_OnAvailabilityChanged;
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
