namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A Hired Sword archetype (e.g. "Pit Fighter") - a mercenary profile from the rulebook, catalogued the
/// same way as Skill/Mutation/EquipmentItem. This pass only needs the catalogue itself and its profile
/// for the "Vendu aux Fosses" (Serious Injury 65) mini-fight comparison - a Hired Sword is NEVER actually
/// recruited into a Warband by this code (that stays a separate future workstream, see
/// SERIOUS_INJURIES_STATUS.md), so there is deliberately no HeadCount/upkeep-tracking/Rating wiring here.
/// </summary>
public class HiredSword
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync/SetTranslationAsync.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One-time cost to hire (gold crowns) - informative only, no recruitment flow reads this yet.</summary>
    public int HireCost { get; set; }

    /// <summary>Per-game upkeep (gold crowns) - informative only, same reason as HireCost.</summary>
    public int Upkeep { get; set; }

    /// <summary>Warband Rating contribution before Experience (e.g. Pit Fighter: +22, "+1 per Experience
    /// point" stays free text in Description - no rules engine V1, and no recruitment flow to feed
    /// anyway).</summary>
    public int BaseRating { get; set; }

    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    // Profile - a Hired Sword always has a full stat line (unlike EquipmentItem's Animal block, which
    // is optional per item). Field names match StatRowView's weak-typing contract exactly.
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    /// <summary>Skill categories this Hired Sword picks from on an Advance (e.g. Pit Fighter: Combat/
    /// Speed/Strength) - same enum as WarriorArchetype.AllowedSkillCategories, informative only.</summary>
    public List<SkillCategory> AllowedSkillCategories { get; set; } = new();

    /// <summary>Fixed starting gear - real EquipmentItem catalogue ids (e.g. Pit Fighter: Morning star,
    /// Helmet, Spiked gauntlet), resolved via HiredSwordEquipmentEntity.</summary>
    public List<int> StartingEquipmentIds { get; set; } = new();

    /// <summary>Empty = hireable by every warband. Non-empty = these WarbandArchetype ids only (e.g. Pit
    /// Fighter: every warband except Undead and Skaven) - same Include/Exclude semantics/UI as Skill/
    /// Mutation/EquipmentItem (see WarbandRestrictionEditor).</summary>
    public List<int> RestrictedToWarbandArchetypeIds { get; set; } = new();

    /// <summary>Named special rules unique to this Hired Sword (e.g. Troll Slayer's "Deathwish"/"Hard to
    /// Kill", Ogre Bodyguard's shared "Causes Fear"/"Large Target") - same find-or-create catalog and
    /// join-table idiom as WarriorArchetype.SpecialRules, resolved/tappable as chips rather than plain
    /// text. Only the clean, single-effect rules go here; a Hired Sword's more elaborate systems (unique
    /// skill lists, a companion profile, a whole market sub-table) stay free text in Description - no
    /// rules engine V1 for those.</summary>
    public List<SpecialRule> SpecialRules { get; set; } = new();
}
