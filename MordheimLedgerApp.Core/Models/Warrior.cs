namespace MordheimLedgerApp.Core.Models;

/// <summary>
/// A single warband member (Hero) OR an entire Henchman group (rulebook: "Henchmen groups gain
/// experience collectively and gain advances together" - a group is mechanically one entity, not N
/// individuals). WarriorArchetypeId records which template this row was recruited from (for display
/// and for MaxCount tracking) — the stat fields below are its own copy, seeded from that archetype at
/// recruitment, then advancing via Experience (individually for a Hero, collectively for the whole
/// Henchman group this row represents).
/// </summary>
public class Warrior
{
    public int Id { get; set; }
    public int WarbandId { get; set; }
    public int WarriorArchetypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsHero { get; set; }
    public int Cost { get; set; }
    public int Experience { get; set; }
    public WarriorStatus Status { get; set; } = WarriorStatus.Active;

    /// <summary>Always 1 for a Hero (meaningless otherwise). For a Henchman group, how many living
    /// models remain in it - the whole point of the group being one row instead of N: casualties just
    /// decrement this (down to deletion at 0), while XP/equipment/skills stay shared across whoever's
    /// left, matching the rulebook rather than the historical "Status.Dead per individual" model.</summary>
    public int HeadCount { get; set; } = 1;

    public int Movement { get; set; }

    /// <summary>Non-null overrides the displayed Movement value with free text (e.g. "2D6" for Cave
    /// Squigs, whose Movement isn't a fixed characteristic) - copied from the recruiting
    /// WarriorArchetype, see WarriorArchetype.MovementOverride/MovementDisplay.</summary>
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

    /// <summary>Which EquipmentList this warrior may buy starting equipment from - copied from the
    /// recruiting WarriorArchetype at recruitment (see WarriorArchetype.EquipmentListId), null = no
    /// equipment usable. Editing the archetype's list later doesn't retroactively change this.</summary>
    public int? EquipmentListId { get; set; }

    /// <summary>Which of the 6 rulebook Skill lists this warrior may pick an Advance from - copied from
    /// the recruiting WarriorArchetype at recruitment (see WarriorArchetype.AllowedSkillCategories), so
    /// the End of Game Advance skill picker can filter to it (see EndOfGameDialogViewModel.
    /// PickAdvanceSkill). Empty = not seeded/unknown, not "may pick nothing".</summary>
    public List<Library.SkillCategory> AllowedSkillCategories { get; set; } = new();

    /// <summary>Loaded separately via the Warrior/EquipmentItem join table — not persisted on this object.</summary>
    public List<WarriorEquipment> Equipment { get; set; } = new();

    /// <summary>Loaded separately via the Warrior/Skill join table — not persisted on this object.</summary>
    public List<WarriorSkill> Skills { get; set; } = new();

    /// <summary>Loaded separately via the Warrior/Injury join table — not persisted on this object. Both
    /// the End of Game Serious Injury roll and manual additions (WarriorEditDialog) go through this
    /// same list — see WarbandDetailViewModel.EndOfGame's find-or-create-by-name lookup.</summary>
    public List<WarriorInjury> Injuries { get; set; } = new();

    /// <summary>Loaded separately via the Warrior/Spell join table — not persisted on this object. Which
    /// specific spells this warrior has learned from its band's granted magic school(s) (a caster
    /// doesn't know the whole table at once, see WarriorSpell) — empty for non-casters.</summary>
    public List<WarriorSpell> Spells { get; set; } = new();

    /// <summary>Loaded separately via the Warrior/Mutation join table — not persisted on this object.
    /// Only meaningful for warriors whose archetype has CanBuyMutations set — empty otherwise.</summary>
    public List<WarriorMutation> Mutations { get; set; } = new();

    /// <summary>Null = no animal assigned. Resolved separately from the Animal catalog by WarbandService
    /// (not a join table - a warrior can only have one animal at a time, picking a new one replaces this).</summary>
    public Library.Animal? Animal { get; set; }

    /// <summary>Copied from the recruiting WarriorArchetype - see WarriorArchetype.IsLargeCreature.</summary>
    public bool IsLargeCreature { get; set; }
}
