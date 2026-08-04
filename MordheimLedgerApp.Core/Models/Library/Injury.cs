namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A permanent injury outcome a warrior can carry (e.g. "Blessure à la jambe"). No category: unlike
/// Skills, the rulebook's Serious Injury table doesn't group results by type. Seeded from the
/// rulebook but fully editable/creatable by the player, same as the rest of the Library.
/// </summary>
public class Injury
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync. Editing and saving writes it back as the translation value for
    /// NameKey in whatever language was requested.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
