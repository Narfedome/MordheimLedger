using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Join row between a Warrior and a Mutation — see Models.WarriorMutation.</summary>
public class WarriorMutationEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorId { get; set; }

    public int MutationId { get; set; }
}
