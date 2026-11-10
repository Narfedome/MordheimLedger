using MordheimLedgerApp.Components.Dialogs;

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
            await DialogNavigationGate.RunAsync(() =>
            {
                var stack = page.Navigation.NavigationStack;
                if (stack.Count > 0 && ReferenceEquals(stack[^1], page))
                    return page.Navigation.PopAsync(animated: true);
                page.Navigation.RemovePage(page);
                return Task.CompletedTask;
            }, $"FormNavigator.Pop({formName})");
        }
        return result;
    }
}
