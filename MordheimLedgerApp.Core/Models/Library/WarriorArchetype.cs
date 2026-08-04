namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A recruitable warrior "type" within a WarbandArchetype (e.g. "Mercenary Captain" under
/// Reiklander Mercenaries). Its stat line and cost are the pre-fill values copied onto a new
/// Warrior when recruited — the Warrior then advances independently from this template, so editing
/// this archetype later only affects warriors recruited afterwards.
/// </summary>
public class WarriorArchetype
{
    public int Id { get; set; }
    public int WarbandArchetypeId { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync/SetTranslationAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public bool IsHero { get; set; }
    public int Cost { get; set; }
    public ContentSource Source { get; set; }

    /// <summary>Null = no recruitment cap tracked (e.g. "0-1 per warband").</summary>
    public int? MaxCount { get; set; }

    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    /// <summary>
    /// XP a newly recruited warrior of this type starts with (e.g. a Witch Hunter Captain starts at
    /// 20 XP) — already reflected in the stat line above, not something the app re-derives. Most
    /// generic warrior types start at 0.
    /// </summary>
    public int StartingExperience { get; set; }

    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
