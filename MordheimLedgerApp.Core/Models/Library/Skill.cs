namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A skill or spell a Hero can pick from an Advance roll (e.g. "Combat Master", "Step Aside").
/// Seeded from the rulebook but fully editable/creatable by the player, same as the rest of the Library.
/// </summary>
public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SkillCategory Category { get; set; }
    public string? Description { get; set; }
    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
