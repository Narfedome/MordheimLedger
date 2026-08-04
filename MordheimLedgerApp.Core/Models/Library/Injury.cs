namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A permanent injury outcome a warrior can carry (e.g. "Blessure à la jambe"). No category: unlike
/// Skills, the rulebook's Serious Injury table doesn't group results by type. Seeded from the
/// rulebook but fully editable/creatable by the player, same as the rest of the Library.
/// </summary>
public class Injury
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
