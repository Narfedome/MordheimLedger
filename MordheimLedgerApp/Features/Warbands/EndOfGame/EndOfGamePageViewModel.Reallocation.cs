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
/// - Retirer d'un Héros existant = simple .Remove() sur sa liste (l'objet retiré garde son id réel
///   positif, juste absent de la collection en mémoire - rien n'est supprimé en base avant Terminer).
/// - Ajouter à un Héros existant = nouvel objet à id SYNTHÉTIQUE NÉGATIF (compteur décroissant) - jamais
///   le même objet réutilisé (delete+recreate est la seule primitive disponible côté service, voir
///   ApplyEquipmentReallocationAsync).
/// - Réserve (dans un sens ou dans l'autre) = un déplacement enregistré dans _reallocationReserveMoves,
///   rejoué par BuildReserve - la carte Réserve affiche la réserve finale (BuildReserve, stade Final),
///   ventes et recrutement compris, écrite à Terminer par ApplyReserveAsync.
/// - Ajouter/retirer chez une recrue = simple Add/Remove sur son EquipmentPick.Equipment brouillon,
///   déjà le mécanisme d'AddRecruitEquipment/RemoveRecruitEquipment - rien de spécial à faire à
///   Terminer pour ce côté, ApplyRecruitmentAsync (déjà dans le pipeline) traite ce brouillon tel quel.</summary>
public partial class EndOfGamePageViewModel
{
    private int _nextSyntheticReallocationId = -1;

    // État des porteurs à l'entrée dans Réallouer (Suivant) - restauré par UndoReallocation.
    private List<(WarriorOutcomeRow Row, List<(WarriorEquipment Equipment, int Quantity)> Equipment)> _reallocationHeroSnapshot = new();
    private List<(WarriorNameSlot Slot, List<EquipmentPick> Picks)> _reallocationRecruitSnapshot = new();

    /// <summary>Photographie l'équipement des Héros existants et des recrues au moment d'entrer dans
    /// Réallouer par Suivant - appelé par Next(). Les objets eux-mêmes ET leur Quantity (un déplacement
    /// partiel décrémente WarriorEquipment.Quantity en place, voir RemoveFromReallocationCarrier).</summary>
    private void CaptureReallocationSnapshot()
    {
        _reallocationHeroSnapshot = WarriorRows.Where(r => r.IsHero)
            .Select(r => (r, r.Warrior.Equipment.Select(e => (e, e.Quantity)).ToList()))
            .ToList();
        _reallocationRecruitSnapshot = RecruitedHeroRows.SelectMany(r => r.NameSlots)
            .Select(s => (s, s.Equipment.ToList()))
            .ToList();
        _reallocationReserveMoves.Clear();
    }

    /// <summary>Annule tous les déplacements de Réallouer - appelé quand on quitte l'étape par Précédent
    /// (voir Back()) : un changement fait plus haut dans le wizard (vente, recrutement...) pourrait sinon
    /// laisser un déplacement pointer vers un objet de la réserve qui n'y est plus.</summary>
    private void UndoReallocation()
    {
        foreach (var (row, equipment) in _reallocationHeroSnapshot)
        {
            row.Warrior.Equipment.Clear();
            foreach (var (item, quantity) in equipment)
            {
                item.Quantity = quantity;
                row.Warrior.Equipment.Add(item);
            }
        }
        foreach (var (slot, picks) in _reallocationRecruitSnapshot)
        {
            slot.Equipment.Clear();
            foreach (var pick in picks) slot.Equipment.Add(pick);
        }
        _reallocationReserveMoves.Clear();
        OnPropertyChanged(nameof(ReallocationCarriers));
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
            yield return ReallocationCarrier.ForReserve(BuildReserve(ReserveStage.Final).Lines.ToList(), Loc["EndOfGameEquipmentTradingStashSource"]);
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

    /// <summary>Héros : retrait complet de la ligne si quantity couvre tout le lot, sinon décrémente sa
    /// Quantity en mémoire (même id réel positif) - voir ApplyEquipmentReallocationAsync pour la
    /// synchronisation à Terminer. Réserve : enregistre le déplacement (_reallocationReserveMoves), que
    /// BuildReserve rejoue - la réserve n'est jamais modifiée directement.</summary>
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
                _reallocationReserveMoves.Add(new ReserveMove(item.ReserveLineSource!.Key, quantity, null));
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
                _reallocationReserveMoves.Add(new ReserveMove(null, quantity, new ReserveInflow(item.Item, item.MaterialRule, quantity, item.FoundValueOverride)));
                break;
        }
    }
}
