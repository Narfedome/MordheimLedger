namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>Which Warrior stat an ExplorationResult's test is rolled against - see
/// ExplorationResult.StatTestField/ExplorationOutcome.StatTestPass. Only the two stats the Exploration
/// chart actually tests (Puits/Endurance, Taverne and Bâtiment Éventré/Commandement) - not a generic
/// reflection-based lookup over every Warrior stat, since only these two are ever needed.</summary>
public enum ExplorationStatField
{
    Toughness = 0,
    Leadership = 1
}
