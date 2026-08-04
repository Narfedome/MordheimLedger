namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A skill or spell a Hero can pick from an Advance roll (e.g. "Combat Master", "Step Aside").
/// Seeded from the rulebook but fully editable/creatable by the player, same as the rest of the Library.
/// </summary>
public class Skill
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync/SetTranslationAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public SkillCategory Category { get; set; }
    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Empty = common to every warband. Non-empty = canon-restricted to these WarbandArchetype
    /// ids (an alternate skill table only some warbands can pick from). Data-only for now: shown as a
    /// badge, not enforced in the skill picker (see WarbandArchetypeSkillEntity).</summary>
    public List<int> RestrictedToWarbandArchetypeIds { get; set; } = new();
}
