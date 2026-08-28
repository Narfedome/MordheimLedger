using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class HiredSwordEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public int HireCost { get; set; }
    public int Upkeep { get; set; }
    public int BaseRating { get; set; }
    public string? DescriptionKey { get; set; }
    public ContentSource Source { get; set; }
    public string? ImagePath { get; set; }

    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    /// <summary>CSV of SkillCategory member names - same convention as WarriorArchetypeEntity.</summary>
    public string? AllowedSkillCategories { get; set; }

    /// <summary>See Models.Library.HiredSword.MagicSchoolId. Null for almost every Hired Sword.</summary>
    [Indexed]
    public int? MagicSchoolId { get; set; }
}
