using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class HomeStatusMetricState : ViewModelBase
{
    private bool _isSelected;

    public HomeStatusMetricState(string title, string value, string hint)
    {
        Title = title;
        Value = value;
        Hint = hint;
    }

    public string Title { get; }

    public string Value { get; }

    public string Hint { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
