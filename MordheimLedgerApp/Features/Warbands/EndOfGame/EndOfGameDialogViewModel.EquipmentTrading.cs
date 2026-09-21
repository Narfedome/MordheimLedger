using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Achat/Vente d'équipement" (livre, étape 9 - "Reallocate equipment" - "A player may
/// trade in weapons and equipment at the same time he buys new ones") - toujours présente, juste AVANT
/// Recrutement (2026-09-05, retour utilisateur - "je me pose des questions... si on faisait d'abord
/// l'achat vente d'équipement, puis le recrutement avec l'assignation des équipements de la stash je me
/// demande si ça serait pas plus simple dans notre gestion"). Deux sections indépendantes :
///
/// **Achat** : choix libre au picker (commonOnly, comme le reste du recrutement), rejoint la réserve de
/// la bande - PAS assigné à un guerrier précis ici (PurchasedReserveItems, de simples EquipmentPick sans
/// slot/groupe porteur, réutilisés tels quels).
///
/// **Vente** : livre des règles - "Warriors can automatically sell equipment for half its listed price...
/// rare equipment and weapons which have a variable price, the warband receives half of the basic cost
/// only" ET "trade in weapons and equipment... swapped around the warband" (donc pas SEULEMENT la réserve
/// - l'équipement DÉJÀ PORTÉ par un guerrier actif aussi, retour utilisateur explicite après une première
/// version scopée à tort à la seule réserve). SellEquipmentDialog (Codex/chips, sélection multiple,
/// retour utilisateur - "affichage façon chiplistitem/codex... on ouvre l'inventaire façon codex,
/// sélection multiple, on valide") liste tout ce qui est vendable (réserve + porté), PendingSales garde
/// les lignes confirmées comme des chips retirables (RemoveSaleEquipment, la croix). Ne porte QUE sur
/// l'équipement DÉJÀ LÀ avant cette partie (WarbandEquipment de _warbandInventory + WarriorEquipment déjà
/// porté) - jamais un achat de CETTE session (Achat ci-dessus, ou un pick de l'étape Recrutement) : annuler
/// un achat en cours de session retire simplement le pick (RemoveReserveEquipment/RemoveRecruitEquipment),
/// ce n'est jamais une "vente" à moitié prix (retour utilisateur - "si on fait un achat et que finalement
/// on change d'avis, on considère que l'objet n'est pas acheté").
///
/// Le solde de cette étape (Achat en dépense, Vente en recette) rejoint EndOfGameTreasuryRemaining comme
/// tout le reste du wizard ; la réserve qui en résulte (pré-partie + Exploration + Achat - Vente) est ce
/// que Recrutement consomme en priorité avant d'acheter au plein tarif - voir
/// EndOfGameDialogViewModel.Recruitment.cs's BuildStashPool/BuildAvailableReservePool.</summary>
public partial class EndOfGameDialogViewModel
{
    // --- Achat : choix libre, rejoint la réserve (pas assigné à un guerrier ici) -----------------------

    public ObservableCollection<EquipmentPick> PurchasedReserveItems { get; } = new();

    /// <summary>Coût total de l'Achat en cours - affiché sous la section (retour utilisateur 2026-09-05 -
    /// "il faudrait en dessous un libellé indiquant combien d'or ça coûte, combien d'or on gagne, et le
    /// total").</summary>
    public int PurchasedReserveItemsCost => PurchasedReserveItems.Sum(p => p.Cost);

    [RelayCommand]
    private async Task AddReserveEquipment()
    {
        // Jamais de choix de matériau ici (retour utilisateur 2026-09-05 - "pas besoin d'avoir la
        // sélection des matériaux lors de l'achat, c'est que du normal puisque que les marchands ne
        // vendent que les items communs") : Gromril/Ithilmar sont des améliorations RARES (SpecialRule.
        // CostMultiplier), jamais proposées par un marchand qui ne vend QUE des Objets Communs
        // (commonOnly: true) - contrairement à AddRecruitEquipment (choix libre pour un guerrier précis,
        // pas limité au commerce ordinaire) où le choix de matériau reste légitime.
        var items = await _equipmentPicker.PickEquipmentAsync(_recruitableWarbandArchetype.Id, availableGold: EndOfGameTreasuryRemaining, commonOnly: true);

        foreach (var equipmentItem in items)
        {
            var pick = new EquipmentPick(equipmentItem, materialRule: null);
            if (EndOfGameTreasuryRemaining < pick.Cost)
            {
                await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                break;
            }

            PurchasedReserveItems.Add(pick);
            NotifyTreasuryChanged();
        }
    }

