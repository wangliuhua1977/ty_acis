using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.ViewModels;

public abstract class PageViewModelBase : ViewModelBase
{
    protected PageViewModelBase(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }

    public Action<AppSectionId>? NavigateToSection { get; set; }

    protected void RequestNavigate(AppSectionId sectionId)
        => NavigateToSection?.Invoke(sectionId);
}
