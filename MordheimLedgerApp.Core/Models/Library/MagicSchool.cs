namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A spell/prayer/ritual tradition (e.g. "Nécromancie", "Prières de Taal", "Magie Waaagh") - the
/// entity Spell.MagicSchoolId points to and WarbandArchetype.MagicSchools grants access to. Splitting
/// this out of the free-text SpellListName it used to be lets a band's granted school(s) drive both
/// which spells are selectable for its casters (see WarriorEditDialogViewModel.AddSpell) and which
/// other bands could be granted the same school (e.g. adding Necromancy to a non-Undead band), instead
/// of relying on matching strings typed identically in several places.
/// </summary>
public class MagicSchool
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
