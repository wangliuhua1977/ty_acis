using System.Collections.ObjectModel;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class DispatchFilterState : ViewModelBase
{
    private string _selectedQuickFilterKey;
    private DispatchFilterOptionState? _selectedGroupOption;
    private DispatchFilterOptionState? _selectedUnitOption;
    private DispatchFilterOptionState? _selectedMaintainerOption;
    private DispatchFilterOptionState? _selectedSupervisorOption;
    private DispatchFilterOptionState? _selectedFaultTypeOption;

    public DispatchFilterState(
        ObservableCollection<DispatchFilterOptionState> quickFilters,
        ObservableCollection<DispatchFilterOptionState> groupOptions,
        ObservableCollection<DispatchFilterOptionState> unitOptions,
        ObservableCollection<DispatchFilterOptionState> maintainerOptions,
        ObservableCollection<DispatchFilterOptionState> supervisorOptions,
        ObservableCollection<DispatchFilterOptionState> faultTypeOptions,
        string selectedQuickFilterKey)
    {
        QuickFilters = quickFilters;
        GroupOptions = groupOptions;
        UnitOptions = unitOptions;
        MaintainerOptions = maintainerOptions;
        SupervisorOptions = supervisorOptions;
        FaultTypeOptions = faultTypeOptions;
        _selectedQuickFilterKey = selectedQuickFilterKey;
        _selectedGroupOption = groupOptions.FirstOrDefault();
        _selectedUnitOption = unitOptions.FirstOrDefault();
        _selectedMaintainerOption = maintainerOptions.FirstOrDefault();
        _selectedSupervisorOption = supervisorOptions.FirstOrDefault();
        _selectedFaultTypeOption = faultTypeOptions.FirstOrDefault();
    }

    public ObservableCollection<DispatchFilterOptionState> QuickFilters { get; }

    public ObservableCollection<DispatchFilterOptionState> GroupOptions { get; }

    public ObservableCollection<DispatchFilterOptionState> UnitOptions { get; }

    public ObservableCollection<DispatchFilterOptionState> MaintainerOptions { get; }

    public ObservableCollection<DispatchFilterOptionState> SupervisorOptions { get; }

    public ObservableCollection<DispatchFilterOptionState> FaultTypeOptions { get; }

    public string SelectedQuickFilterKey
    {
        get => _selectedQuickFilterKey;
        set => SetProperty(ref _selectedQuickFilterKey, value);
    }

    public DispatchFilterOptionState? SelectedGroupOption
    {
        get => _selectedGroupOption;
        set => SetProperty(ref _selectedGroupOption, value);
    }

    public DispatchFilterOptionState? SelectedUnitOption
    {
        get => _selectedUnitOption;
        set => SetProperty(ref _selectedUnitOption, value);
    }

    public DispatchFilterOptionState? SelectedMaintainerOption
    {
        get => _selectedMaintainerOption;
        set => SetProperty(ref _selectedMaintainerOption, value);
    }

    public DispatchFilterOptionState? SelectedSupervisorOption
    {
        get => _selectedSupervisorOption;
        set => SetProperty(ref _selectedSupervisorOption, value);
    }

    public DispatchFilterOptionState? SelectedFaultTypeOption
    {
        get => _selectedFaultTypeOption;
        set => SetProperty(ref _selectedFaultTypeOption, value);
    }
}
