using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>An equipment item held by the warband itself rather than by any specific warrior - the
/// band's "unassigned stash" (e.g. loot found via the End of Game wizard's Exploration step, not tied
/// to a warrior at the table either). Deliberately a separate entity from WarriorEquipment rather than
/// a nullable WarriorId on that same table: WarriorEquipment is deeply assumed to always belong to a
/// warrior across the app (weapon limits, warrior card chips, restriction checks) - keeping it that way
/// avoids touching any of that. See IWarbandService.EquipWarbandItemToWarriorAsync for moving a row
/// here onto a warrior (delete here, insert there) rather than flipping a field.</summary>
public class WarbandEquipment
{
    public int Id { get; set; }
    public int WarbandId { get; set; }
    public EquipmentItem Item { get; set; } = null!;
    public int Quantity { get; set; } = 1;

    /// <summary>Null = Item's plain Cost applies. Non-null = a material (e.g. "Gromril") - see
    /// WarriorEquipment.MaterialRule, same idiom.</summary>
    public SpecialRule? MaterialRule { get; set; }

    /// <summary>True only when MaterialRule.IsResaleUpgrade is set (e.g. "Ornate Weapon" from Overturned
    /// Cart) - gates the "Vendre" action in WarbandInventoryDialog. There's no generic "sell any
    /// equipment" mechanic in the core rules, so a plain find (or a normal Gromril/Ithilmar purchase that
    /// somehow ended up here) is never sellable.</summary>
    public bool IsSellable => MaterialRule?.IsResaleUpgrade == true;

    public string NameDisplay => MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $"{Item.Name} ({abbr})" : Item.Name;

    /// <summary>Alias for NameDisplay - ChipView binds its Label directly to Name, same idiom as
    /// WarriorEquipment.Name.</summary>
    public string Name => NameDisplay;
}
