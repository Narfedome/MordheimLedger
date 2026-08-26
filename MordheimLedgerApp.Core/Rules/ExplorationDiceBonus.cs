using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Extra dice a warband rolls on its post-battle Exploration roll from whatever its warriors
/// currently carry - e.g. the All-seeing Eye of Numas (Œil Omniscient de Numas) grants +1 (see
/// EquipmentItem.GrantsBonusExplorationDice). Fed into Core.Rules.ExplorationChart.ComputeDiceCount's
/// bonusDice parameter, computed live from carried equipment rather than baked into the Warband at find
/// time - same carried-equipment idiom as SkillEligibility/RareItemSearchBonus.</summary>
public static class ExplorationDiceBonus
{
    public static int EffectiveBonusDice(IEnumerable<Warrior> warriors) =>
        warriors.SelectMany(w => w.Equipment).Sum(e => e.Item.GrantsBonusExplorationDice ?? 0);
}
