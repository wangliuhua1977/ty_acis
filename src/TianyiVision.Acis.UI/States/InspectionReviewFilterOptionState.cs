using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionReviewFilterOptionState : ViewModelBase
{
    private bool _isSelected;

    public InspectionReviewFilterOptionState(string key, string label, bool isSelected = false)
    {
        Key = key;
        Label = label;
        _isSelected = isSelected;
    }

    public string Key { get; }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
