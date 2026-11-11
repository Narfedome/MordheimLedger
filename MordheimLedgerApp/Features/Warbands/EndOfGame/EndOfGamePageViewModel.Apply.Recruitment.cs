using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Pipeline de persistance de Fin de Partie (Apply*Async) - Recrutement/Renvoyer/Achat
/// et Vente d'équipement/Réallocation (étapes 8-9 du livre). Voir EndOfGamePageViewModel.cs's Finish()
/// pour l'ordre d'appel complet du pipeline.
///
/// **Aucune de ces méthodes n'écrit la réserve (WarbandEquipment)** depuis le 2026-09-25 : ce que la
/// réserve gagne ou perd (renvois, achats, ventes, consommation au recrutement, réallocation) est déjà
/// dans la réserve finale calculée par BuildReserve, écrite une seule fois par ApplyReserveAsync. Ces
/// méthodes ne gardent que l'équipement porté par les guerriers, la trésorerie et l'historique - et
/// reprennent les coûts de l'aperçu (EquipmentPick.Cost, PlanTopUps) plutôt que de les recalculer contre
/// l'état de la base, pour que Terminer débite exactement ce que le wizard a affiché.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Écritures du recrutement, des renvois, des ventes et de la réallocation (Core, testé).</summary>
    private RosterChangeService Roster => new(_warbandService);

    /// <summary>Étape "Recrutement" (livre, étape 8) - transforme WarriorRecruitRow/WarriorNameSlot
    /// (EndOfGamePageViewModel.Recruitment.cs, de simples brouillons en mémoire jusqu'ici) en vrais
    /// Warrior, au même moment que tout le reste de ce wizard (Terminer) - jamais avant, voir la doc de
    /// classe d'EndOfGamePageViewModel.Recruitment.cs pour pourquoi (refus explicite de l'utilisateur
    /// d'un mécanisme "recruter puis annuler"). Toujours une recrue NEUVE. Chaque pick coûte son
    /// RecruitPickCost - 0 s'il est réellement servi par la réserve (servedReservePicks, figé en tête de
    /// Finish), vient de Réallouer (IsReallocated) ou est la dague gratuite (IsFree), sinon plein tarif.</summary>
    private async Task ApplyRecruitmentAsync(HashSet<EquipmentPick> servedReservePicks, List<string> sentences)
    {
        if (Warband is null) return;

        var totalCost = 0;
        foreach (var row in HeroRecruitRows.Where(r => r.Count > 0))
        {
            foreach (var slot in row.NameSlots)
            {
                var name = slot.Name.Trim();
                await Roster.RecruitAsync(Warband.Id, new RecruitDraft(row.Archetype, name, 1,
                    slot.Equipment.Select(p => (p.Item, p.MaterialRule)).ToList(), slot.Mutations.ToList()));
                // Mutations achetées au recrutement (Possédés/Mutants - livre, "+ the cost of mutations").
                totalCost += row.Cost + slot.Equipment.Sum(p => RecruitPickCost(p, servedReservePicks)) + slot.Mutations.Sum(m => m.Cost);

                sentences.Add(string.Format(Loc["HistoryRecruitedSentence"], name, row.Archetype.Name));
            }
        }

        await Roster.AdjustTreasuryAsync(Warband, -totalCost);
    }

    /// <summary>Étape "Renvoyer" (livre des règles - "Disbanding a Warband" + FAQ officielle - "you are
    /// allowed to dismiss any warrior at any time during the post-battle sequence... transfer the
    /// warrior's weapons and gear to your stash and then dismiss him"). Un Héros ou un groupe d'Hommes de
    /// main ENTIÈREMENT renvoyé perd son équipement porté puis passe WarriorStatus.Retired (jamais
    /// DeleteWarriorAsync, qui effacerait son historique). Un renvoi PARTIEL d'un groupe ne touche que
    /// Warrior.HeadCount (Quantity est une quantité PAR MODÈLE, voir PlanTopUps's own doc). Ce que
    /// l'équipement rendu apporte à la réserve est déjà dans la réserve finale (PendingDismissedEquipment,
    /// voir BuildReserve) - rien à y écrire ici.</summary>
    private async Task ApplyDismissalsAsync(List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var row in WarriorRows.Where(r => r.EffectiveDismissCount > 0))
        {
            // EffectiveDismissCount raisonne déjà sur l'effectif SURVIVANT de cette bataille ; le service
            // reclampe contre le HeadCount RÉEL (ex. une promotion appliquée juste avant retire une figurine).
            if (!await Roster.DismissAsync(row.Warrior, row.EffectiveDismissCount)) continue;
            sentences.Add(string.Format(Loc["HistoryWarriorDismissedSentence"], row.Name));
        }
    }

    /// <summary>Étape "Achat/Vente d'équipement" (livre, étape 9 - "Reallocate equipment"). Achat :
    /// débite le coût (l'objet rejoint la réserve via BuildReserve). Vente : livre des règles -
    /// "Warriors can automatically sell equipment for half its listed price" - crédite SellPrice (déjà
    /// calculé sur le candidat) ; seule une vente d'équipement PORTÉ par un guerrier touche la base ici
    /// (WarriorEquipment, "trade in weapons and equipment... swapped around the warband"), une vente de
    /// réserve étant déjà retranchée de la réserve finale. Vente partielle d'une ligne portée : supprime
    /// puis recrée le reliquat (aucune méthode de service de réduction partielle) - ne préserve pas
    /// BlessingRule sur le reliquat, lacune acceptée (une arme bénie fait rarement partie d'une pile).</summary>
    private async Task ApplyEquipmentTradingAsync(List<string> sentences)
    {
        if (Warband is null) return;

        var netGold = 0;
        foreach (var pick in PurchasedReserveItems)
        {
            netGold -= pick.Cost;
            sentences.Add(string.Format(Loc["HistoryEquipmentPurchasedSentence"], pick.Name));
        }

        foreach (var candidate in PendingSales)
        {
            if (candidate.CarriedItem is { } carriedItem)
                await Roster.SellCarriedAsync(carriedItem, candidate.SelectedQuantity);

            netGold += candidate.SellPrice;
            sentences.Add(string.Format(Loc["HistoryEquipmentSoldSentence"], candidate.SoldLabel, candidate.SellPrice));
        }

        await Roster.AdjustTreasuryAsync(Warband, netGold);
    }

    /// <summary>Étape "Recrutement" (livre, étape 8, suite) - même principe qu'ApplyRecruitmentAsync
    /// (Héros). Groupes existants renforcés : équipement et coût total repris de topUpPlans (PlanTopUps +
    /// GetTopUpBreakdown, figés en tête de Finish - la même source que l'aperçu). Figer le coût règle au
    /// passage un double compte : la surtaxe d'XP (GroupExperience) relue ici APRÈS
    /// ApplyWarriorOutcomesAsync incluait deux fois l'XP de cette partie, déjà ajoutée au guerrier. Nouveau
    /// groupe : chaque pick coûte RecruitPickCost × effectif (0 si servi par la réserve/IsFree).</summary>
    private async Task ApplyHenchmanRecruitmentAsync(IReadOnlyDictionary<ExistingHenchmanTopUp, (List<TopUpEquipmentPlan> Plan, HenchmanTopUpCostBreakdown Breakdown)> topUpPlans,
        HashSet<EquipmentPick> servedReservePicks, List<string> sentences)
    {
        if (Warband is null) return;

        var totalCost = 0;

        foreach (var (topUp, (plan, breakdown)) in topUpPlans)
        {
            // Les vétérans portent l'équipement du groupe (quantités par modèle) : payé ou pris dans la réserve
            // (breakdown/plan), aucune ligne d'équipement à ajouter - seul l'effectif change.
            var warrior = topUp.Row.Warrior;
            await Roster.TopUpAsync(warrior, topUp.AddCount);
            totalCost += breakdown.Total;
            sentences.Add(string.Format(Loc["HistoryHenchmanTopUpSentence"], topUp.AddCount, warrior.Name));
        }

        foreach (var row in HenchmanRecruitRows)
        {
            foreach (var group in row.HenchmanGroupDrafts)
            {
                var name = group.Name.Trim();
                // Équipement enregistré PAR MODÈLE (« Épée x1 » pour un groupe de 3), payé pour chaque modèle.
                await Roster.RecruitAsync(Warband.Id, new RecruitDraft(row.Archetype, name, group.Count,
                    group.Equipment.Select(p => (p.Item, p.MaterialRule)).ToList(), group.Mutations.ToList()));
                totalCost += group.Count * (row.Cost + group.Equipment.Sum(p => RecruitPickCost(p, servedReservePicks)) + group.Mutations.Sum(m => m.Cost));

                sentences.Add(string.Format(Loc["HistoryRecruitedSentence"], name, row.Archetype.Name));
            }
        }

        await Roster.AdjustTreasuryAsync(Warband, -totalCost);
    }

    /// <summary>Étape "Réallouer l'équipement" (livre, étape 9) - côté Héros uniquement : leur
    /// Warrior.Equipment a été muté en mémoire pendant la session (voir EndOfGamePageViewModel.
    /// Reallocation.cs). Diffe chaque ligne courante contre son instantané d'origine
    /// (WarriorOutcomeRow.OriginalEquipmentIds/OriginalEquipmentQuantities) : un id d'origine absent OU à
    /// Quantity réduite = tout ou partie parti ailleurs (Remove, puis Add du reliquat) ; un id NÉGATIF =
    /// arrivé d'ailleurs (Add). Les recrues sont déjà traitées par ApplyRecruitmentAsync, la Réserve par
    /// ApplyReserveAsync (déplacements _reallocationReserveMoves) - seule la phrase d'historique des objets
    /// rangés en réserve est émise ici.</summary>
    private async Task ApplyEquipmentReallocationAsync(List<string> sentences)
    {
        if (Warband is null) return;

        // Un Héros mort cette bataille n'a jamais été porteur à l'étape Réallouer (voir ReallocationCarriers) :
        // rien à synchroniser pour lui.
        foreach (var row in WarriorRows.Where(r => r.IsHero && !r.IsDead))
        {
            var arrivedLines = await Roster.ApplyEquipmentChangesAsync(row.Warrior,
                row.OriginalEquipmentIds.ToDictionary(id => id, id => row.OriginalEquipmentQuantities[id]));
            foreach (var arrived in arrivedLines)
            {
                sentences.Add(string.Format(Loc["HistoryEquipmentReallocatedSentence"], arrived.NameDisplay, row.Name));
            }
        }

        foreach (var move in _reallocationReserveMoves.Where(m => m.In is not null))
        {
            var inflow = move.In!;
            var name = inflow.MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $"{inflow.Item.Name} ({abbr})" : inflow.Item.Name;
            sentences.Add(string.Format(Loc["HistoryEquipmentReallocatedSentence"], name, Loc["EndOfGameEquipmentTradingStashSource"]));
        }
    }
}
