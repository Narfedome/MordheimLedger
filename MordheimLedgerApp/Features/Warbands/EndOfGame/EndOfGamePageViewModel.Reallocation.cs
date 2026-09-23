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
/// EndOfGamePageViewModel.Recruitment.cs's doc de classe), alors qu'un Héros déjà actif et la réserve
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
public partial class EndOfGamePageViewModel
{
    private int _nextSyntheticReallocationId = -1;

    /// <summary>Copie de travail de la réserve pré-partie - depuis l'unification de la réserve
    /// (2026-09-23), seedée depuis _originalReserveSnapshot (JAMAIS _reserve directement) : Réallouer
    /// doit rester insensible aux ventes de CETTE session (limite connue acceptée, voir
    /// EnsureReallocationReserveExtrasAdded's own doc - "un objet vendu puis encore visible ici ne ferait
    /// qu'un déplacement sans effet"), alors que _reserve EST réduite en direct par chaque vente
    /// confirmée. Peuplée une seule fois au constructeur principal (voir son appel dans
    /// EndOfGamePageViewModel.cs) - des COPIES indépendantes de _originalReserveSnapshot, jamais les
    /// mêmes instances (dont Quantity ne doit justement jamais changer, voir sa propre doc).</summary>
    public List<ReserveLine> ReallocationReserve { get; private set; } = new();

    private void InitializeReallocation()
    {
        // _originalReserveSnapshot (champ de EndOfGamePageViewModel.cs) sert directement d'instantané
        // SourceId+Quantity pour ApplyEquipmentReallocationAsync - plus besoin d'un
        // OriginalReserveEquipmentIds séparé (retiré 2026-09-23, en ajoutant la détection de quantité
        // réduite, voir Apply.cs).
        ReallocationReserve = _originalReserveSnapshot
            .Select(l => new ReserveLine(l.Item, l.MaterialRule, l.Quantity, l.Origin, l.SourceId, l.FoundValueOverride))
            .ToList();
    }

    private bool _reallocationReserveExtrasAdded;
    private readonly HashSet<EquipmentPick> _mergedPurchasePicks = new();
    private readonly List<(EquipmentItem Item, SpecialRule? MaterialRule, int Quantity)> _reallocationReserveExtrasOriginal = new();

    /// <summary>Ce que Exploration/Renvoyer/Achat ont ajouté à la réserve depuis l'ouverture du wizard,
    /// avant que ce même wizard ne les crée réellement en base - snapshot pris au moment de
    /// EnsureReallocationReserveExtrasAdded (retour utilisateur 2026-09-22 - "les objets trouvés lors de
    /// l'exploration ne sont pas dans la réserve lors de la réallocation").</summary>
    public IReadOnlyList<(EquipmentItem Item, SpecialRule? MaterialRule, int Quantity)> ReallocationReserveExtrasOriginal => _reallocationReserveExtrasOriginal;

