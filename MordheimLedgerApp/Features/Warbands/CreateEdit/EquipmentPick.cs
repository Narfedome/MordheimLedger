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

    /// <summary>Set true by the caller (EndOfGamePageViewModel.Recruitment.AddRecruitEquipment) when
    /// this pick was already sitting in the band's réserve (unassigned WarbandEquipment, from before this
    /// same session - see BuildAvailableReservePool) rather than bought fresh - 2026-09-05, retour
    /// utilisateur "assignation des équipements de la stash" plutôt qu'un achat systématique au picker.
    /// Distinct from IsFree (dague gratuite, une règle différente) même si les deux ramènent Cost à 0 -
    /// gardés séparés pour ne pas mélanger deux raisons différentes d'être gratuit.</summary>
    public bool FromReserve { get; set; }

    /// <summary>Set true by the caller (EndOfGamePageViewModel.Reallocation) when this pick represents
    /// an item moved for free from an existing Hero or the reserve via the "Réallouer l'équipement" step
    /// (livre des règles - "Swap equipment between models as desired") - never a new purchase. Distinct
    /// from FromReserve (which means "consumed from the band's shared stash pool during Recruitment") and
    /// IsFree (free dagger) even though all three zero out Cost, same reasoning as those two: never mix
    /// two different reasons for being free.</summary>
    public bool IsReallocated { get; set; }

    public int Cost => EquipmentPricing.CalculateCost(Item.Cost, MaterialRule?.CostMultiplier, IsFree || FromReserve || IsReallocated);

    /// <summary>Null = pick à acheter au Save (comportement d'origine). Non-null = id du WarriorEquipment
    /// déjà en base que ce pick représente (bande rouverte pour édition, voir RecruitSlot constructeur) -
    /// déjà payé, exclu de TotalSpent (WarbandEditDialogViewModel) et laissé tel quel par Save() plutôt
    /// que racheté ; sert aussi à détecter un retrait (Save() compare la liste courante à ce que
    /// RecruitSlot.BaselineEquipment portait).</summary>
    public int? ExistingId { get; init; }

    public EquipmentPick(EquipmentItem item, SpecialRule? materialRule)
    {
        Item = item;
        MaterialRule = materialRule;
    }
}
