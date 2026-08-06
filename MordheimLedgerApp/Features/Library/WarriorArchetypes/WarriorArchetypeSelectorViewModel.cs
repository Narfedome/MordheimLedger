using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes;

/// <summary>Selector-only sibling of WarriorArchetypeViewModel (that one is a full CRUD page tied to
/// one WarbandArchetypeId via Shell QueryProperty, single-select - not reused here to avoid
/// regressing its "manage this warband's roster" flow). Multi-select across possibly several
/// warbands at once, no grouping (see SkillEditDialogViewModel.AddRestrictedWarrior - in practice 0
/// or 1 warband is selected when this picker opens).</summary>
public partial class WarriorArchetypeSelectorViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IWarriorArchetypePickerNavigationService _pickerNavigation;

    [ObservableProperty]
    private ObservableCollection<WarriorArchetypeRow> items = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    public ObservableCollection<WarriorArchetypeRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    /// <summary>Set by WarriorArchetypePickerService right after construction, before InitializeAsync -
    /// only warriors of one of these warbands are shown.</summary>
    public List<int> AllowedWarbandArchetypeIds { get; set; } = new();

    public WarriorArchetypeSelectorViewModel(ILibraryService libraryService, IWarriorArchetypePickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var warriors = await _libraryService.GetWarriorArchetypesAsync(AllowedWarbandArchetypeIds, LocalizationService.Instance.Language);
        Items = new ObservableCollection<WarriorArchetypeRow>(warriors.Select(w => new WarriorArchetypeRow(w)));
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    [RelayCommand]
    private void Select(WarriorArchetypeRow row)
    {
        row.IsSelected = !row.IsSelected;
        if (row.IsSelected) SelectedRows.Add(row);
        else SelectedRows.Remove(row);
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<WarriorArchetype>());
}
