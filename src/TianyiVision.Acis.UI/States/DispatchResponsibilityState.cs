using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class DispatchResponsibilityState : ViewModelBase
{
    private string _currentHandlingUnit;
    private string _maintainerName;
    private string _maintainerPhone;
    private string _supervisorName;
    private string _supervisorPhone;

    public DispatchResponsibilityState(
        string currentHandlingUnit,
        string maintainerName,
        string maintainerPhone,
        string supervisorName,
        string supervisorPhone)
    {
        _currentHandlingUnit = currentHandlingUnit;
        _maintainerName = maintainerName;
        _maintainerPhone = maintainerPhone;
        _supervisorName = supervisorName;
        _supervisorPhone = supervisorPhone;
    }

    public string CurrentHandlingUnit
    {
        get => _currentHandlingUnit;
        set => SetProperty(ref _currentHandlingUnit, value);
    }

    public string MaintainerName
    {
        get => _maintainerName;
        set => SetProperty(ref _maintainerName, value);
    }

    public string MaintainerPhone
    {
        get => _maintainerPhone;
        set => SetProperty(ref _maintainerPhone, value);
    }

    public string SupervisorName
    {
        get => _supervisorName;
        set => SetProperty(ref _supervisorName, value);
    }

    public string SupervisorPhone
    {
        get => _supervisorPhone;
        set => SetProperty(ref _supervisorPhone, value);
    }

    public DispatchResponsibilityState Clone()
        => new(CurrentHandlingUnit, MaintainerName, MaintainerPhone, SupervisorName, SupervisorPhone);
}
