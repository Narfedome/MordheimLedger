using MordheimLedgerApp.Core.Models;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

public class WarriorEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandId { get; set; }

    [Indexed]
    public int WarriorArchetypeId { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsHero { get; set; }
    public int Cost { get; set; }
    public int Experience { get; set; }
    public WarriorStatus Status { get; set; } = WarriorStatus.Active;

    public int Movement { get; set; }
    public string? MovementOverride { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    /// <summary>Null = no animal assigned. The warrior's stats aren't merged with the animal's - it's
    /// tracked as its own separate profile, resolved from AnimalEntity by WarbandService (see
    /// Models.Warrior.Animal).</summary>
    public int? AnimalId { get; set; }

    public int? EquipmentListId { get; set; }

    /// <summary>Comma-separated SkillCategory member names - see Warrior.AllowedSkillCategories /
    /// WarriorArchetypeEntity.AllowedSkillCategories (same storage convention, copied at recruitment).</summary>
    public string? AllowedSkillCategories { get; set; }
}
