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

    /// <summary>Rules specific to this one warrior type (e.g. "Chef", "Provoque la Peur", "Pièges") -
    /// distinct from the parent WarbandArchetype's own SpecialRules, which apply band-wide regardless of
    /// warrior type. Each SpecialRule is a shared catalog entry (see WarriorArchetypeSpecialRuleEntity
    /// join) rather than free text, so e.g. "Chef" reads identically on every archetype that has it.</summary>
    public List<SpecialRule> SpecialRules { get; set; } = new();

    /// <summary>Null = not a spellcaster. Non-null = this archetype rolls on Spell entries whose
    /// SpellListName matches this value (e.g. "Nécromancie" for a Nécromancien). A simple string match
    /// rather than a join table since it's a 1:1 relationship (one archetype -> one list).</summary>
    public string? SpellListName { get; set; }

    /// <summary>True for Mutant/Possessed-type archetypes that may buy Mutations at recruitment (see
    /// RulesReference/Campagne.md - "Mutants et Possédés ne peuvent acheter une mutation qu'au
    /// recrutement"). Gates the Mutations tab on WarriorEditDialog - false for every ordinary archetype.</summary>
    public bool CanBuyMutations { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
