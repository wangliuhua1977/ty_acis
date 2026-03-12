using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class HomeOverlayPanelState : ViewModelBase
{
    private double _x;
    private double _y;
    private bool _isVisible;

    public HomeOverlayPanelState(string id, string title, double x, double y, bool isVisible = true)
    {
        Id = id;
        Title = title;
        _x = x;
        _y = y;
        _isVisible = isVisible;
    }

    public string Id { get; }

    public string Title { get; }

    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
