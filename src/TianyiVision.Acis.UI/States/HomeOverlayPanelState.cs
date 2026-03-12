using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class HomeOverlayPanelState : ViewModelBase
{
    private double _x;
    private double _y;
    private double _defaultX;
    private double _defaultY;
    private bool _isVisible;
    private bool _isDragEnabled;
    private bool _hasPersistedLayout;
    private bool _isUsingDefaultPositionFallback;

    public HomeOverlayPanelState(
        string id,
        string title,
        double x,
        double y,
        bool isVisible = true,
        bool isDragEnabled = true)
    {
        Id = id;
        Title = title;
        _x = x;
        _y = y;
        _defaultX = x;
        _defaultY = y;
        _isVisible = isVisible;
        _isDragEnabled = isDragEnabled;
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

    public double DefaultX
    {
        get => _defaultX;
        set => SetProperty(ref _defaultX, value);
    }

    public double DefaultY
    {
        get => _defaultY;
        set => SetProperty(ref _defaultY, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsDragEnabled
    {
        get => _isDragEnabled;
        set => SetProperty(ref _isDragEnabled, value);
    }

    public bool HasPersistedLayout
    {
        get => _hasPersistedLayout;
        set => SetProperty(ref _hasPersistedLayout, value);
    }

    public bool IsUsingDefaultPositionFallback
    {
        get => _isUsingDefaultPositionFallback;
        set => SetProperty(ref _isUsingDefaultPositionFallback, value);
    }
}
