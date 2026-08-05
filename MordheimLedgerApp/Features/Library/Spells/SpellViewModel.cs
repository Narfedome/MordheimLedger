using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Spells.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Spells;

/// <summary>
/// Also doubles as a picker (see WarriorSpell): a caster learns specific spells one at a time via
/// Advance rolls rather than knowing its whole magic school table, so Spells DO get attached to a
/// Warrior now - same IsSelectorMode/SelectedRows multi-select toggle as InjuryViewModel.
/// </summary>
public partial class SpellViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly ISpellPickerNavigationService _pickerNavigation;
    private List<MagicSchool> _magicSchools = new();

    [ObservableProperty]
    private ObservableCollection<SpellRow> spells = new();

    /// <summary>Null (Library CRUD tab) = no filter, shows every school's spells. Non-null (picker mode,
    /// set by SpellPickerService before construction) = only spells whose MagicSchool.Id is in this set,
    /// enforcing that a Warrior can only learn from the school(s) its WarbandArchetype grants access to
    /// (see WarriorEditDialogViewModel.AddSpell).</summary>
    public List<int>? AllowedMagicSchoolIds { get; set; }

    [ObservableProperty]
    private SpellRow? selectedRow;

    /// <summary>Set by SpellSelectorPage right after construction - même bascule multi-sélection
    /// qu'InjuryViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<SpellRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public SpellViewModel(ILibraryService libraryService, ISpellPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((SpellViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetSpellsAsync(LocalizationService.Instance.Language);
        _magicSchools = await _libraryService.GetMagicSchoolsAsync(LocalizationService.Instance.Language);

        var filtered = AllowedMagicSchoolIds is null
            ? allItems
            : allItems.Where(s => AllowedMagicSchoolIds.Contains(s.MagicSchoolId)).ToList();

        Spells = new ObservableCollection<SpellRow>(filtered.Select(i => new SpellRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(SpellRow? oldValue, SpellRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(SpellRow row)
    {
        if (!IsSelectorMode)
        {
            SelectedRow = row;
            return;
        }

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
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Spell>());

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new Spell();
        var dialogViewModel = new SpellEditDialogViewModel(newItem, Loc["SpellCreateTitle"], _magicSchools);
        if (await ShowDialogAsync(new SpellEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSpellAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Spell
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            MagicSchoolId = s.MagicSchoolId,
            RollValue = s.RollValue,
            Difficulty = s.Difficulty,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new SpellEditDialogViewModel(copy, Loc["SpellEditTitle"], _magicSchools);
        if (await ShowDialogAsync(new SpellEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSpellAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteSpellAsync(row.Item.Id);
        await LoadData();
    }
}
