namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A warband's race/species (Humain, Skaven, Orque, Nain, Elfe, Mort-Vivant, Homme-Bête...) -
/// WarbandArchetype.RaceId points to one. Introduced 2026-08-20 for the Prisonniers "escort as a
/// Henchman if you can equip him" branch, whose book text only allows joining "one of your human
/// Henchman groups" - a band with no human Henchmen (Skaven, Orc, Dwarf...) simply can't recruit that
/// way, only take the gold. Same "user-editable Library catalog" shape as MagicSchool (Official/
/// Modified/Custom, no restriction-list fields of its own) rather than a hardcoded enum, since the
/// user explicitly wants to add/rename races later without a code change.
/// </summary>
public class Race
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
}
