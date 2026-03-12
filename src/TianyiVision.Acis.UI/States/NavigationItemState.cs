using System.Windows.Input;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class NavigationItemState : ViewModelBase
{
    private bool _isSelected;

    public NavigationItemState(AppSectionId sectionId, string label, string shortCode, ICommand selectCommand)
    {
        SectionId = sectionId;
        Label = label;
        ShortCode = shortCode;
        SelectCommand = selectCommand;
    }

    public AppSectionId SectionId { get; }

    public string Label { get; }

    public string ShortCode { get; }

    public ICommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
