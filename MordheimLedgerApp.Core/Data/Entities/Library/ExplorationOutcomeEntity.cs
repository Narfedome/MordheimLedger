using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Child row of one ExplorationResultEntity - a plain 1-N table, not a join, see
/// Models.Library.ExplorationOutcome.</summary>
public class ExplorationOutcomeEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ExplorationResultId { get; set; }

    public int? SubRollMin { get; set; }
    public int? SubRollMax { get; set; }
    public ExplorationOutcomeKind Kind { get; set; }
    public string? GoldFormula { get; set; }
    public string? EquipmentItemName { get; set; }
    public string? ItemQuantityFormula { get; set; }
    public string? FoundValueFormula { get; set; }
    public string? MaterialRuleName { get; set; }
    public string? SecondaryEquipmentItemName { get; set; }
    public string? AlternativeEquipmentItemName { get; set; }
    public string? Note { get; set; }
    public bool? StatTestPass { get; set; }
    public bool CausesSickness { get; set; }
    public bool RequiresDoubleRoll { get; set; }
    public bool CausesDeath { get; set; }
    public bool TriggersArtefactRoll { get; set; }

    /// <summary>Comma-separated English WarbandArchetype.Name(s) - see Models.Library.ExplorationOutcome.
    /// RestrictedToWarbandArchetypeNames. Null/empty = the catch-all branch.</summary>
    public string? RestrictedToWarbandArchetypeNamesCsv { get; set; }

    public bool GrantsNextExplorationBonusDie { get; set; }
    public int? GrantsLeaderExperience { get; set; }
    public string? GrantsFreeHenchmanArchetypeName { get; set; }
}