    /// <summary>Complète ReallocationReserve avec ce que les étapes précédentes de ce même wizard
    /// (Exploration, Renvoyer, Achat) ont ajouté à la réserve. SourceId null (ni réel positif ni
    /// synthétique négatif) pour ces entrées : elles seront de VRAIES lignes WarbandEquipment d'ici
    /// Terminer - ApplyExplorationOutcomeAsync/ApplyDismissalsAsync/ApplyEquipmentTradingAsync les créent
    /// chacune à leur tour, plus tôt dans le pipeline qu'ApplyEquipmentReallocationAsync - donc jamais
    /// recréées ici si le joueur ne les touche pas ; si déplacées ailleurs, ApplyEquipmentReallocationAsync
    /// les retrouve par une requête fraîche (même idiome que SellableEquipmentCandidate.IsFromExploration/
    /// IsFromDismissal) plutôt que par id. N'inclut PAS ce qui a déjà été vendu (Vente) : limite connue
    /// acceptée, un objet vendu puis encore visible ici ne ferait qu'un déplacement sans effet
    /// (suppression silencieuse d'une ligne déjà absente), jamais de doublon ni d'erreur.
    ///
    /// Exploration/Renvoyer restent fusionnées UNE SEULE FOIS (à la première visite RÉELLE de cette
    /// étape, jamais au constructeur InitializeReallocation qui tourne bien avant) - ce sont de pures
    /// dérivations recalculées à chaque appel (PendingExplorationStashItems/PendingDismissedEquipment),
    /// re-fusionner à chaque accès reviendrait à dupliquer une quantité déjà comptée, ou pire, à écraser
    /// un déplacement déjà fait par le joueur dans cette même étape.
    ///
    /// Achat (PurchasedReserveItems) fusionné à CHAQUE accès (2026-09-23, correction retour utilisateur -
    /// "dans l'allocation des items... les achats ne sont pas dans la réserve" : revenir à Achat/Vente
    /// APRÈS une première visite de Réallouer pour acheter davantage ne remontait jamais dans
    /// ReallocationReserve, verrouillé par le même garde one-shot qu'Exploration/Renvoyer) - contrairement
    /// à ces deux dérivations, un Achat est un objet DISCRET et STABLE (EquipmentPick, jamais recréé/
    /// reconstruit), donc _mergedPurchasePicks (identité de référence) permet de ne fusionner que les
    /// NOUVEAUX picks à chaque appel sans dupliquer ceux déjà fusionnés (et potentiellement déjà déplacés
    /// depuis). Annuler un Achat déjà fusionné ici (retour à Achat/Vente, "-") ne retire PAS la ligne déjà
    /// fusionnée - limite acceptée, symétrique à celle déjà documentée pour Vente ci-dessus, pas rencontrée
    /// dans le retour utilisateur qui a motivé ce correctif.</summary>
    private void EnsureReallocationReserveExtrasAdded()
    {
        if (!_reallocationReserveExtrasAdded)
        {
            _reallocationReserveExtrasAdded = true;
            foreach (var (item, materialRule, quantity) in PendingExplorationStashItems().Concat(PendingDismissedEquipment()))
            {
                ReallocationReserve.Add(new ReserveLine(item, materialRule, quantity, ReserveLineOrigin.Exploration));
                _reallocationReserveExtrasOriginal.Add((item, materialRule, quantity));
            }
        }

        foreach (var pick in PurchasedReserveItems)
        {
            if (!_mergedPurchasePicks.Add(pick)) continue;
            ReallocationReserve.Add(new ReserveLine(pick.Item, pick.MaterialRule, 1, ReserveLineOrigin.Purchase));
            _reallocationReserveExtrasOriginal.Add((pick.Item, pick.MaterialRule, 1));
        }
    }

    /// <summary>Recréé à chaque accès (jamais mis en cache, même principe que DismissibleWarriorRows) :
    /// reflète en direct les décisions des étapes précédentes de ce même wizard (un Héros entièrement
    /// renvoyé disparaît, une recrue nouvellement comptée apparaît). Réserve EN PREMIER (2026-09-23,
    /// retour utilisateur - "reserve tout en haut toujours visible") : simple réordonnancement, elle
    /// n'était auparavant que le DERNIER porteur, facilement perdue en bas d'une longue liste de
    /// Héros.</summary>
    public IEnumerable<ReallocationCarrier> ReallocationCarriers
    {
        get
        {
            EnsureReallocationReserveExtrasAdded();
            yield return ReallocationCarrier.ForReserve(ReallocationReserve, Loc["EndOfGameEquipmentTradingStashSource"]);
            foreach (var row in WarriorRows.Where(r => r.IsHero && !r.IsFullyDismissed))
                yield return ReallocationCarrier.ForHero(row);
            foreach (var slot in RecruitedHeroRows.SelectMany(r => r.NameSlots))
                yield return ReallocationCarrier.ForRecruit(slot);
        }
    }

    /// <summary>Absente si aucun Héros existant NI recrue cette partie - rien à réallouer.</summary>
    public bool HasEligibleReallocationCarriers => WarriorRows.Any(r => r.IsHero && !r.IsFullyDismissed) || RecruitedHeroRows.Any();

    [RelayCommand]
    private Task ShowReallocationItemDetail(ReallocatableItem item) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(item.Item, item.MaterialRule, item.FoundValueOverride, item.BlessingRule);

