using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class WarriorArchetypeEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public string NameKey { get; set; } = string.Empty;
    public bool IsHero { get; set; }
    public int Cost { get; set; }
    public ContentSource Source { get; set; }
    public int? MaxCount { get; set; }
    public int? MinCount { get; set; }

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
    public int StartingExperience { get; set; }

    public string? DescriptionKey { get; set; }
    public bool IsSpellcaster { get; set; }
    public bool CanBuyMutations { get; set; }
    public string? ImagePath { get; set; }
    public int? EquipmentListId { get; set; }
    public bool CanUseEquipment { get; set; } = true;

    /// <summary>Comma-separated SkillCategory member names (e.g. "Combat,Strength,Speed") - see
    /// WarriorArchetype.AllowedSkillCategories. A plain delimited column rather than a join table since
    /// SkillCategory is a small fixed enum, not a foreign catalog to look up.</summary>
    public string? AllowedSkillCategories { get; set; }
    public bool IsLargeCreature { get; set; }
}
