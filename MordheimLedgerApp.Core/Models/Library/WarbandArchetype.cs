namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A warband "type" (e.g. Reiklander Mercenaries) — the actual catalog entry that
/// Warband.WarbandArchetypeId points to. Selecting one when creating a Warband pre-fills its
/// starting treasury; selecting one of its WarriorArchetypes when adding a Warrior pre-fills stats.
/// </summary>
public class WarbandArchetype
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContentSource Source { get; set; }
    public int StartingTreasury { get; set; }

    /// <summary>Null = no roster cap tracked.</summary>
    public int? MaxWarriors { get; set; }

    public string? Description { get; set; }
}
