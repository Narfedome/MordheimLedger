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
}
