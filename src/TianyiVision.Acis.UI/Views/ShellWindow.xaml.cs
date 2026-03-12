using System.Windows;
using TianyiVision.Acis.UI.ViewModels;

namespace TianyiVision.Acis.UI.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
