namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>
/// Ouvre chaque dialog (voir BaseViewModel.ShowDialogAsync) comme une vraie Page modale
/// (DialogPage) via Navigation.PushModalAsync/PopModalAsync plutôt qu'un CommunityToolkit.Maui Popup.
/// Un dialog qui en ouvre un autre (ex. MagicSchoolEditDialog -> "Ajouter un sort" -> SpellEditDialog,
/// ou WarbandArchetypeEditDialog -> EditWarrior -> chip détail, jusqu'à 3 niveaux) empile alors
/// plusieurs vraies Pages sur la pile modale native de MAUI, gérée par le framework lui-même (LIFO
/// garanti) - pas de bookkeeping maison. C'est délibérément un remplacement du Popup du toolkit : sur
/// WinUI, empiler 2 Popup CommunityToolkit.Maui casse le premier (sourd aux clics une fois le second
/// fermé - régression connue, voir issues #2774/#2557/#1931), et même un seul Popup réutilisé comme
/// conteneur générique s'est mal comporté (auto-dimensionnement au contenu au lieu de pleine fenêtre).
/// Une Page poussée modalement n'a aucune de ces deux ambiguïtés.
/// </summary>
public sealed class DialogStack
{
    public static DialogStack Instance { get; } = new();

    private DialogStack() { }

    /// <summary>Pousse le contenu donné comme sa propre DialogPage modale. Le résultat se résout quand
    /// CE dialog précis se ferme (Save/Cancel/tap sur le fond) - la Page se dépile alors, révélant
    /// exactement ce qu'il y avait en dessous (page normale ou dialog parent), géré par la pile modale
    /// native.</summary>
    public async Task<TResult?> PushAsync<TResult>(DialogContent<TResult> content, Page currentPage)
    {
        var dialogName = content.GetType().Name;
        var viewModel = (DialogViewModel<TResult>)content.BindingContext!;
        var tcs = new TaskCompletionSource<TResult?>();
        var handled = false;
        var poppedNatively = false;

        void OnClose(TResult result)
        {
            // Garde-fou : un double Save/Cancel avant que la Page n'ait fini de se dépiler ne doit pas
            // déclencher deux PopModalAsync (couvre aussi le cas où OnModalPopped ci-dessous et un
            // Enregistrer/Annuler explicite se produisent quasi en même temps).
            if (handled) return;
            handled = true;

            viewModel.CloseRequested -= OnClose;
            tcs.TrySetResult(result);
        }

        viewModel.CloseRequested += OnClose;

        var dialogPage = new DialogPage(content, () => viewModel.CancelCommand.Execute(null));

        // Filet de sécurité (même idiome que les ~10 XxxPickerService) : si la page est dépilée par le
        // bouton/geste retour d'Android plutôt que par Enregistrer/Annuler/tap sur le fond, aucun
        // CloseRequested n'est jamais levé - sans ce filet, tcs.Task ne se résoudrait jamais et
        // l'appelant resterait bloqué indéfiniment. On réutilise CancelCommand (même résultat qu'un tap
        // sur le fond) plutôt que résoudre tcs directement, pour garder un seul chemin de fermeture -
        // poppedNatively empêche alors le PopModalAsync plus bas de dépiler une SECONDE fois (la page
        // est déjà partie).
        var window = currentPage.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, dialogPage)) return;
            if (window is not null) window.ModalPopped -= OnModalPopped;
            poppedNatively = true;
            viewModel.CancelCommand.Execute(null);
        }
        if (window is not null) window.ModalPopped += OnModalPopped;

        // Sérialisé (voir DialogNavigationGate) : un XxxPickerService peut pousser sa propre page modale
        // pendant que CE PushModalAsync est encore en train de s'installer (bouton du dialog lui-même
        // qui ouvre un picker) - deux Push/PopModalAsync concurrents sur la même pile sont un piège
        // MAUI/Shell connu.
        await DialogNavigationGate.RunAsync(() => currentPage.Navigation.PushModalAsync(dialogPage, animated: false), $"DialogStack.Push({dialogName})");

        var result = await tcs.Task;
        if (window is not null) window.ModalPopped -= OnModalPopped;
        if (!poppedNatively)
            await DialogNavigationGate.RunAsync(() => currentPage.Navigation.PopModalAsync(animated: false), $"DialogStack.Pop({dialogName})");
        return result;
    }
}
