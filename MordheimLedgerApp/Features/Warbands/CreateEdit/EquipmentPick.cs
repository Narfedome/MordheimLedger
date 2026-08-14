using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Un objet d'équipement choisi en mémoire à l'étape Équipement du wizard de création, avant
/// tout WarriorId réel - WarbandService.AddWarriorEquipmentAsync exige un guerrier déjà en base, donc
/// impossible de construire un vrai WarriorEquipment ici (voir WarbandEditDialogViewModel.Save, qui
/// convertit chaque EquipmentPick en WarriorEquipment une fois le guerrier recruté). Name/Cost exposés
/// directement (plutôt que via Item.Name/Item.Cost) pour rester utilisable tel quel par ChipView/
/// ChipListView, qui attendent un objet avec Name.</summary>
public class EquipmentPick
{
    public EquipmentItem Item { get; }

    /// <summary>Null = coût normal de l'objet. Non-null = un matériau (Gromril, Ithilmar...) a été
    /// choisi pour cette arme de corps à corps - voir SpecialRule.CostMultiplier et
    /// WarriorEditDialogViewModel.AddEquipment pour le même choix côté guerrier déjà recruté.</summary>
    public SpecialRule? MaterialRule { get; }

    /// <summary>"Sword (G)" when a material with an Abbreviation was chosen, plain "Sword" otherwise -
    /// keeps the chip compact instead of appending the material's full name (see SpecialRule.
    /// Abbreviation).</summary>
    public string Name => MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $"{Item.Name} ({abbr})" : Item.Name;

    /// <summary>Set true by the caller (WarbandEditDialogViewModel.AddEquipment) when Item.IsFreeDagger
    /// and the target doesn't already carry one - overrides Cost to 0 for this specific pick. Not
    /// persisted (no equivalent column on WarriorEquipmentEntity): the actual treasury deduction only
    /// ever happens once, at purchase time in AddEquipment, so nothing downstream needs to remember it
    /// after that - this flag only exists so TotalSpent/RemainingTreasury keep computing correctly for
    /// the rest of the wizard session while this pick sits in a WarriorRecruitRow/HenchmanGroupDraft/
    /// WarriorNameSlot's in-memory Equipment list.</summary>
    public bool IsFree { get; set; }

    public int Cost => EquipmentPricing.CalculateCost(Item.Cost, MaterialRule?.CostMultiplier, IsFree);

    public EquipmentPick(EquipmentItem item, SpecialRule? materialRule)
    {
        Item = item;
        MaterialRule = materialRule;
    }
}
