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
/// **Achat** : choix libre au picker partagé (_equipmentPicker, commonOnly, comme le reste du
/// recrutement), rejoint la réserve de la bande - PAS assigné à un guerrier précis ici
/// (PurchasedReserveItems, de simples EquipmentPick sans slot/groupe porteur, réutilisés tels quels).
///
/// **Vente** : livre des règles - "Warriors can automatically sell equipment for half its listed price...
/// rare equipment and weapons which have a variable price, the warband receives half of the basic cost
/// only" ET "trade in weapons and equipment... swapped around the warband" (donc pas SEULEMENT la réserve
/// - l'équipement DÉJÀ PORTÉ par un guerrier actif aussi). Ne porte QUE sur l'équipement DÉJÀ LÀ avant
/// cette partie (réserve d'origine + WarriorEquipment déjà porté) OU trouvé PENDANT
/// cette même partie via Exploration - jamais un achat de CETTE session (Achat ci-dessus, ou un pick de
/// l'étape Recrutement) : annuler un achat en cours de session retire simplement le pick
/// (RemoveReserveEquipment/RemoveRecruitEquipment), ce n'est jamais une "vente" à moitié prix.
///
/// **Historique de design (2026-09-21)** : Vente a d'abord vécu derrière un "+" ouvrant SellEquipmentDialog
/// (un popup DialogContent&lt;T&gt; bespoke), puis une itération affichée en ligne directement sur cette
/// étape - ni l'une ni l'autre ne correspondait au standard de l'app. Retour utilisateur final : "on
/// utilise le sélecteur qu'on a toujours eu dans l'appli. Ouverture d'une nouvelle page... grouper par
/// personnage/groupe/réserve. Fin le classique d'un sélecteur qu'on avait jusque-là" - _sellEquipmentPicker
/// (ISellEquipmentPickerService/SellEquipmentSelectorPage) suit exactement la même famille que
/// _equipmentPicker/IMutationPickerService et consorts (page poussée modalement via PushModalAsync, pas un
/// dialog). PendingSales reste la liste PERSISTANTE des ventes confirmées (chips retirables sur cet écran,
/// comme Achat) ; BuildSellableCandidates() reconstruit à CHAQUE ouverture du picker (jamais mis en cache)
/// ce qui reste vendable, en retranchant ce qui est déjà dans PendingSales - le picker n'affiche donc
/// jamais deux fois la même unité déjà vendue. Reconstruire à la demande (pas au constructeur du wizard)
/// règle aussi un problème de timing : à l'ouverture du wizard, l'étape Exploration (antérieure dans
/// Steps) n'a pas encore été jouée, donc ses trouvailles ne seraient pas encore dans la réserve - reconstruire
/// seulement quand le joueur clique "+" (forcément après avoir déjà traversé Exploration) évite ce piège.
///
/// Le solde de cette étape (Achat en dépense, Vente en recette) rejoint EndOfGameTreasuryRemaining comme
/// tout le reste du wizard ; la réserve qui en résulte (BuildReserve, stade AfterSales) est ce
/// que Recrutement consomme en priorité avant d'acheter au plein tarif - voir
/// EndOfGamePageViewModel.Reserve.cs.</summary>
public partial class EndOfGamePageViewModel
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
        var items = await _equipmentPicker.PickEquipmentAsync(_recruitableWarbandArchetype.Id, availableGold: EndOfGameTreasuryRemaining, commonOnly: true, allowCreate: false);

        foreach (var equipmentItem in items)
        {
            // Plus de dialog "fonds insuffisants" ici (retirée 2026-09-23, retour utilisateur - elle
            // s'affichait une demi-seconde puis disparaissait, le joueur restant bloqué dans le picker :
            // même course de navigation modale que ShowWeaponLimitWarningIfNeededAsync's own doc décrit
            // déjà). Tous les objets choisis rejoignent la réserve ; un solde négatif bloque Suivant via
            // IsTreasuryBlocked (bandeau dédié dans EquipmentTradingStepView.xaml).
            // PurchasedReserveItems est à la fois la liste d'affichage (un pick par achat, retirable) et
            // l'apport à la réserve - BuildReserve la relit, rien d'autre à tenir à jour.
            PurchasedReserveItems.Add(new EquipmentPick(equipmentItem, materialRule: null));
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

    // --- Vente : réserve pré-partie + équipement déjà porté + trouvailles d'Exploration, jamais un achat
    // de cette session - même famille de picker (page poussée modalement) que le reste de l'app ------------

    public ObservableCollection<SellableEquipmentCandidate> PendingSales { get; } = new();

    /// <summary>Gain total de la Vente en cours - affiché sous la section, même idiome que
    /// PurchasedReserveItemsCost.</summary>
    public int PendingSalesTotal => PendingSales.Sum(c => c.SellPrice);

    /// <summary>Solde net de cette étape (Vente - Achat) - affiché en plus des deux totaux séparés, sous
    /// les deux sections (retour utilisateur - "combien d'or ça coûte, combien d'or on gagne, et le
    /// total").</summary>
    public int EquipmentTradingNetTotal => PendingSalesTotal - PurchasedReserveItemsCost;

    /// <summary>Tout ce qui peut être vendu (livre des règles - "swapped around the warband from one
    /// fighter to another") : la réserve telle qu'elle est à cette étape (BuildReserve, stade AfterSales -
    /// ventes déjà confirmées retranchées : réserve d'avant la partie, trouvailles d'Exploration, objets du
    /// scénario, équipement rendu par les renvois), puis l'équipement porté par chaque guerrier actif (y
    /// compris un guerrier Hors de combat cette partie : son équipement existait bien avant). Jamais un
    /// achat de CETTE session (Achat ou objet rare - "annuler un achat... ce n'est jamais une vente").
    /// Reconstruite à chaque appel.</summary>
    private IEnumerable<SellableEquipmentCandidate> BuildSellableCandidates()
    {
        foreach (var line in BuildReserve(ReserveStage.AfterSales).Lines
                     .Where(l => l.Origin is not (ReserveLineOrigin.Purchase or ReserveLineOrigin.RarePurchase) && l.Quantity > 0))
            yield return new SellableEquipmentCandidate(line, line.Quantity);

        // Équipement porté : pas dans la réserve, décompte des ventes déjà confirmées par ligne portée.
        var alreadyPendingQty = PendingSales.Where(c => c.CarriedItem is not null)
            .GroupBy(c => c.CarriedItem!.Id).ToDictionary(g => g.Key, g => g.Sum(c => c.SelectedQuantity));

        // IsFullyDismissed exclu : tout son équipement rejoint la réserve (PendingDismissedEquipment,
        // ci-dessus) - le montrer aussi ici le compterait deux fois. Un renvoi PARTIEL (groupe d'Hommes de
        // main) laisse cette ligne intacte à dessein : les figurines restantes portent toujours la même
        // quantité PAR MODÈLE, encore vendable normalement.
        foreach (var row in WarriorRows.Where(r => !r.IsFullyDismissed))
            foreach (var equipment in row.Warrior.Equipment)
            {
                var remaining = equipment.Quantity - alreadyPendingQty.GetValueOrDefault(equipment.Id);
                // WarriorEquipment.Quantity d'un groupe d'Hommes de main est une quantité PAR MODÈLE - chaque
                // "cran" vendu ici retire donc Effectif objets physiques d'un coup, jamais 1 seul (retour
                // utilisateur 2026-09-22 - "la vente ne comptabilise qu'une arme et pas l'arme×effectif").
                // Toujours 1 pour un Héros (HeadCount == 1 par construction).
                if (remaining > 0) yield return new SellableEquipmentCandidate(equipment, row.Warrior.Name, remaining, saleQuantityMultiplier: Math.Max(1, row.Warrior.HeadCount));
            }
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
            await ShowInfoAsync(Loc["EndOfGameEquipmentTradingSellHeading"], Loc["EndOfGameNoSellableEquipment"]);
            return;
        }

        // Une vente de réserve est retranchée par BuildReserve (stade AfterSales) depuis PendingSales -
        // rien d'autre à tenir à jour, ni ici ni à l'annulation (RemoveSaleEquipment).
        foreach (var candidate in await _sellEquipmentPicker.PickSaleAsync(candidates))
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
