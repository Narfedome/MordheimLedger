using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Join row between a Warrior and its "Rancune" target — see Models.WarriorHatred. Exactly one
/// of TargetWarbandArchetypeId/TargetFreeText is set - no CHECK constraint enforced at the DB level,
/// same "no rules engine V1" trust-the-app-layer stance as elsewhere.</summary>
public class WarriorHatredEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorId { get; set; }

    public int? TargetWarbandArchetypeId { get; set; }
    public string? TargetFreeText { get; set; }
}
