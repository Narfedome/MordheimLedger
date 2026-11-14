using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

/// <summary>Garde des tâches lancées par l'interface sans appelant pour récupérer leur exception : gestionnaires
/// <c>async void</c> (OnAppearing, OnNavigatedTo, événements) et chargements lancés sans être attendus. Sans elle,
/// une exception dans un <c>async void</c> fait planter l'appli, et une tâche jamais attendue échoue en silence
/// (écran vide). Ici l'erreur est toujours écrite dans le journal (CrashLogger), et pour un chargement de données le
/// joueur voit un message plutôt qu'un plantage ; les effets purement visuels (défilement, fondu) restent muets.</summary>
public static class UiErrors
{
    // Contextes dont l'échec a déjà été montré et qui n'ont pas réussi depuis. Fermer le message relance
    // OnNavigatedTo/OnAppearing de l'écran (fermeture d'un popup), donc son chargement, qui échouerait de nouveau :
    // sans ce garde-fou, chaque OK rouvrirait aussitôt le même message (vu sur Android, liste des bandes).
    private static readonly HashSet<string> Reported = [];

    /// <summary>Exécute work ; une exception est journalisée (et affichée si showMessage), jamais relancée.</summary>
    public static async Task RunAsync(Func<Task> work, string context, bool showMessage = true)
    {
        try
        {
            await work();
            lock (Reported) Reported.Remove(context);
        }
        catch (Exception ex)
        {
            await ReportAsync(ex, context, showMessage);
        }
    }

    public static async Task ReportAsync(Exception ex, string context, bool showMessage = true)
    {
        CrashLogger.LogException(context, ex);
        if (!showMessage) return;
        lock (Reported)
            if (!Reported.Add(context)) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = (Page?)Shell.Current ?? Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page is null) return;
                var loc = LocalizationService.Instance;
                var dialog = new ConfirmDialog(new ConfirmDialogViewModel(
                    loc["ErrorTitle"], string.Format(loc["UnexpectedErrorMessage"], ex.Message), loc["DialogOk"], null));
                await DialogStack.Instance.PushAsync(dialog, page);
            });
        }
        catch (Exception dialogError)
        {
            // Le message lui-même ne doit jamais devenir une nouvelle source de plantage.
            CrashLogger.LogException($"{context} (message d'erreur)", dialogError);
        }
    }
}
