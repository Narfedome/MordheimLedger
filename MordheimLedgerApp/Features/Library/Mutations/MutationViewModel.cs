using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Mutations.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Mutations;

public partial class MutationViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IMutationPickerNavigationService _pickerNavigation;

    [ObservableProperty]
    private ObservableCollection<MutationRow> mutations = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private MutationRow? selectedRow;

    /// <summary>Set by MutationSelectorPage right after construction - même bascule multi-sélection
    /// qu'InjuryViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<MutationRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public MutationViewModel(ILibraryService libraryService, IMutationPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((MutationViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetMutationsAsync(LocalizationService.Instance.Language);
        Mutations = new ObservableCollection<MutationRow>(allItems.Select(i => new MutationRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(MutationRow? oldValue, MutationRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(MutationRow row)
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
        var newItem = new Mutation();
        var dialogViewModel = new MutationEditDialogViewModel(newItem, Loc["MutationCreateTitle"]);
        if (await ShowDialogAsync(new MutationEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMutationAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Mutation
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Cost = s.Cost,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new MutationEditDialogViewModel(copy, Loc["MutationEditTitle"]);
        if (await ShowDialogAsync(new MutationEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMutationAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteMutationAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Mutation>());
}
