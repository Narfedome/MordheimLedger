namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A warband "type" (e.g. Reiklander Mercenaries) — the actual catalog entry that
/// Warband.WarbandArchetypeId points to. Selecting one when creating a Warband pre-fills its
/// starting treasury; selecting one of its WarriorArchetypes when adding a Warrior pre-fills stats.
/// </summary>
public class WarbandArchetype
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync/SetTranslationAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public ContentSource Source { get; set; }
    public int StartingTreasury { get; set; }

    /// <summary>Null = no roster cap tracked.</summary>
    public int? MaxWarriors { get; set; }

    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    /// <summary>Rules that apply to every warrior in the band regardless of type (e.g. "Autonome" for
    /// Ostlanders, "Difficiles à Tuer"/"Haine des Orques et des Gobelins"/... for Dwarfs) - distinct from
    /// a WarriorArchetype's own SpecialRules, which only apply to that one warrior type. Each SpecialRule
    /// is a shared catalog entry (see WarbandArchetypeSpecialRuleEntity join) rather than free text, so
    /// e.g. "Chef"/"Leader" reads identically wherever it's attached instead of being retyped per band.</summary>
    public List<SpecialRule> SpecialRules { get; set; } = new();

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
