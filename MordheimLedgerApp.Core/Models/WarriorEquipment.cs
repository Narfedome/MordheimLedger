using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>An equipment item carried by a specific warrior (join between Warrior and the catalog, e.g. "6 bullets").</summary>
public class WarriorEquipment
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public EquipmentItem Item { get; set; } = null!;
    public int Quantity { get; set; } = 1;

    /// <summary>Null = Item's plain Cost applies. Non-null = a material (e.g. "Gromril", "Ithilmar")
    /// was chosen for this specific carried weapon at purchase time - see SpecialRule.CostMultiplier for
    /// the price math and WarriorEditDialogViewModel.AddEquipment for where it's applied. Any
    /// SpecialRule with CostMultiplier set is eligible, not just those two - the picker lists whatever
    /// the Library catalog currently has.</summary>
    public SpecialRule? MaterialRule { get; set; }

    /// <summary>Null = not blessed. Non-null = an End of Game Exploration branch attached a blessing-type
    /// SpecialRule to this SPECIFIC already-owned weapon (so far only "Blessed Weapon", Shrine's Sisters
    /// of Sigmar/Witch Hunters branch - see ExplorationOutcome.GrantsWeaponBlessing) - a SEPARATE slot
    /// from MaterialRule (never overwrites it) since a weapon can carry BOTH at once (e.g. a Gromril Axe
    /// that also gets blessed, confirmed by the user 2026-08-21) - unlike Material, never chosen at
    /// purchase time, always applied after the fact by IWarbandService.
    /// SetWarriorEquipmentBlessingRuleAsync.</summary>
    public SpecialRule? BlessingRule { get; set; }

    /// <summary>Same idiom as Models.WarbandEquipment.FoundValueOverride - carried over when a stashed
    /// find (e.g. the Jewelsmith's Ruby) is equipped onto a warrior (see IWarbandService.
    /// EquipWarbandItemToWarriorAsync), so the item's detail popup keeps showing what was actually
    /// rolled instead of falling back to the catalog's generic range once it leaves the warband stash.
    /// Not used for resale (no "sell what a warrior carries" flow exists).</summary>
    public int? FoundValueOverride { get; set; }

    /// <summary>"Sword (G)" for a plain material, "Sword (G, B)" if also blessed (both abbreviations,
    /// MaterialRule first) - plain "Sword" if neither is set. Same idiom as EquipmentPick.Name (the
    /// in-memory equivalent before this row exists in the database).</summary>
    public string NameDisplay
    {
        get
        {
            var abbrs = new[] { MaterialRule?.Abbreviation, BlessingRule?.Abbreviation }
                .Where(a => !string.IsNullOrEmpty(a)).ToList();
            return abbrs.Count > 0 ? $"{Item.Name} ({string.Join(", ", abbrs)})" : Item.Name;
        }
    }

    /// <summary>Alias for NameDisplay - ChipView (composant de puce partagé) lie son Label directement
    /// sur Name, quel que soit le type réel qu'on lui passe (voir WarriorInjury.Name).</summary>
    public string Name => NameDisplay;
}
