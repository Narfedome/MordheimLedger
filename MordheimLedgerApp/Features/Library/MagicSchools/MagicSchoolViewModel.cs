using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.MagicSchools.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.MagicSchools;

public partial class MagicSchoolViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IMagicSchoolPickerNavigationService _pickerNavigation;

    [ObservableProperty]
    private ObservableCollection<MagicSchoolRow> magicSchools = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private MagicSchoolRow? selectedRow;

    /// <summary>Set by MagicSchoolSelectorPage right after construction - même bascule multi-sélection
    /// que SpecialRuleViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<MagicSchoolRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public MagicSchoolViewModel(ILibraryService libraryService, IMagicSchoolPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((MagicSchoolViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetMagicSchoolsAsync(LocalizationService.Instance.Language);
        MagicSchools = new ObservableCollection<MagicSchoolRow>(allItems.Select(i => new MagicSchoolRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(MagicSchoolRow? oldValue, MagicSchoolRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(MagicSchoolRow row)
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
    private async Task Create()
    {
        var newItem = new MagicSchool();
        var dialogViewModel = new MagicSchoolEditDialogViewModel(newItem, Loc["MagicSchoolCreateTitle"]);
        if (await ShowDialogAsync(new MagicSchoolEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMagicSchoolAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new MagicSchool
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new MagicSchoolEditDialogViewModel(copy, Loc["MagicSchoolEditTitle"]);
        if (await ShowDialogAsync(new MagicSchoolEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMagicSchoolAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteMagicSchoolAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<MagicSchool>());
}
