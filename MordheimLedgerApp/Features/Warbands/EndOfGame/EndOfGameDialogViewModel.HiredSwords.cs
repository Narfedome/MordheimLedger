using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Francs-Tireurs" (StepKind.HiredSwords, voir EndOfGameDialogViewModel.Steps) -
/// regroupe à la fois le règlement de solde de chaque Franc-Tireur déjà engagé (voir
/// HiredSwordUpkeepEntry) ET un recrutement optionnel d'un nouveau, les deux relevant de la même
/// "phase de campagne entre deux parties" du livre. Entièrement absente du wizard si
/// HasAnyHiredSwordRelevance est faux (aucun déjà engagé, aucun éligible à recruter). Placée juste
/// avant Récapitulatif (après l'or d'Exploration, voir EndOfGameDialogViewModel.Steps) pour que le
/// joueur décide en connaissant sa trésorerie finale - voir EndOfGameDialogViewModel.
/// EndOfGameTreasuryRemaining/CanAffordNewHiredSword.</summary>
public partial class EndOfGameDialogViewModel
{
    /// <summary>Catalogue complet (non filtré par restriction de bande NI par "déjà engagé" - voir
    /// AvailableHiredSwordsToRecruit) - permet de résoudre le profil (Upkeep notamment, jamais stocké
    /// sur Warrior lui-même) d'un Franc-Tireur DÉJÀ engagé même si sa restriction a changé depuis.</summary>
    private readonly List<HiredSword> _hiredSwordCatalog;

    public ObservableCollection<HiredSwordUpkeepEntry> HiredSwordUpkeepEntries { get; } = new();

    public bool HasHiredSwordUpkeep => HiredSwordUpkeepEntries.Count > 0;

    /// <summary>Éligibles à CETTE bande (RestrictedToWarbandArchetypeIds vide ou la contenant) moins les
    /// types déjà activement engagés (WarriorRows ne contient que les guerriers Actifs cette partie - un
    /// type Mort/parti n'y figure plus, donc redevient recrutable, conforme au livre). Base commune aux
    /// deux pickers de Franc-Tireur du wizard (celui-ci, et le grant gratuit de EndOfGameDialogViewModel.
    /// Exploration.cs) - chacun exclut EN PLUS sa propre sélection dans l'AUTRE picker (jamais la
    /// sienne : un picker ne doit jamais faire disparaître sa propre valeur choisie de son propre
    /// ItemsSource, ça la déselectionnerait).</summary>
    private List<HiredSword> EligibleHiredSwordsForBand => _hiredSwordCatalog
        .Where(h => h.RestrictedToWarbandArchetypeIds.Count == 0 || h.RestrictedToWarbandArchetypeIds.Contains(_warbandArchetypeId))
        .Where(h => !WarriorRows.Any(r => r.Warrior.HiredSwordId == h.Id))
        .ToList();

    /// <summary>Picker de recrutement payant (étape "Francs-Tireurs") - exclut le type déjà choisi
    /// gratuitement à l'étape Exploration (SelectedFreeHiredSword) pour éviter de proposer deux fois le
    /// même type dans la même session de wizard.</summary>
    public List<HiredSword> AvailableHiredSwordsToRecruit => EligibleHiredSwordsForBand
        .Where(h => h.Id != SelectedFreeHiredSword?.Id)
        .ToList();

    private bool HasAnyHiredSwordRelevance => HiredSwordUpkeepEntries.Count > 0 || AvailableHiredSwordsToRecruit.Count > 0;

    /// <summary>Pilote la visibilité du bloc recrutement dans le XAML - AvailableHiredSwordsToRecruit
    /// n'est pas directement bindable à IsVisible (une liste, pas un bool).</summary>
    public bool HasHiredSwordsToRecruit => AvailableHiredSwordsToRecruit.Count > 0;

    [ObservableProperty]
    private HiredSword? selectedNewHiredSword;

    partial void OnSelectedNewHiredSwordChanged(HiredSword? value)
    {
        NewHiredSwordAffordError = null;
        if (value is null) NewHiredSwordName = string.Empty;
        // Son coût d'engagement dispute désormais la même trésorerie que le reste du wizard (2026-09-03,
        // voir EndOfGameDialogViewModel.EndOfGameTreasuryRemaining/NotifyTreasuryChanged's own doc).
        NotifyTreasuryChanged();
    }

    [ObservableProperty]
    private string newHiredSwordName = string.Empty;

    partial void OnNewHiredSwordNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NewHiredSwordNameError = null;
    }

    [ObservableProperty]
    private string? newHiredSwordNameError;

    [ObservableProperty]
    private string? newHiredSwordAffordError;

    /// <summary>Le coût d'engagement de SelectedNewHiredSword est déjà retranché de
    /// EndOfGameTreasuryRemaining (2026-09-03, voir sa doc) - "peut-on se le permettre" revient donc
    /// simplement à vérifier que ce solde net, TOUTES dépenses de ce wizard confondues, ne passe pas dans
    /// le rouge. Remplace l'ancien HiredSwordTreasuryAfter, une version isolée qui ne tenait compte que de
    /// la trésorerie de base + l'or d'Exploration (ni des Objets rares, ni de la Corruption/Rétention, ni
    /// de l'Homme de main équipé - exactement le genre d'écart signalé par l'utilisateur le 2026-09-03).</summary>
    public bool CanAffordNewHiredSword => SelectedNewHiredSword is null || EndOfGameTreasuryRemaining >= 0;

    /// <summary>Peuplée une seule fois à la construction du dialog (voir EndOfGameDialogViewModel ctor) -
    /// ce sont de vrais guerriers déjà recrutés, pas un compte arbitraire piloté par un steppeur.</summary>
    private void BuildHiredSwordUpkeepEntries()
    {
        var payLabel = Loc["EndOfGameHiredSwordPayAction"];
        var dismissLabel = Loc["WarbandsDismissHiredSwordAction"];
        foreach (var row in WarriorRows.Where(r => r.Warrior.IsHiredSword))
        {
            var hiredSword = _hiredSwordCatalog.FirstOrDefault(h => h.Id == row.Warrior.HiredSwordId);
            if (hiredSword is null) continue;
            HiredSwordUpkeepEntries.Add(new HiredSwordUpkeepEntry(row.Warrior, hiredSword, payLabel, dismissLabel));
        }
    }

    private bool ValidateHiredSwordsStep()
    {
        var valid = true;
        foreach (var entry in HiredSwordUpkeepEntries.Where(e => e.HasChoice))
            valid &= CheckRoll(entry.WillPay is null, () => entry.ChoiceError = Loc["EndOfGameRollRequired"]);

        if (SelectedNewHiredSword is not null)
        {
            valid &= CheckRoll(string.IsNullOrWhiteSpace(NewHiredSwordName), () => NewHiredSwordNameError = Loc["LibFieldRequired"]);
            valid &= CheckRoll(!CanAffordNewHiredSword, () => NewHiredSwordAffordError = Loc["WarbandsInsufficientFundsMessage"]);
        }

        return valid;
    }

    [RelayCommand]
    private Task ShowHiredSwordToRecruitDetail(HiredSword hiredSword) => _detailDialogs.ShowHiredSwordDetailDialogAsync(hiredSword);
}
