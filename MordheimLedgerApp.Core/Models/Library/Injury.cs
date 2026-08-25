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

    /// <summary>Same free-text convention as RollRange, one level down: for a D66 result that itself
    /// branches on a further 1D6 (Arm Wound/Smashed Leg, roll "23"/"25" - see Core.Rules.
    /// SeriousInjuryEffectTable.RequiresBranchSubRoll), each branch is its own catalog row sharing the
    /// same RollRange but a distinct BranchRange ("1" vs "2-6") - so the chip/History text names the
    /// actual outcome ("Blessure au bras : amputé") rather than the ambiguous shared roll name. Null for
    /// every non-branching row (the overwhelming majority).</summary>
    public string? BranchRange { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Rules permanently granted to whoever carries this Injury (e.g. Stupidity/Frenzy from
    /// Madness, 24) - same shared SpecialRule catalog/join-table idiom as EquipmentItem.SpecialRules,
    /// resolved once and merged into the carrying Warrior's own SpecialRules chip list
    /// (WarbandDetailViewModel.ToRow) rather than tracked as a separate mechanized effect - for this
    /// kind of Serious Injury result, the chip/rule reminder IS the effect. Empty for the overwhelming
    /// majority of rows (Palier 1's stat/status/equipment effects don't need this).</summary>
    public List<SpecialRule> SpecialRules { get; set; } = new();
}
