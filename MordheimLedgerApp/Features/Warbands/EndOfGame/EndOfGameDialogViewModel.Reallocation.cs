using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Réallouer l'équipement" (livre des règles, étape 9 - "Swap equipment between models
/// as desired") - DERNIÈRE étape du wizard avant le Récapitulatif, après Achat/Vente ET tout le
/// Recrutement. Héros uniquement (existants ET nouvelles recrues de cette même partie) + la Réserve -
/// jamais un groupe d'Hommes de main (équipement partagé "par modèle" pour tout le groupe, pas une
/// figurine individuelle qu'on peut isoler). Objet par objet, toujours gratuit (jamais de coût, jamais
/// de demi-prix comme la Vente) : chip classique (tap = détail, inchangé) + icône ⇄ qui ouvre un choix
/// de destination (ActionSheet, même mécanisme que WarbandInventoryDialogViewModel.Equip).
///
/// **Mécanisme** (le point délicat - voir aussi ReallocatableItem.cs) : une recrue de cette session
/// n'est qu'un brouillon (WarriorNameSlot/EquipmentPick, jamais un vrai Warrior avant Terminer - voir
/// EndOfGameDialogViewModel.Recruitment.cs's doc de classe), alors qu'un Héros déjà actif et la réserve
/// pré-partie sont de VRAIES lignes DB pas encore touchées par ce wizard. Ce wizard ne persiste jamais
/// rien avant Terminer, donc "déplacer" un objet réel reste purement en mémoire jusque-là :
/// - Retirer d'un Héros existant/de la Réserve = simple .Remove() sur la liste réelle (l'objet retiré
///   garde son id réel positif, juste absent de la collection en mémoire - rien n'est supprimé en base
///   avant Terminer).
/// - Ajouter à un Héros existant/la Réserve = nouvel objet à id SYNTHÉTIQUE NÉGATIF (compteur
///   décroissant, même idiome que SellableEquipmentCandidate.SyntheticDismissalSourceId) - jamais le
///   même objet réutilisé (même si le prix de revient est identique, delete+recreate est la seule
///   primitive disponible côté service, voir WarbandDetailViewModel.EndOfGame.
///   ApplyEquipmentReallocationAsync).
/// - Ajouter/retirer chez une recrue = simple Add/Remove sur son EquipmentPick.Equipment brouillon,
///   déjà le mécanisme d'AddRecruitEquipment/RemoveRecruitEquipment - rien de spécial à faire à
///   Terminer pour ce côté, ApplyRecruitmentAsync (déjà dans le pipeline) traite ce brouillon tel quel.</summary>
public partial class EndOfGameDialogViewModel
{
    private int _nextSyntheticReallocationId = -1;

    /// <summary>Copie de travail de la réserve pré-partie - JAMAIS _warbandInventory directement, qui
    /// doit rester un instantané figé pour Captives/BuildStashPool/BuildSellableCandidates. Peuplée une
    /// seule fois au constructeur principal (voir son appel dans EndOfGameDialogViewModel.cs).</summary>
    public List<WarbandEquipment> ReallocationReserve { get; private set; } = new();

    /// <summary>Instantané des ids WarbandEquipment RÉELLEMENT en base (= _warbandInventory, jamais muté)
    /// - même rôle que WarriorOutcomeRow.OriginalEquipmentIds côté Réserve, pour
    /// ApplyEquipmentReallocationAsync.</summary>
    public IReadOnlyCollection<int> OriginalReserveEquipmentIds { get; private set; } = Array.Empty<int>();

    private void InitializeReallocation()
    {
        ReallocationReserve = _warbandInventory.ToList();
        OriginalReserveEquipmentIds = _warbandInventory.Select(w => w.Id).ToList();
    }

    private bool _reallocationReserveExtrasAdded;
    private readonly List<(EquipmentItem Item, SpecialRule? MaterialRule, int Quantity)> _reallocationReserveExtrasOriginal = new();

    /// <summary>Ce que Exploration/Renvoyer/Achat ont ajouté à la réserve depuis l'ouverture du wizard,
    /// avant que ce même wizard ne les crée réellement en base - snapshot pris au moment de
    /// EnsureReallocationReserveExtrasAdded (retour utilisateur 2026-09-22 - "les objets trouvés lors de
    /// l'exploration ne sont pas dans la réserve lors de la réallocation").</summary>
    public IReadOnlyList<(EquipmentItem Item, SpecialRule? MaterialRule, int Quantity)> ReallocationReserveExtrasOriginal => _reallocationReserveExtrasOriginal;

