using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

/// <summary>Read-only recap of WarriorArchetypeEditDialog - single scrollable view rather than the
/// Edit dialog's 3-step wizard (no other Codex detail dialog uses multiple steps).</summary>
public partial class WarriorArchetypeDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public WarriorArchetype Item { get; }
    public string RoleDisplay { get; }
    public string MaxCountDisplay { get; }
    public string EquipmentListDisplay { get; }

    /// <summary>Already resolved by the caller (WarbandArchetypeDetailDialogViewModel, which already
    /// fetches the warband's EquipmentLists for its own Équipement tab) - avoids a second fetch just to
    /// resolve Item.EquipmentListId here.</summary>
    public WarriorArchetypeDetailDialogViewModel(WarriorArchetype item, IReadOnlyList<EquipmentList> warbandEquipmentLists)
    {
        Item = item;
        Title = item.Name;
        RoleDisplay = item.IsHero ? Loc["WarriorRoleHeroSingular"] : Loc["WarriorRoleHenchmanSingular"];
        MaxCountDisplay = item.MaxCount?.ToString() ?? "—";
        EquipmentListDisplay = item.EquipmentListId is { } listId
            ? warbandEquipmentLists.FirstOrDefault(l => l.Id == listId)?.Name ?? Loc["WarriorArchetypeEquipmentListNone"]
            : Loc["WarriorArchetypeEquipmentListNone"];
    }

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);
}
