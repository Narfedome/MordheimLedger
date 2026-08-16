namespace MordheimLedgerApp.Core.Rules;

/// <summary>Livre des règles : "A Wizard starts with one spell, determined randomly - roll 1D6 on the
/// appropriate list." Même mécanique de jet pour un nouveau sort obtenu à la place d'une compétence à
/// l'Avancement (livre des règles, non encore câblé côté UI - voir EndOfGameDialogViewModel) : les deux
/// réutiliseront ce même RollDice le jour où cette 2e règle sera implémentée.</summary>
public static class SpellRules
{
    public static int RollDice() => Random.Shared.Next(1, 7);
}
