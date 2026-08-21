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

    /// <summary>Official quality/provenance tier (mordheimer.net's Core/1a/1b/1c/2a classification) -
    /// used to filter/group the Codex Warbands tab, same idiom as Spells grouping by MagicSchool.</summary>
    public WarbandGrade Grade { get; set; }

    public int StartingTreasury { get; set; }

    /// <summary>Null = no roster cap tracked.</summary>
    public int? MaxWarriors { get; set; }

    /// <summary>Null = no roster floor tracked (e.g. the rulebook's "minimum of three models").</summary>
    public int? MinWarriors { get; set; }

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

    /// <summary>Magic school(s) this band's casters may pick spells from (e.g. Nécromancie for Undead) -
    /// empty = this band has no spellcasting. A shared MagicSchool catalog entry (see
    /// WarbandArchetypeMagicSchoolEntity join) rather than a free-text field on each caster archetype,
    /// since in the rulebook a spell list belongs to the warband as a whole - see WarriorArchetype.
    /// IsSpellcaster for which of the band's Hero types may actually cast.</summary>
    public List<MagicSchool> MagicSchools { get; set; } = new();

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>This band's race/species (Humain, Skaven, Orque, Nain...) - see Race, introduced
    /// 2026-08-20 for the Prisonniers "escort as a Henchman if you can equip him" branch, whose book
    /// text restricts joining to "one of your human Henchman groups" (a Skaven/Orc/Dwarf/... band
    /// simply has none). 0 = not set yet (pre-migration data - see AppDatabase.
    /// BackfillWarbandArchetypeRaceAsync, which fixes every existing row on next launch).</summary>
    public int RaceId { get; set; }

    /// <summary>Resolved from RaceId - null only when the caller didn't ask for it (see
    /// LibraryService.GetWarbandArchetypesAsync's optional racesById) or, transiently, for a row not
    /// yet backfilled.</summary>
    public Race? Race { get; set; }
}
