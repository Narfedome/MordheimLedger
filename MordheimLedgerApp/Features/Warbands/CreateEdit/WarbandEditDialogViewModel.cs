using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;
using MordheimLedgerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit
{
    public partial class WarbandEditDialogViewModel : DialogViewModel<bool>
    {
        private const int StepCount = 5;

        private readonly IWarbandArchetypePickerService _warbandArchetypePicker;
        private readonly IWarbandService _warbandService;
        public bool IsWizardMode { get; }
        protected override bool CancelResult => false;

        [ObservableProperty]
        private WarbandArchetype? archetype;

        [ObservableProperty]
        private Warband item;

        private string? nameError;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
        [NotifyPropertyChangedFor(nameof(IsRulesTab))]
        [NotifyPropertyChangedFor(nameof(IsMagicTab))]
        [NotifyPropertyChangedFor(nameof(IsEquipmentTab))]
        [NotifyPropertyChangedFor(nameof(IsWarriorsTab))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        [NotifyPropertyChangedFor(nameof(IsLastStep))]
        [NotifyPropertyChangedFor(nameof(StepLabel))]
        private int selectedTab;
        public bool IsGeneralTab => SelectedTab == 0;
        public bool IsRulesTab => SelectedTab == 1;
        public bool IsMagicTab => SelectedTab == 2;
        public bool IsEquipmentTab => SelectedTab == 3;
        public bool IsWarriorsTab => SelectedTab == 4;

        /// <summary>Mode assistant (IsWizardMode) uniquement : pilote Précédent/le libellé d'étape.</summary>
        public bool CanGoBack => SelectedTab > 0;
        public bool IsLastStep => SelectedTab == StepCount - 1;
        public string StepLabel => string.Format(Loc["LibStepLabel"], SelectedTab + 1, StepCount);

        public WarbandEditDialogViewModel(Warband item, string title, IWarbandArchetypePickerService warbandArchetypePicker, IWarbandService warbandService)
        {
            this.item = item;
            this.title = title;
            this._warbandArchetypePicker = warbandArchetypePicker;
            IsWizardMode = item.Id == 0;
            _warbandService = warbandService;
        }

        [RelayCommand]
        private void ShowGeneralTab() => SelectedTab = 0;

        [RelayCommand]
        private void ShowRulesTab() => SelectedTab = 1;

        [RelayCommand]
        private void ShowMagicTab() => SelectedTab = 2;

        [RelayCommand]
        private async Task ShowEquipmentTab()
        {
            SelectedTab = 3;
        }

        [RelayCommand]
        private async Task ShowWarriorsTab()
        {
            SelectedTab = 4;
        }

        /// <summary>Onglet Général : seul champ obligatoire vérifié pour l'instant (Nom). Pose NameError
        /// (texte affiché sous le champ, pas juste une couleur) si invalide.</summary>
        private bool ValidateGeneralStep()
        {
            if (string.IsNullOrWhiteSpace(Item.Name))
            {
                NameError = Loc["LibFieldRequired"];
                return false;
            }
            NameError = null;
            return true;
        }

        /// <summary>Mode assistant uniquement (bouton Suivant) - avance d'une étape dans l'ordre fixe
        /// Général→Règles→Magie→Équipement→Guerriers, déclenchant le chargement différé au passage sur
        /// Équipement/Guerriers comme le ferait un tap sur l'onglet correspondant en mode édition. Validé
        /// par étape : en quittant Général, bloque tant que le Nom est vide (pas d'intérêt à valider
        /// Règles/Magie/Équipement/Guerriers pour l'instant, rien d'obligatoire dessus).</summary>
        [RelayCommand]
        private async Task Next()
        {
            if (IsGeneralTab && !ValidateGeneralStep()) return;
            if (SelectedTab >= StepCount - 1) return;
            SelectedTab++;
        }

        /// <summary>Mode assistant uniquement (bouton Précédent).</summary>
        [RelayCommand]
        private void Back()
        {
            if (SelectedTab > 0) SelectedTab--;
        }
        [RelayCommand]
        private async Task AddArchetype()
        {

            var picked = await _warbandArchetypePicker.PickWarbandArchetypeAsync();
            if (picked == null) return;

            Archetype = picked;
        }

        /// <summary>Seul point d'écriture en base de tout le dialog - la bande d'abord (Item.Id devient réel
        /// si c'était une création), puis les guerriers/listes en attente avec Item.Id réassigné, puis les
        /// suppressions en attente. Le dialog est donc auto-persistant (contrairement à la plupart des autres
        /// dialogs Edit de l'app où l'appelant persiste après fermeture) - le seul dont le Save doit
        /// orchestrer plusieurs entités liées par clé étrangère, pas juste un item + ses listes de
        /// références.</summary>
        [RelayCommand]
        private async Task Save()
        {
            // Mode édition (pas de notion d'étape, Next ne passe jamais par ici) : seul point de vérification
            // du Nom - ramène sur Général si on en était sorti, pour que l'erreur reste visible.
            if (!ValidateGeneralStep())
            {
                SelectedTab = 0;
                return;
            }

            var language = LocalizationService.Instance.Language;
            await Loading.RunAsync(async () =>
            {
                await _warbandService.SaveWarbandAsync(Item);
            });

            Close(true);
        }
    }
}
