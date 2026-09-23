using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>ViewModel du picker "classique" de Vente (SellEquipmentSelectorPage, poussé modalement plein
/// écran comme tous les autres pickers de l'app - EquipmentPickerService/MutationPickerService... - retour
/// utilisateur 2026-09-21 : "on utilise le sélecteur qu'on a toujours eu dans l'appli. Ouverture d'une
/// nouvelle page, avec un ListItem, grouper par personnage/groupe/réserve. Fin le classique d'un sélecteur
/// qu'on avait jusque-là" - remplace une première itération en popup (SellEquipmentDialog, retirée) puis
/// une deuxième affichée en ligne sur l'étape elle-même (retirée aussi), aucune des deux ne correspondant à
/// ce standard.
///
/// Résolu via DI (AddTransient, comme MutationViewModel/EquipmentItemViewModel) mais initialisé APRÈS
/// résolution (Initialize) par SellEquipmentPickerService plutôt qu'au constructeur - Candidates vient du
/// wizard appelant (EndOfGamePageViewModel.EquipmentTrading.cs's BuildSellableCandidates, calculé à la
/// demande à chaque ouverture pour refléter la réserve à cet instant précis, jamais interrogé ici via un
/// service Library comme les autres pickers de cette famille).</summary>
public partial class SellEquipmentSelectorViewModel : ObservableObject
{
    private ISellEquipmentPickerNavigationService? _navigationService;

    public ObservableCollection<SellableEquipmentCandidate> Candidates { get; } = new();

    /// <summary>Réserve en premier (stash + trouvailles d'Exploration, IsFromStash couvre les deux) puis
    /// une section par guerrier porteur - retour utilisateur 2026-09-21, "un affichage à la codex, avec la
    /// séparation par héros, avec la réserve tout en haut".</summary>
    public ObservableCollection<SellableEquipmentGroup> Groups { get; } = new();

    public int TotalSellPrice => Candidates.Sum(c => c.SellPrice);

    public void Initialize(IEnumerable<SellableEquipmentCandidate> candidates, ISellEquipmentPickerNavigationService navigationService)
    {
        _navigationService = navigationService;

        foreach (var candidate in candidates)
            Candidates.Add(candidate);

        var stash = new SellableEquipmentGroup(LocalizationService.Instance["EndOfGameEquipmentTradingStashSource"]);
        foreach (var candidate in Candidates.Where(c => c.IsFromStash))
            stash.Add(candidate);
        if (stash.Count > 0) Groups.Add(stash);

        foreach (var carrierGroup in Candidates.Where(c => !c.IsFromStash).GroupBy(c => c.CarrierName))
        {
            var group = new SellableEquipmentGroup(carrierGroup.Key!);
            foreach (var candidate in carrierGroup)
                group.Add(candidate);
            Groups.Add(group);
        }
    }

    [RelayCommand]
    private void Increment(SellableEquipmentCandidate candidate)
    {
        if (!candidate.CanIncrement) return;
        candidate.SelectedQuantity++;
        OnPropertyChanged(nameof(TotalSellPrice));
    }

    [RelayCommand]
    private void Decrement(SellableEquipmentCandidate candidate)
    {
        if (!candidate.CanDecrement) return;
        candidate.SelectedQuantity--;
        OnPropertyChanged(nameof(TotalSellPrice));
    }

    [RelayCommand]
    private Task Confirm() => _navigationService!.ClosePickerAsync(Candidates.Where(c => c.SelectedQuantity > 0).ToList());

    [RelayCommand]
    private Task Cancel() => _navigationService!.ClosePickerAsync(Array.Empty<SellableEquipmentCandidate>());
}
