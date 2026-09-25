using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
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
                var warrior = await _warbandService.RecruitWarriorAsync(Warband.Id, row.Archetype, name);
                totalCost += row.Cost;

                foreach (var pick in slot.Equipment)
                {
                    await _warbandService.AddWarriorEquipmentAsync(warrior.Id, pick.Item, materialRule: pick.MaterialRule);
                    totalCost += RecruitPickCost(pick, servedReservePicks);
                }

                // Mutations achetées au recrutement (Possédés/Mutants - livre, "+ the cost of mutations").
                foreach (var mutation in slot.Mutations)
                {
                    await _warbandService.AddWarriorMutationAsync(warrior.Id, mutation);
                    totalCost += mutation.Cost;
                }

                sentences.Add(string.Format(Loc["HistoryRecruitedSentence"], name, row.Archetype.Name));
            }
        }

        if (totalCost > 0)
        {
            Warband.Treasury -= totalCost;
            await _warbandService.SaveWarbandAsync(Warband);
        }
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

        foreach (var row in WarriorRows.Where(r => r.DismissCount > 0))
        {
            var warrior = row.Warrior;
            // Filet de sécurité : le stepper de cette étape borne DismissCount sur le HeadCount affiché
            // PENDANT le wizard (jamais remis à jour en direct par les morts de CETTE bataille, qui ne
            // mutent Warrior.HeadCount qu'à ApplyWarriorOutcomesAsync, juste avant cet appel - même
            // limitation acceptée que partout ailleurs dans ce wizard). Reclamper ici contre le HeadCount
            // RÉEL évite un HeadCount négatif si des figurines sont mortes entre-temps.
            var dismissCount = Math.Min(row.DismissCount, warrior.HeadCount);
            if (dismissCount <= 0) continue;

            var fullyDismissed = dismissCount >= warrior.HeadCount;

            if (fullyDismissed)
            {
                foreach (var equipment in warrior.Equipment.ToList())
                    await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                warrior.Status = WarriorStatus.Retired;
            }
            else
            {
                warrior.HeadCount -= dismissCount;
            }

            await _warbandService.SaveWarriorAsync(warrior);
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
            {
                await _warbandService.RemoveWarriorEquipmentAsync(carriedItem.Id);
                var leftover = carriedItem.Quantity - candidate.SelectedQuantity;
                if (leftover > 0)
                    await _warbandService.AddWarriorEquipmentAsync(carriedItem.WarriorId, carriedItem.Item, quantity: leftover, materialRule: carriedItem.MaterialRule, foundValueOverride: carriedItem.FoundValueOverride);
            }

            netGold += candidate.SellPrice;
            sentences.Add(string.Format(Loc["HistoryEquipmentSoldSentence"], candidate.SoldLabel, candidate.SellPrice));
        }

        if (netGold != 0)
        {
            Warband.Treasury += netGold;
            await _warbandService.SaveWarbandAsync(Warband);
        }
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
            var warrior = topUp.Row.Warrior;
            warrior.HeadCount += topUp.AddCount;
            await _warbandService.SaveWarriorAsync(warrior);

            foreach (var line in plan.Where(l => l.NeededQty > 0))
                await _warbandService.AddWarriorEquipmentAsync(warrior.Id, line.Equipment.Item, quantity: line.NeededQty, materialRule: line.Equipment.MaterialRule);

            totalCost += breakdown.Total;
            sentences.Add(string.Format(Loc["HistoryHenchmanTopUpSentence"], topUp.AddCount, warrior.Name));
        }

        foreach (var row in HenchmanRecruitRows)
        {
            foreach (var group in row.HenchmanGroupDrafts)
            {
                var name = group.Name.Trim();
                var warrior = await _warbandService.RecruitWarriorAsync(Warband.Id, row.Archetype, name, headCount: group.Count);
                totalCost += group.Count * row.Cost;

                // Quantity = group.Count : un groupe est UN SEUL Warrior (HeadCount = l'effectif), même
                // convention que le reste du recrutement (bug 2026-09-05, "l'épée coute 30, hors l'épée
                // coute 10").
                foreach (var pick in group.Equipment)
                {
                    await _warbandService.AddWarriorEquipmentAsync(warrior.Id, pick.Item, quantity: group.Count, materialRule: pick.MaterialRule);
                    totalCost += RecruitPickCost(pick, servedReservePicks) * group.Count;
                }

                foreach (var mutation in group.Mutations)
                {
                    await _warbandService.AddWarriorMutationAsync(warrior.Id, mutation);
                    totalCost += mutation.Cost * group.Count;
                }

                sentences.Add(string.Format(Loc["HistoryRecruitedSentence"], name, row.Archetype.Name));
            }
        }

        if (totalCost > 0)
        {
            Warband.Treasury -= totalCost;
            await _warbandService.SaveWarbandAsync(Warband);
        }
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

        foreach (var row in WarriorRows.Where(r => r.IsHero))
        {
            foreach (var id in row.OriginalEquipmentIds)
            {
                var current = row.Warrior.Equipment.FirstOrDefault(we => we.Id == id);
                if (current is not null && current.Quantity == row.OriginalEquipmentQuantities[id]) continue;

                await _warbandService.RemoveWarriorEquipmentAsync(id);
                if (current is { Quantity: > 0 })
                {
                    var recreated = await _warbandService.AddWarriorEquipmentAsync(row.Warrior.Id, current.Item, current.Quantity, current.MaterialRule, current.FoundValueOverride);
                    if (current.BlessingRule is { } keptBlessing)
                        await _warbandService.SetWarriorEquipmentBlessingRuleAsync(recreated.Id, keptBlessing.Id);
                }
            }

            foreach (var arrived in row.Warrior.Equipment.Where(we => we.Id < 0).ToList())
            {
                var created = await _warbandService.AddWarriorEquipmentAsync(row.Warrior.Id, arrived.Item, arrived.Quantity, arrived.MaterialRule, arrived.FoundValueOverride);
                if (arrived.BlessingRule is { } blessing)
                    await _warbandService.SetWarriorEquipmentBlessingRuleAsync(created.Id, blessing.Id);
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
