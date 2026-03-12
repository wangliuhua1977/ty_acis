using System.Collections.ObjectModel;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionReviewFilterState : ViewModelBase
{
    private string _selectedUnit = string.Empty;
    private string _selectedFaultType = string.Empty;
    private string _selectedQuickFilterKey = string.Empty;

    public InspectionReviewFilterState(
        ObservableCollection<InspectionReviewFilterOptionState> quickFilters,
        ObservableCollection<string> unitOptions,
        ObservableCollection<string> faultTypeOptions,
        string selectedUnit,
        string selectedFaultType,
        string selectedQuickFilterKey)
    {
        QuickFilters = quickFilters;
        UnitOptions = unitOptions;
        FaultTypeOptions = faultTypeOptions;
        _selectedUnit = selectedUnit;
        _selectedFaultType = selectedFaultType;
        _selectedQuickFilterKey = selectedQuickFilterKey;
    }

    public ObservableCollection<InspectionReviewFilterOptionState> QuickFilters { get; }

    public ObservableCollection<string> UnitOptions { get; }

    public ObservableCollection<string> FaultTypeOptions { get; }

    public string SelectedUnit
    {
        get => _selectedUnit;
        set => SetProperty(ref _selectedUnit, value);
    }

    public string SelectedFaultType
    {
        get => _selectedFaultType;
        set => SetProperty(ref _selectedFaultType, value);
    }

    public string SelectedQuickFilterKey
    {
        get => _selectedQuickFilterKey;
        set => SetProperty(ref _selectedQuickFilterKey, value);
    }
}
