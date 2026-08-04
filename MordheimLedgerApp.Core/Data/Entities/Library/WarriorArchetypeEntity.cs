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

    public int Movement { get; set; }
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
    public string? ImagePath { get; set; }
}