    /// <summary>Déplace item.Quantity par défaut (comportement inchangé pour une source non-empilée),
    /// mais demande la quantité PRÉCISE à déplacer quand item.Quantity &gt; 1 (2026-09-23, retour
    /// utilisateur - "si on a 2 hallebardes... ajouter la sélection du nombre d'item à allouer") : une
    /// recrue de cette session (DraftPickSource) n'atteint jamais ce cas (EquipmentPick n'a pas de
    /// quantité empilée, un pick = une unité, voir AddToReallocationCarrier), donc seul un Héros
    /// existant/la Réserve peut réellement déclencher ce prompt. Même idiome que SplitHenchmanGroupDraft
    /// (ShowPromptAsync + int.TryParse + borne) - initialValue pré-rempli au total pour qu'un simple OK
    /// déplace tout comme avant ce correctif.</summary>
    [RelayCommand]
    private async Task MoveReallocationItem(ReallocatableItem item)
    {
        var destinations = ReallocationCarriers.Where(c => !c.IsSameCarrierAs(item.Carrier)).ToList();
        if (destinations.Count == 0) return;

        var quantity = item.Quantity;
        if (quantity > 1)
        {
            var input = await ShowPromptAsync(Loc["EndOfGameReallocationQuantityTitle"],
                string.Format(Loc["EndOfGameReallocationQuantityPrompt"], quantity), initialValue: quantity.ToString());
            if (!int.TryParse(input, out var parsed) || parsed <= 0 || parsed > quantity) return;
            quantity = parsed;
        }

        var index = await ShowActionSheetIndexAsync(Loc["EndOfGameEquipmentReallocationDestinationTitle"], destinations.Select(c => c.Name).ToArray());
        if (index < 0 || index >= destinations.Count) return;
        var destination = destinations[index];

        RemoveFromReallocationCarrier(item, quantity);
        AddToReallocationCarrier(item, destination, quantity);

        OnPropertyChanged(nameof(ReallocationCarriers));

        if (destination.Kind != ReallocationCarrierKind.Reserve
            && WeaponLimits.ExceedsLimits(destination.Items.Select(i => i.Item)))
            await ShowInfoAsync(Loc["WarbandsWeaponLimitWarningTitle"], string.Format(Loc["WarbandsWeaponLimitWarningMessage"], destination.Name));
    }

    /// <summary>Retrait complet de la ligne si quantity couvre tout le lot (comportement inchangé),
    /// sinon décrémente sa Quantity en mémoire en laissant la ligne (même id réel positif) en place -
    /// voir EndOfGamePageViewModel.Apply.cs's ApplyEquipmentReallocationAsync pour la synchronisation de
    /// cette réduction vers la DB à Terminer, nécessaire pour ne pas dupliquer l'objet.</summary>
    private void RemoveFromReallocationCarrier(ReallocatableItem item, int quantity)
    {
        switch (item.Carrier.Kind)
        {
            case ReallocationCarrierKind.ExistingHero:
                var equipment = item.WarriorEquipmentSource!;
                if (quantity >= equipment.Quantity)
                    item.Carrier.ExistingHeroRow!.Warrior.Equipment.Remove(equipment);
                else
                    equipment.Quantity -= quantity;
                break;
            case ReallocationCarrierKind.NewRecruit:
                item.Carrier.NewRecruitSlot!.Equipment.Remove(item.DraftPickSource!);
                break;
            case ReallocationCarrierKind.Reserve:
                var line = item.ReserveLineSource!;
                if (quantity >= line.Quantity)
                    ReallocationReserve.Remove(line);
                else
                    line.Quantity -= quantity;
                break;
        }
    }

    private void AddToReallocationCarrier(ReallocatableItem item, ReallocationCarrier destination, int quantity)
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
                    Quantity = quantity,
                    FoundValueOverride = item.FoundValueOverride
                });
                break;
            case ReallocationCarrierKind.NewRecruit:
                // EquipmentPick n'a pas de notion de quantité empilée - un pick par exemplaire du lot
                // déplacé (ex. "Munitions x6" devient 6 picks), accepté comme simplification connue.
                for (var i = 0; i < quantity; i++)
                    destination.NewRecruitSlot!.Equipment.Add(new EquipmentPick(item.Item, item.MaterialRule) { IsReallocated = true });
                break;
            case ReallocationCarrierKind.Reserve:
                ReallocationReserve.Add(new ReserveLine(item.Item, item.MaterialRule, quantity, ReserveLineOrigin.Purchase, _nextSyntheticReallocationId--, item.FoundValueOverride));
                break;
        }
    }
}
