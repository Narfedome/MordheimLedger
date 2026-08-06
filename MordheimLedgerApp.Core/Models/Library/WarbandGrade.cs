namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// Official quality/provenance tier a warband was published under (mordheimer.net's classification -
/// the sole reference source retained for this, see CLAUDE.md). Core = the original Mordheim
/// Rulebook; Grade1a = GW/Fanatic material deemed "official" in the 2005 Rules Review; Grade1b/1c/2a
/// are progressively less official (fan-made, tested by the community). All 15 warbands seeded so far
/// are Core or Grade1a - the later tiers exist for future imports.
/// </summary>
public enum WarbandGrade
{
    Core = 0,
    Grade1a = 1,
    Grade1b = 2,
    Grade1c = 3,
    Grade2a = 4
}
