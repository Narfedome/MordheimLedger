using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components.FormPage;

/// <summary>Ouvre un formulaire plein écran et attend sa fermeture - pendant de DialogStack.PushAsync pour les
/// formulaires de création/modification, qui quittent les dialogs pour des pages Shell (décision de
/// l'utilisateur, 2026-11-10 - revient sur le choix d'août des dialogs d'édition). Même contrat que
/// ShowDialogAsync : le résultat se résout quand le ViewModel se ferme (Enregistrer/Annuler/✕/retour
/// Android), la page est alors dépilée.</summary>
public static class FormNavigator
{
    public static async Task<TResult?> PushAsync<TResult>(FormContent<TResult> content)
    {
        var formName = content.GetType().Name;
        var viewModel = (DialogViewModel<TResult>)content.BindingContext!;
        var shell = Shell.Current;
        var tcs = new TaskCompletionSource<TResult?>();
        var handled = false;
        var removedNatively = false;

        void OnClose(TResult result)
        {
            if (handled) return;
            handled = true;
            viewModel.CloseRequested -= OnClose;
            tcs.TrySetResult(result);
        }
        viewModel.CloseRequested += OnClose;

        var page = new FormPage(content, () => viewModel.CancelCommand.Execute(null));

        // Filet de sécurité : page retirée par Shell lui-même (re-tap sur l'onglet courant, qui revient à la
        // racine) sans passer par Enregistrer/Annuler - trop tard pour demander quoi que ce soit, on annule
        // comme DialogStack.OnModalPopped. Parent nul plutôt que « absente de la pile courante » : changer
        // d'onglet laisse le formulaire en place dans le sien, ce n'est pas une fermeture.
        void OnNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            if (handled || page.Parent is not null) return;
            removedNatively = true;
            viewModel.ForceCancel();
        }
        shell.Navigated += OnNavigated;

        viewModel.CaptureInitialState();
        await DialogNavigationGate.RunAsync(() => shell.Navigation.PushAsync(page, animated: true), $"FormNavigator.Push({formName})");

        var result = await tcs.Task;
        shell.Navigated -= OnNavigated;
        if (!removedNatively && page.Parent is not null)
        {
            await DialogNavigationGate.RunAsync(() => RemoveFormPageAsync(shell, page), $"FormNavigator.Pop({formName})");
        }
        return result;
    }

    /// <summary>Retire la page de formulaire de la pile de l'onglet qui la contient vraiment. Plantage remonté par
    /// Google Play (2026-11-28) : `page.Navigation.RemovePage` sur une page qui n'était plus dans la pile de son onglet
    /// (Shell l'avait déjà retirée sans lui ôter son Parent) remontait jusqu'à la navigation de la fenêtre, qui
    /// refuse RemovePage (InvalidOperationException, appli fermée). On cherche donc l'onglet dont la pile contient la
    /// page ; aucun = déjà retirée, rien à faire. Le nettoyage ne doit jamais faire planter l'appli.</summary>
    private static async Task RemoveFormPageAsync(Shell shell, Page page)
    {
        try
        {
            var owner = shell.Items.SelectMany(item => item.Items)
                .Select(section => section.Navigation)
                .FirstOrDefault(navigation => navigation.NavigationStack.Contains(page));
            if (owner is null) return;

            var stack = owner.NavigationStack;
            if (ReferenceEquals(stack[^1], page)) await owner.PopAsync(animated: true);
            else owner.RemovePage(page);
        }
        catch (InvalidOperationException ex)
        {
            CrashLogger.LogException("FormNavigator.RemoveFormPageAsync", ex);
        }
    }
}
