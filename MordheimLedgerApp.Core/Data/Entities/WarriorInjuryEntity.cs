using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Join row between a Warrior and an Injury — see Models.WarriorInjury.</summary>
public class WarriorInjuryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorId { get; set; }

    public int InjuryId { get; set; }
}
