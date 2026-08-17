namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// Reference lookup for the rulebook's Heroes' Serious Injury table (D66: two D6, first die = tens
/// digit) - Henchmen use a completely different, simpler mechanic, see HenchmanInjuryTable. Pure
/// flavor/reference text - deliberately does not mutate any Warrior stat itself (see the roadmap's
/// "no rules engine in V1" boundary, also documented on Models/WarriorStatus.cs): the resulting
/// text becomes (find-or-create) a Library Injury linked onto the Warrior (see
/// WarbandDetailViewModel.EndOfGame). Status IS auto-derived from the roll though (see IsDeath) - the
/// 11-15 results are unambiguously "Mort", so the End of Game wizard sets WarriorStatus.Dead itself
/// instead of asking the player to also pick it by hand. TryGetTextKey returns a localization resource
/// key rather than resolved text - Core stays MAUI/localization-free, the caller (App layer) resolves
/// the key via LocalizationService.
///
/// Verified against the rulebook (p. 118-119) via RulesReference/Campagne.md. The "Blessures
/// multiples" result (16, 21) means rolling 1D6 to determine how many more sub-rolls to make on this
/// same table (not a fixed count) - the End of Game wizard has the player roll that 1D6 itself (see
/// EndOfGameDialogViewModel.WarriorOutcomeRow.SetMultipleInjuryCount), then resolves that many
/// sub-rolls (MultipleInjuryRolls), each becoming its own Injury on the warrior alongside the main
/// "Blessures multiples" text. The rulebook says to re-roll any further Dead/Captured/Multiple
/// Injuries sub-result, but the app deliberately does NOT enforce or auto-reroll that itself (explicit
/// decision, 2026-08-17): whatever the player rolls or types in is accepted as-is, same as every other
/// injury result. Still no stat mutation from the sub-rolls' own effects (leg wound, arm wound, etc.)
/// - same "no rules engine" boundary as the main table.
/// </summary>
public static class SeriousInjuryTable
{
    private static readonly int[] Rolls =
    [
        11, 12, 13, 14, 15, 16,
        21, 22, 23, 24, 25, 26,
        31, 32, 33, 34, 35, 36,
        41, 42, 43, 44, 45, 46,
        51, 52, 53, 54, 55, 56,
        61, 62, 63, 64, 65, 66
    ];

    private static readonly int[] DeathRolls = [11, 12, 13, 14, 15];
    private static readonly int[] MultipleInjuriesRolls = [16, 21];

    public static bool TryGetTextKey(int roll, out string key)
    {
        if (Array.IndexOf(Rolls, roll) < 0)
        {
            key = string.Empty;
            return false;
        }

        key = $"InjurySerious{roll}";
        return true;
    }

    /// <summary>True for the unambiguous "Mort" results (11-15). 16 ("Blessures multiples") isn't
    /// included even though it could lead to death indirectly - it means rolling twice more, which
    /// this single-roll check can't resolve on its own.</summary>
    public static bool IsDeath(int roll) => Array.IndexOf(DeathRolls, roll) >= 0;

    /// <summary>True for the "Blessures multiples" result itself (16, 21) - triggers a 1D6 roll for
    /// the number of additional sub-rolls to make on this same table.</summary>
    public static bool IsMultipleInjuries(int roll) => Array.IndexOf(MultipleInjuriesRolls, roll) >= 0;

    /// <summary>Rolls two D6 (D66: first die = tens digit).</summary>
    public static int RollDice() => Random.Shared.Next(1, 7) * 10 + Random.Shared.Next(1, 7);
}
