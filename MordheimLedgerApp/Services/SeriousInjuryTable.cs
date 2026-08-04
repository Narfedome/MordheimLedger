namespace MordheimLedgerApp.Services;

/// <summary>
/// Reference lookup for the rulebook's Serious Injury table (D66: two D6, first die = tens digit).
/// Pure flavor/reference text - deliberately does not mutate any Warrior stat itself (see the
/// roadmap's "no rules engine in V1" boundary, also documented on Core/Models/WarriorStatus.cs): the
/// resulting text becomes (find-or-create) a Library Injury linked onto the Warrior (see
/// WarbandDetailViewModel.EndOfGame), Status is chosen by the player in the dialog. Lives in the App
/// layer (not Core) because the entries are looked up through LocalizationService - Core stays
/// MAUI/localization-free.
///
/// IMPORTANT: entered from general knowledge of the Mordheim rulebook, NOT verified against the
/// actual book (same caveat as OfficialContentSeed.cs's Reiklander Mercenaries stats) - the exact
/// roll ranges and wording here are very likely off and need a full rewrite from the real table.
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

    public static bool TryGet(int roll, out string text)
    {
        if (Array.IndexOf(Rolls, roll) < 0)
        {
            text = string.Empty;
            return false;
        }

        text = LocalizationService.Instance[$"InjurySerious{roll}"];
        return true;
    }

    /// <summary>Rolls two D6 (D66: first die = tens digit) and looks up the result.</summary>
    public static (int Roll, string Text) Roll()
    {
        var roll = Random.Shared.Next(1, 7) * 10 + Random.Shared.Next(1, 7);
        TryGet(roll, out var text);
        return (roll, text);
    }
}
