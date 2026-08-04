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
/// Pure reference browsing - unlike Equipment/Skill/Injury, Spells are never picked onto a Warrior (no
/// in-app casting, "no rules engine V1"), so there's no picker/selector mode here at all.
/// </summary>
public partial class SpellViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;

    [ObservableProperty]
    private ObservableCollection<SpellRow> spells = new();

    [ObservableProperty]
    private SpellRow? selectedRow;

    public SpellViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((SpellViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetSpellsAsync(LocalizationService.Instance.Language);
        Spells = new ObservableCollection<SpellRow>(allItems.Select(i => new SpellRow(i)));
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(SpellRow? oldValue, SpellRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(SpellRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new Spell();
        var dialogViewModel = new SpellEditDialogViewModel(newItem, Loc["SpellCreateTitle"]);
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
            SpellListName = s.SpellListName,
            RollValue = s.RollValue,
            Difficulty = s.Difficulty,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new SpellEditDialogViewModel(copy, Loc["SpellEditTitle"]);
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