    /// <summary>Complète ReallocationReserve avec ce que les étapes précédentes de ce même wizard
    /// (Exploration, Renvoyer, Achat) ont ajouté à la réserve - jamais fait au constructeur
    /// (InitializeReallocation), qui tourne bien avant qu'aucune de ces étapes n'ait eu lieu, seulement à
    /// la première visite RÉELLE de cette étape (appelée en tête de ReallocationCarriers). Id = 0
    /// (délibérément, ni id réel positif ni id synthétique négatif) pour ces entrées : elles seront de
    /// VRAIES lignes WarbandEquipment d'ici Terminer - ApplyExplorationOutcomeAsync/ApplyDismissalsAsync/
    /// ApplyEquipmentTradingAsync les créent chacune à leur tour, plus tôt dans le pipeline qu'
    /// ApplyEquipmentReallocationAsync - donc jamais recréées ici si le joueur ne les touche pas ; si
    /// déplacées ailleurs, ApplyEquipmentReallocationAsync les retrouve par une requête fraîche (même
    /// idiome que SellableEquipmentCandidate.IsFromExploration/IsFromDismissal) plutôt que par id.
    /// N'inclut PAS ce qui a déjà été vendu (Vente) : limite connue acceptée, un objet vendu puis encore
    /// visible ici ne ferait qu'un déplacement sans effet (suppression silencieuse d'une ligne déjà
    /// absente), jamais de doublon ni d'erreur.</summary>
    private void EnsureReallocationReserveExtrasAdded()
    {
        if (_reallocationReserveExtrasAdded) return;
        _reallocationReserveExtrasAdded = true;

        foreach (var (item, materialRule, quantity) in PendingExplorationStashItems().Concat(PendingDismissedEquipment()))
        {
            ReallocationReserve.Add(new WarbandEquipment { Item = item, MaterialRule = materialRule, Quantity = quantity });
            _reallocationReserveExtrasOriginal.Add((item, materialRule, quantity));
        }
        foreach (var pick in PurchasedReserveItems)
        {
            ReallocationReserve.Add(new WarbandEquipment { Item = pick.Item, MaterialRule = pick.MaterialRule, Quantity = 1 });
            _reallocationReserveExtrasOriginal.Add((pick.Item, pick.MaterialRule, 1));
        }
    }

    /// <summary>Recréé à chaque accès (jamais mis en cache, même principe que DismissibleWarriorRows) :
    /// reflète en direct les décisions des étapes précédentes de ce même wizard (un Héros entièrement
    /// renvoyé disparaît, une recrue nouvellement comptée apparaît).</summary>
    public IEnumerable<ReallocationCarrier> ReallocationCarriers
    {
        get
        {
            EnsureReallocationReserveExtrasAdded();
            foreach (var row in WarriorRows.Where(r => r.IsHero && !r.IsFullyDismissed))
                yield return ReallocationCarrier.ForHero(row);
            foreach (var slot in RecruitedHeroRows.SelectMany(r => r.NameSlots))
                yield return ReallocationCarrier.ForRecruit(slot);
            yield return ReallocationCarrier.ForReserve(ReallocationReserve, Loc["EndOfGameEquipmentTradingStashSource"]);
        }
    }

    /// <summary>Absente si aucun Héros existant NI recrue cette partie - rien à réallouer.</summary>
    public bool HasEligibleReallocationCarriers => WarriorRows.Any(r => r.IsHero && !r.IsFullyDismissed) || RecruitedHeroRows.Any();

    [RelayCommand]
    private Task ShowReallocationItemDetail(ReallocatableItem item) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(item.Item, item.MaterialRule, item.FoundValueOverride, item.BlessingRule);

    [RelayCommand]
    private async Task MoveReallocationItem(ReallocatableItem item)
    {
        var destinations = ReallocationCarriers.Where(c => !c.IsSameCarrierAs(item.Carrier)).ToList();
        if (destinations.Count == 0) return;

        var index = await ShowActionSheetIndexAsync(Loc["EndOfGameEquipmentReallocationDestinationTitle"], destinations.Select(c => c.Name).ToArray());
        if (index < 0 || index >= destinations.Count) return;
        var destination = destinations[index];

        RemoveFromReallocationCarrier(item);
        AddToReallocationCarrier(item, destination);

        OnPropertyChanged(nameof(ReallocationCarriers));

        if (destination.Kind != ReallocationCarrierKind.Reserve
            && WeaponLimits.ExceedsLimits(destination.Items.Select(i => i.Item)))
            await ShowInfoAsync(Loc["WarbandsWeaponLimitWarningTitle"], string.Format(Loc["WarbandsWeaponLimitWarningMessage"], destination.Name));
    }

    private void RemoveFromReallocationCarrier(ReallocatableItem item)
    {
        switch (item.Carrier.Kind)
        {
            case ReallocationCarrierKind.ExistingHero:
                item.Carrier.ExistingHeroRow!.Warrior.Equipment.Remove(item.WarriorEquipmentSource!);
                break;
            case ReallocationCarrierKind.NewRecruit:
                item.Carrier.NewRecruitSlot!.Equipment.Remove(item.DraftPickSource!);
                break;
            case ReallocationCarrierKind.Reserve:
                ReallocationReserve.Remove(item.WarbandEquipmentSource!);
                break;
        }
    }

    private void AddToReallocationCarrier(ReallocatableItem item, ReallocationCarrier destination)
    {
        switch (destination.Kind)
        {
            case ReallocationCarrierKind.ExistingHero:
                destination.ExistingHeroRow!.Warrior.Equipment.Add(new WarriorEquipment
                {
                    Id = _nextSyntheticReallocationId--,
                    WarriorId = destination.ExistingHeroRow.Warrior.Id,
                    Item = item.Item,
                    MaterialRule = item.MaterialRule,
                    BlessingRule = item.BlessingRule,
                    Quantity = item.Quantity,
                    FoundValueOverride = item.FoundValueOverride
                });
                break;
            case ReallocationCarrierKind.NewRecruit:
                // EquipmentPick n'a pas de notion de quantité empilée - un pick par exemplaire du lot
                // déplacé (ex. "Munitions x6" devient 6 picks), accepté comme simplification connue.
                for (var i = 0; i < item.Quantity; i++)
                    destination.NewRecruitSlot!.Equipment.Add(new EquipmentPick(item.Item, item.MaterialRule) { IsReallocated = true });
                break;
            case ReallocationCarrierKind.Reserve:
                ReallocationReserve.Add(new WarbandEquipment
                {
                    Id = _nextSyntheticReallocationId--,
                    Item = item.Item,
                    MaterialRule = item.MaterialRule,
                    Quantity = item.Quantity,
                    FoundValueOverride = item.FoundValueOverride
                });
                break;
        }
    }
}
