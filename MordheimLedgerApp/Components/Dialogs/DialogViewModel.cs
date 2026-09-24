using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Components.Dialogs
{
    /// <summary>
    /// Common base for dialog ViewModels (Confirm/Prompt/ActionSheet/...): each one redeclared the
    /// same CloseRequested event and the same Cancel command invoking it with a fixed "cancelled"
    /// value (false, null, -1...) — only the confirmation logic actually differs per dialog.
    /// </summary>
    public abstract partial class DialogViewModel<TResult> : BaseViewModel
    {
        /// <summary>Signals the XAML wrapper (ConfirmDialog, PromptDialog...) to close.</summary>
        public event Action<TResult>? CloseRequested;

        protected void Close(TResult result) => CloseRequested?.Invoke(result);

        /// <summary>Value sent on cancel-close (false, null, -1 depending on the dialog).</summary>
        protected abstract TResult CancelResult { get; }

        /// <summary>Annuler (bouton, tap sur le fond, bouton retour Android - voir DialogPage) : demande
        /// confirmation seulement si l'état éditable a changé depuis l'ouverture (voir EditableState) -
        /// jamais pour un dialog qui n'en déclare pas (Confirm/Prompt/lecture seule...). AsyncRelayCommand
        /// n'accepte qu'une exécution à la fois : un second tap sur le fond pendant la confirmation est
        /// ignoré plutôt que d'empiler une deuxième confirmation.</summary>
        [RelayCommand]
        public async Task Cancel()
        {
            if (HasUnsavedChanges() && !await ConfirmAsync(Loc["DialogDiscardChangesTitle"], Loc["DialogDiscardChangesMessage"]))
                return;
            Close(CancelResult);
        }

        /// <summary>Fermeture sans confirmation - seulement quand la page du dialog a déjà été dépilée par
        /// la plateforme elle-même (DialogStack.OnModalPopped) : trop tard pour demander quoi que ce soit.</summary>
        internal void ForceCancel() => Close(CancelResult);

        // --- Détection des modifications non enregistrées (2026-09-24, retour utilisateur) ---------------

        private static readonly JsonSerializerOptions SnapshotOptions = new()
        {
            // DramatisPersona.PairedWithDramatisPersona est auto-référent, et les modèles se croisent
            // (objet -> règles spéciales...) - on veut les valeurs, pas un graphe fidèle.
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        private string? _initialSnapshot;

        /// <summary>Ce que l'utilisateur édite dans ce dialog et qui sera perdu en annulant - null (défaut)
        /// = pas de confirmation à l'annulation. Chaque dialog d'édition renvoie l'objet édité (Item, lié
        /// directement aux champs) plus tout état tenu à part jusqu'à l'enregistrement (listes de puces,
        /// restrictions, saisie de Mouvement...). Doit rester sérialisable en JSON : modèles Core et
        /// projections de valeurs simples, jamais de ViewModel/commande.</summary>
        protected virtual object? EditableState => null;

        /// <summary>Instantané de référence - pris par DialogStack juste avant l'affichage, et repris par
        /// RunBaselineNeutralAsync après un chargement différé.</summary>
        internal void CaptureInitialState() => _initialSnapshot = SerializeEditableState();

        protected bool HasUnsavedChanges()
        {
            if (_initialSnapshot is null) return false;
            return SerializeEditableState() != _initialSnapshot;
        }

        /// <summary>Pour un chargement déclenché par le dialog lui-même APRÈS son ouverture (onglet chargé
        /// à la demande, lignes de recrutement...) : ce qu'il ajoute à EditableState n'est pas une
        /// modification de l'utilisateur. Si rien n'avait changé avant le chargement, l'instantané de
        /// référence est repris après ; sinon il est gardé tel quel (le dialog est déjà modifié de toute
        /// façon).</summary>
        protected async Task RunBaselineNeutralAsync(Func<Task> load)
        {
            var wasClean = !HasUnsavedChanges();
            await load();
            if (wasClean && _initialSnapshot is not null) CaptureInitialState();
        }

        private string? SerializeEditableState()
        {
            if (EditableState is not { } state) return null;
            try
            {
                return JsonSerializer.Serialize(state, SnapshotOptions);
            }
            catch (Exception)
            {
                // État non sérialisable (ne devrait pas arriver, voir EditableState) : on préfère une
                // confirmation de trop à une saisie perdue sans prévenir - valeur unique à chaque appel,
                // donc toujours "modifié".
                return Guid.NewGuid().ToString();
            }
        }

        /// <summary>Opens the shared chip mini-popup (Name+Description) - every chip-tap command in the
        /// app funnels through here instead of duplicating the ShowDialogAsync/ChipDetailDialog wiring.
        /// Lives on the generic base (not just ReadOnlyDialogViewModel) since editable dialogs
        /// (WarbandArchetypeEditDialogViewModel) preview chips the same way while still editing them.</summary>
        protected async Task ShowChipDetailAsync(string name, string? description) =>
            await ShowChipDetailAsync(name, description, null);

        /// <summary>Same popup, plus a read-only list of Spells shown underneath - only MagicSchool chips
        /// pass a non-null/non-empty list (see WarbandArchetypeDetailDialogViewModel/
        /// WarbandArchetypeEditDialogViewModel.ShowMagicSchoolDetail); every other chip type keeps using
        /// the 2-arg overload above, unaffected.</summary>
        protected async Task ShowChipDetailAsync(string name, string? description, IReadOnlyList<Spell>? relatedSpells) =>
            await ShowDialogAsync(new ChipDetailDialog(new ChipDetailDialogViewModel(name, description, relatedSpells)));
    }
}
