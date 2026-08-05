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
}
