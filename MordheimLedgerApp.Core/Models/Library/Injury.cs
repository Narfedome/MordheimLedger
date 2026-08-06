namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A permanent injury outcome a warrior can carry (e.g. "Blessure à la jambe"), one row of either the
/// Heroes' D66 Serious Injuries chart or the Henchmen's simpler D6 chart (see Category). Seeded from
/// the rulebook but fully editable/creatable by the player, same as the rest of the Library - see
/// MordheimLedgerApp.Services.SeriousInjuryTable/HenchmanInjuryTable for the separate, already-tested
/// dice-roll lookup used by the End of Game wizard, which this catalog doesn't drive (kept apart, per
/// the "no rules engine V1" boundary - this is browsing/reference/editing only).
/// </summary>
public class Injury
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync. Editing and saving writes it back as the translation value for
    /// NameKey in whatever language was requested.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public InjuryCategory Category { get; set; }

    /// <summary>Free-text dice roll reference for this row (e.g. "11-15", "66", "3-6") - purely a
    /// display aid matching the rulebook chart's own D66/D6 notation, not parsed/validated by the app.
    /// Null for player-created entries that don't map to a specific roll.</summary>
    public string? RollRange { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
