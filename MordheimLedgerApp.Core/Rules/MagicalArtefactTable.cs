namespace MordheimLedgerApp.Core.Rules;

/// <summary>The rulebook's fixed 6-item "Magical Artefacts" table, rolled on a plain D6 whenever an
/// Exploration chart entry says to (Villa d'un Noble's 5-6 sub-roll; also referenced by Trésor Caché's
/// "Artefact Magique (5+)" row, not yet wizard-wired since it needs Groupe B/RollsIndependently
/// support first - see ExplorationOutcome.TriggersArtefactRoll). A tiny, immutable, book-fixed lookup
/// - kept as a pure C# table rather than more ExplorationResult/Outcome rows, since it's referenced
/// from more than one chart entry and isn't itself tied to a DiceCount/Value pair. Each item is a real
/// EquipmentItem in the common catalog (Equipment.json), resolved by English name like every other
/// Exploration find. "In a campaign none of these items can appear more than once" is not enforced
/// here - no cross-warband/campaign tracking exists yet, same "no rules engine V1" stance as everything
/// not cheaply computable.</summary>
public static class MagicalArtefactTable
{
    public static string? RollForItemName(int roll) => roll switch
    {
        1 => "Boots and Rope of Pieter",
        2 => "The Count of Ventimiglia's Misericordia",
        3 => "Att'la's Plate Mail",
        4 => "Bow of Seeking",
        5 => "Executioner's Hood",
        6 => "All-seeing Eye of Numas",
        _ => null
    };
}
