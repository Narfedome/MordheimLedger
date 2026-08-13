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

    /// <summary>Null = no recruitment floor tracked. Non-null only for a warband's sole "no more, no
    /// less" leader type (e.g. the Vampire for Undead), where it equals MaxCount (exactly one
    /// required) - never set for merely-capped types (Dregs, Dire Wolves...), where 0 is valid.</summary>
    public int? MinCount { get; set; }

    public int Movement { get; set; }

    /// <summary>Non-null overrides the displayed Movement value with free text (e.g. "2D6" for Cave
    /// Squigs, whose Movement isn't a fixed characteristic) - Movement itself is kept as a numeric
    /// fallback/sort key. See MovementDisplay.</summary>
    public string? MovementOverride { get; set; }

    /// <summary>What the UI should actually show for Movement - MovementOverride if set, otherwise the
    /// plain numeric Movement.</summary>
    public string MovementDisplay => MovementOverride ?? Movement.ToString();

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

    /// <summary>True = this archetype may learn spells. Which magic school(s) it can actually pick from
    /// is not tracked here - it's whatever WarbandArchetype.MagicSchools grants to the parent band (see
    /// WarbandDetailViewModel.LoadAsync), since in the rulebook a spell list belongs to the warband, not
    /// to one specific Hero type within it.</summary>
    public bool IsSpellcaster { get; set; }

    /// <summary>True for Mutant/Possessed-type archetypes that may buy Mutations at recruitment (see
    /// RulesReference/Campagne.md - "Mutants et Possédés ne peuvent acheter une mutation qu'au
    /// recrutement"). Gates the Mutations tab on WarriorEditDialog - false for every ordinary archetype.</summary>
    public bool CanBuyMutations { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Which of the parent WarbandArchetype's EquipmentLists this archetype's Weapons/Armour
    /// line draws from (e.g. Skaven's Night Runners, a Hero, use the Henchmen list) - null = this
    /// archetype never uses equipment (Ghouls, Zombies, Trolls, Cave Squigs...). Copied onto Warrior at
    /// recruitment (see EntityMapping.ToWarrior), same snapshot-at-recruit-time convention as Cost/the
    /// stat line - editing this later doesn't retroactively change already-recruited warriors.</summary>
    public int? EquipmentListId { get; set; }

    /// <summary>Which of the 6 rulebook Skill lists this archetype may pick an Advance from (its row of
    /// the warband's "skill table", e.g. a Witch Hunter Captain gets all 5 standard categories, a Bear
    /// Tamer only Combat/Strength/Speed) - empty = not seeded/unknown, not "may pick nothing". Data-only,
    /// same "no rules engine V1" convention as the other Restricted* lists elsewhere: not enforced in the
    /// Skill picker, just informative.</summary>
    public List<SkillCategory> AllowedSkillCategories { get; set; } = new();

    /// <summary>True for "large creature" archetypes (Rat Ogre, Ogre, Troll...) - the rulebook's Warband
    /// Rating formula counts these as a flat 20 points instead of the usual 5 per warrior (see
    /// RulesReference "Starting a Warband" - "calculate the warband rating"). Copied onto Warrior at
    /// recruitment (see ToWarrior), same snapshot-at-recruit-time convention as the rest of the stat
    /// line.</summary>
    public bool IsLargeCreature { get; set; }
}
