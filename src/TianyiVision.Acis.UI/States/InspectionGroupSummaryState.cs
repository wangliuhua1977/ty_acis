using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionGroupSummaryState : ViewModelBase
{
    private bool _isSelected;
    private bool _isEnabled;

    public InspectionGroupSummaryState(string id, string name, string summary, bool isEnabled)
    {
        Id = id;
        Name = name;
        Summary = summary;
        _isEnabled = isEnabled;
    }

    public string Id { get; }

    public string Name { get; }

    public string Summary { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
