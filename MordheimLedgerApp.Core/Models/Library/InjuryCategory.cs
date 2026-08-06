namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>Which Serious Injuries table (see the rulebook's Post-Battle Sequence) an Injury belongs
/// to - Heroes roll on a D66 chart, Henchmen on a much simpler D6 chart, and the two never mix.</summary>
public enum InjuryCategory
{
    Hero = 0,
    Henchman = 1
}
