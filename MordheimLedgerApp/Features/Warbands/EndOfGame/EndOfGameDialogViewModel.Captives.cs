using CommunityToolkit.Mvvm.Input;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Prisonniers ennemis" - une seule étape pour toute la bande (voir la doc de
/// EndOfGameDialogViewModel.Steps), demandée par l'utilisateur (2026-08-27) en repensant Capturé (61) :
/// le capteur d'un DE NOS guerriers est une bande adverse non modélisée, mais l'inverse (nous capturons
/// des héros adverses) ne dépend que de NOTRE bande, dont le type est bien connu - d'où le picker de
/// destin filtré par IsUndeadWarband/IsPossessedWarband plutôt qu'une simplification symétrique.</summary>
public partial class EndOfGameDialogViewModel
{
    private bool ValidateCaptivesStep()
    {
        if (!HasCapturedEnemies) return true;

        var valid = true;
        foreach (var entry in CapturedEnemies)
        {
            valid &= CheckRoll(entry.SelectedFate is null, () => entry.FateError = Loc["EndOfGameRollRequired"]);
            if (entry.ShowGoldAmount)
                valid &= CheckRoll(!entry.HasValidGoldAmount, () => entry.FateError = Loc["EndOfGameRollRequired"]);
        }
        return valid;
    }

    [RelayCommand]
    private void IncrementCapturedEnemyCount() => CapturedEnemyCount = Math.Min(6, CapturedEnemyCount + 1);

    [RelayCommand]
    private void DecrementCapturedEnemyCount() => CapturedEnemyCount = Math.Max(1, CapturedEnemyCount - 1);

    // Formule du livre pour "Vendu aux esclavagistes" (1D6x5 CO) - le champ reste modifiable ensuite pour
    // un jet physique, même convention que le reste du wizard.
    [RelayCommand]
    private void AutoRollSoldToSlavers(CapturedEnemyEntry entry) => entry.GoldAmount = (Random.Shared.Next(1, 7) * 5).ToString();
}
