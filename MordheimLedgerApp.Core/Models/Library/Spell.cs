namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// One entry (a single die-roll result) of a spell/prayer/ritual table (e.g. "Nécromancie", "Prières
/// de Sigmar", "Magie Waaagh") - Mordheim spellcasting is a fixed D6/2D6 table per tradition, not a
/// freely composed spell list. SpellListName groups entries into their table (see WarriorArchetype.
/// SpellListName, which links a Hero archetype to the table it rolls on by matching this string) - kept
/// as a plain field rather than a separate normalized entity for simplicity, since a spell list is just
/// a shared grouping label. Purely descriptive like the rest of the Library ("no rules engine V1") -
/// there's no in-app casting/rolling, just reference text.
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

    /// <summary>Which table this entry belongs to (e.g. "Nécromancie") - matches WarriorArchetype.
    /// SpellListName for the archetype(s) that can roll on it.</summary>
    public string SpellListName { get; set; } = string.Empty;

    /// <summary>The die-roll result this entry corresponds to (1-6 for a D6 table, etc.).</summary>
    public int RollValue { get; set; }

    /// <summary>Target number to cast ("Difficulté X") - null for tables that don't use one.</summary>
    public int? Difficulty { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
