using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
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

    private readonly IDetailDialogService _detailDialogs;

    public EquipmentListDetailDialogViewModel(EquipmentList item, List<EquipmentItem> items, IDetailDialogService detailDialogs)
    {
        Item = item;
        Title = item.Name;
        Items = items;
        _detailDialogs = detailDialogs;
    }

    [RelayCommand]
    private Task ShowItemDetail(EquipmentItem equipmentItem) => _detailDialogs.ShowEquipmentDetailDialogAsync(equipmentItem);
}
