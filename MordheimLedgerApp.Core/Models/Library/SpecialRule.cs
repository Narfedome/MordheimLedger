namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A named, reusable rule (e.g. "Chef"/"Leader", "Provoque la Peur"/"Causes Fear") shared across
/// warbands and warrior types via WarbandArchetypeSpecialRuleEntity/WarriorArchetypeSpecialRuleEntity
/// join tables, instead of being duplicated as free text on every archetype that has it. Description
/// text stays generic/mechanical (no per-archetype flavor) so the same entry reads correctly wherever
/// it's attached - archetype-specific flavor belongs on the archetype's own Description instead.
/// Descriptive text only, same "no rules engine V1" boundary as the rest of the Library.
/// </summary>
public class SpecialRule
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Null = this rule has no purchase-cost effect (the vast majority). Non-null marks it as a
    /// selectable "material" a player can apply to a hand-to-hand weapon when buying it for a warrior
    /// (e.g. "Gromril" -&gt; 4, "Ithilmar" -&gt; 3): the actual gc paid is the weapon's Cost times this
    /// multiplier - see WarriorEquipment.MaterialRule. Editable like any other SpecialRule field, so
    /// correcting the multiplier (or the rule text) here applies everywhere it's used - no rules engine
    /// beyond this one multiplication, same "no rules engine V1" stance as the rest of the Library.</summary>
    public int? CostMultiplier { get; set; }
}
