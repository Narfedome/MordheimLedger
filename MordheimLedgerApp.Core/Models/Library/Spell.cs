namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// One entry (a single die-roll result) of a spell/prayer/ritual table (e.g. "Nécromancie", "Prières
/// de Sigmar", "Magie Waaagh") - Mordheim spellcasting is a fixed D6/2D6 table per tradition, not a
/// freely composed spell list. MagicSchool groups entries into their table - a caster's WarbandArchetype
/// grants access to a school (see WarbandArchetype.MagicSchools), which is what the Spell picker filters
/// against (see WarriorEditDialogViewModel.AddSpell). Purely descriptive like the rest of the Library
/// ("no rules engine V1") - there's no in-app casting/rolling, just reference text.
/// </summary>
public class Spell
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync/SetTranslationAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    /// <summary>Which table this entry belongs to.</summary>
    public int MagicSchoolId { get; set; }

    /// <summary>Resolved from MagicSchoolId - see LibraryService.GetSpellsAsync.</summary>
    public MagicSchool? MagicSchool { get; set; }

    /// <summary>The die-roll result this entry corresponds to (1-6 for a D6 table, etc.). Defaults to 1
    /// (never 0, which is never a valid roll on a D6/2D6 table) so a freshly created Spell doesn't start
    /// on an already-invalid value.</summary>
    public int RollValue { get; set; } = 1;

    /// <summary>Target number to cast ("Difficulté X") - null for tables that don't use one.</summary>
    public int? Difficulty { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
