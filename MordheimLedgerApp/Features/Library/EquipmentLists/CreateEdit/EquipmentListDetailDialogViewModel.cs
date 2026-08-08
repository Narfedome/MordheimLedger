using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;

/// <summary>Read-only recap of EquipmentListEditDialog. EquipmentList has no Description field (just a
/// Name + member items). Tapping a member item reuses the Trading Post's own EquipmentItemDetailDialog
/// (same recap everywhere an EquipmentItem is shown, on explicit user request) - restrictions are
/// resolved lazily on tap, same idiom as EquipmentItemViewModel.ShowDetails, to avoid the eager-fetch
/// delay the parent WarbandArchetypeDetailDialogViewModel used to have.</summary>
public partial class EquipmentListDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public EquipmentList Item { get; }

    /// <summary>Already resolved by the caller (WarbandArchetypeDetailDialogViewModel) from
    /// Item.ItemIds - same idiom as every other XxxDetailDialogViewModel.</summary>
    public List<EquipmentItem> Items { get; }

    private readonly ILibraryService _libraryService;

    public EquipmentListDetailDialogViewModel(EquipmentList item, List<EquipmentItem> items, ILibraryService libraryService)
    {
        Item = item;
        Title = item.Name;
        Items = items;
        _libraryService = libraryService;
    }

    [RelayCommand]
    private async Task ShowItemDetail(EquipmentItem equipmentItem)
    {
        var language = LocalizationService.Instance.Language;
        var categoryLabel = Loc[$"EquipmentCategory{equipmentItem.Category}"];

        var restrictedWarbands = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : (await _libraryService.GetWarbandArchetypesAsync(language))
                .Where(w => equipmentItem.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var restrictedWarriors = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0 || equipmentItem.RestrictedToWarriorArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(equipmentItem.RestrictedToWarbandArchetypeIds, language))
                .Where(w => equipmentItem.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        await ShowDialogAsync(new EquipmentItemDetailDialog(
            new EquipmentItemDetailDialogViewModel(equipmentItem, categoryLabel, restrictedWarbands, restrictedWarriors)));
    }
}
