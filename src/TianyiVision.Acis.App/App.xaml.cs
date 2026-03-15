using System.Windows;
using TianyiVision.Acis.UI.ViewModels;
using TianyiVision.Acis.UI.Views;

namespace TianyiVision.Acis.App;

public partial class App : Application
{
    private AppBootstrapper? _bootstrapper;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _bootstrapper = new AppBootstrapper();
        _bootstrapper.ApplyTheme(Resources);

        ShellViewModel shellViewModel = _bootstrapper.CreateShellViewModel(Resources);
        var shellWindow = new ShellWindow(shellViewModel);

        MainWindow = shellWindow;
        shellWindow.Show();
    }
}
