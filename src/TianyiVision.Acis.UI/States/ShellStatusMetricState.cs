using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class ShellStatusMetricState : ViewModelBase
{
    private bool _isSelected;

    public ShellStatusMetricState(string title, string value)
    {
        Title = title;
        Value = value;
    }

    public string Title { get; }

    public string Value { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