    [RelayCommand]
    private Task ShowReserveEquipmentDetail(EquipmentPick pick) => _detailDialogs.ShowEquipmentDetailDialogAsync(pick.Item, pick.MaterialRule);

    [RelayCommand]
    private void RemoveReserveEquipment(EquipmentPick pick)
    {
        PurchasedReserveItems.Remove(pick);
        NotifyTreasuryChanged();
    }

    // --- Vente : réserve pré-partie + équipement déjà porté, jamais un achat de cette session -----------

    public ObservableCollection<SellableEquipmentCandidate> PendingSales { get; } = new();

    /// <summary>Gain total de la Vente en cours - affiché sous la section, même idiome que
    /// PurchasedReserveItemsCost.</summary>
    public int PendingSalesTotal => PendingSales.Sum(c => c.SellPrice);

    /// <summary>Solde net de cette étape (Vente - Achat) - affiché en plus des deux totaux séparés, sous
    /// les deux sections (retour utilisateur - "combien d'or ça coûte, combien d'or on gagne, et le
    /// total").</summary>
    public int EquipmentTradingNetTotal => PendingSalesTotal - PurchasedReserveItemsCost;

    /// <summary>Tout ce qui existait déjà AVANT cette partie et peut donc être vendu (livre des règles -
    /// "swapped around the warband from one fighter to another") : la réserve pré-partie
    /// (_warbandInventory, jamais ce que l'étape Achat vient d'y ajouter) puis l'équipement déjà porté par
    /// chaque guerrier actif (WarriorRows - tous, y compris un guerrier Hors de combat cette partie : son
    /// équipement existait bien avant, seul son statut change). SelectableCandidateKey (IsFromStash,
    /// SourceId) exclut ce qui est déjà dans PendingSales, pour ne jamais proposer deux fois la même
    /// ligne au picker.</summary>
    private IEnumerable<SellableEquipmentCandidate> BuildSellableCandidates()
    {
        var alreadyPending = PendingSales.Select(c => (c.IsFromStash, c.SourceId)).ToHashSet();

        foreach (var stashItem in _warbandInventory)
            if (!alreadyPending.Contains((true, stashItem.Id)))
                yield return new SellableEquipmentCandidate(stashItem);

        foreach (var row in WarriorRows)
            foreach (var equipment in row.Warrior.Equipment)
                if (!alreadyPending.Contains((false, equipment.Id)))
                    yield return new SellableEquipmentCandidate(equipment, row.Warrior.Name);
    }

    [RelayCommand]
    private async Task AddSaleEquipment()
    {
        var candidates = BuildSellableCandidates().ToList();
        if (candidates.Count == 0)
        {
            // Bouton "+" toujours cliquable même quand il n'y a rien à vendre (pas de désactivation
            // conditionnelle) - un no-op silencieux se lisait comme "le menu ne s'ouvre pas" (retour
            // utilisateur 2026-09-05) - message explicite à la place.
            await ShowInfoAsync(Loc["EndOfGameSellEquipmentDialogTitle"], Loc["EndOfGameNoSellableEquipment"]);
            return;
        }

        var viewModel = new SellEquipmentDialogViewModel(candidates);
        var confirmed = await ShowDialogAsync(new SellEquipmentDialog(viewModel));
        if (confirmed != true) return;

        foreach (var candidate in viewModel.Candidates.Where(c => c.IsSelected))
            PendingSales.Add(candidate);

        NotifyTreasuryChanged();
    }

    [RelayCommand]
    private void RemoveSaleEquipment(SellableEquipmentCandidate candidate)
    {
        PendingSales.Remove(candidate);
        NotifyTreasuryChanged();
    }
}
