namespace MordheimLedgerApp.Core.Models;

/// <summary>
/// A single member of a warband — Hero or Henchman alike (both are tracked individually here,
/// unlike the tabletop rule where Henchmen only advance as a group).
/// WarriorArchetypeId records which template this warrior was recruited from (for display and for
/// MaxCount tracking) — the stat fields below are the warrior's own copy, seeded from that
/// archetype at recruitment, and then advance independently via Experience.
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

    /// <summary>Null = not mounted. Resolved separately from the Mount catalog by WarbandService (not a
    /// join table - a warrior can only ride one mount at a time, picking a new one replaces this).</summary>
    public Library.Mount? Mount { get; set; }
}
