using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Pipeline de persistance de Fin de Partie (Apply*Async) - Recrutement/Renvoyer/Achat
/// et Vente d'équipement/Réallocation (étapes 8-9 du livre). Scindé depuis EndOfGamePageViewModel.
/// Apply.cs (2026-09-23, retour utilisateur - "on a un view model assez énorme est ce qu'on peut
/// le découper") : pur déplacement de méthode(s), aucun changement de comportement. Voir
/// EndOfGamePageViewModel.cs's Finish() pour l'ordre d'appel complet du pipeline.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Étape "Recrutement" (livre, étape 8) - transforme WarriorRecruitRow/WarriorNameSlot
    /// (EndOfGamePageViewModel.Recruitment.cs, de simples brouillons en mémoire jusqu'ici) en vrais
    /// Warrior, au même moment que tout le reste de ce wizard (Terminer) - jamais avant, voir la doc de
    /// classe d'EndOfGamePageViewModel.Recruitment.cs pour pourquoi (refus explicite de l'utilisateur
    /// d'un mécanisme "recruter puis annuler"). Miroir de la boucle Héros de WarbandEditDialogViewModel.
    /// Save() - pas un appel direct à cette méthode, simplifiée : toujours une recrue NEUVE (jamais
    /// ExistingWarrior à synchroniser), équipement toujours Commun (voir AddRecruitEquipment.commonOnly).
    /// Cette passe ne couvre que les Héros (voir HeroRecruitRows) - Hommes de main (groupes existants avec
    /// le budget vétérans, ou tout nouveau groupe) restent à construire, voir EndOfGamePageViewModel.
    /// Recruitment.cs.</summary>
    private async Task ApplyRecruitmentAsync(string language, List<string> sentences)
    {
        if (Warband is null) return;

        // Équipement acheté à l'onglet Équipement de l'étape Recrutement - réserve en priorité (étape
        // Achat/Vente juste avant, déjà appliquée à ce stade du pipeline), sinon plein tarif - voir
        // ConsumeReserveOrBuyAsync's own doc. Persisté directement sur ce Héros (2026-09-05, retour
        // utilisateur - "on peut mélanger les deux, comme dans le warband edit" - abandon du plan
        // antérieur "achat versé dans la réserve").
        var stashPool = await _warbandService.GetWarbandEquipmentAsync(Warband.Id, language);
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
                    if (pick.IsFree)
                    {
                        // Dague gratuite déjà déterminée au moment du pick (AddRecruitEquipment, sensible
                        // à ce que ce slot porte déjà ou non - contrairement à ConsumeReserveOrBuyAsync's
                        // propre règle interne, pensée pour le top-up des groupes existants) - jamais
                        // re-dérivée ici, coûte 0 quelle que soit sa provenance.
                        await _warbandService.AddWarriorEquipmentAsync(warrior.Id, pick.Item, materialRule: pick.MaterialRule);
                    }
                    else
                    {
                        totalCost += await ConsumeReserveOrBuyAsync(stashPool, warrior.Id, pick.Item, pick.MaterialRule, quantity: 1, applyFreeDaggerRule: false);
                    }
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

    /// <summary>Réserve en priorité, sinon achète au plein tarif - factorisé ici (ApplyRecruitmentAsync/
    /// ApplyHenchmanRecruitmentAsync) plutôt que dupliqué en closure locale par méthode. stashPool est
    /// MUTÉ en place (List, passé par référence) au fil des appels successifs - chaque appelant doit le
    /// fetcher une seule fois via GetWarbandEquipmentAsync puis le réutiliser pour tous ses propres appels,
    /// jamais re-fetcher entre deux consommations du même pipeline. applyFreeDaggerRule: true seulement
    /// pour le top-up d'un groupe EXISTANT (chaque unité achetée ici EST la dague personnelle gratuite
    /// d'une recrue neuve différente, voir ApplyHenchmanRecruitmentAsync's own doc) - false pour un pick
    /// Héros/nouveau groupe, où IsFree est déjà déterminé au moment du pick (AddRecruitEquipment) et geré
    /// séparément par l'appelant, jamais re-dérivé ici.</summary>
    private async Task<int> ConsumeReserveOrBuyAsync(List<WarbandEquipment> stashPool, int warriorId, EquipmentItem item, SpecialRule? materialRule, int quantity, bool applyFreeDaggerRule)
    {
        var remaining = quantity;
        foreach (var stashRow in stashPool.Where(w => w.Item.Id == item.Id && w.MaterialRule?.Id == materialRule?.Id).ToList())
        {
            if (remaining <= 0) break;
            await _warbandService.RemoveWarbandEquipmentAsync(stashRow.Id);
            await _warbandService.AddWarriorEquipmentAsync(warriorId, stashRow.Item, materialRule: stashRow.MaterialRule, foundValueOverride: stashRow.FoundValueOverride);
            stashPool.Remove(stashRow);
            remaining--;
        }

        if (remaining <= 0) return 0;

        await _warbandService.AddWarriorEquipmentAsync(warriorId, item, quantity: remaining, materialRule: materialRule);
        var isFreeDagger = applyFreeDaggerRule && item.IsFreeDagger && materialRule is null;
        return remaining * EquipmentPricing.CalculateCost(item.Cost, materialRule?.CostMultiplier, isFree: isFreeDagger);
    }

    /// <summary>Étape "Renvoyer" (livre des règles - "Disbanding a Warband" + FAQ officielle - "you are
    /// allowed to dismiss any warrior at any time during the post-battle sequence... transfer the
    /// warrior's weapons and gear to your stash and then dismiss him") - juste AVANT Achat/Vente dans le
    /// pipeline. Un Héros (DismissCount == HeadCount == 1) ou un groupe d'Hommes de main ENTIÈREMENT
    /// renvoyé (DismissCount == HeadCount) restitue TOUT son équipement à la réserve puis passe
    /// WarriorStatus.Retired (jamais DeleteWarriorAsync, qui effacerait son historique - même statut que
    /// la retraite d'Œil crevé). Un renvoi PARTIEL d'un groupe (DismissCount &lt; HeadCount) restitue
    /// seulement la part des figurines qui partent (Quantity × DismissCount, Quantity étant une quantité
    /// PAR MODÈLE - voir ExistingHenchmanTopUp.GetTopUpBreakdown's own doc) et laisse la ligne
    /// WarriorEquipment elle-même intacte (les figurines restantes portent toujours la même quantité par
    /// modèle), ne touchant que Warrior.HeadCount.</summary>
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

            foreach (var equipment in warrior.Equipment.ToList())
            {
                await _warbandService.AddWarbandEquipmentAsync(Warband.Id, equipment.Item,
                    quantity: equipment.Quantity * dismissCount, materialRule: equipment.MaterialRule,
                    foundValueOverride: equipment.FoundValueOverride);
                if (fullyDismissed)
                    await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
            }

            if (fullyDismissed)
                warrior.Status = WarriorStatus.Retired;
            else
                warrior.HeadCount -= dismissCount;

            await _warbandService.SaveWarriorAsync(warrior);
            sentences.Add(string.Format(Loc["HistoryWarriorDismissedSentence"], row.Name));
        }
    }

    /// <summary>Étape "Achat/Vente d'équipement" (livre, étape 9 - "Reallocate equipment") - juste AVANT
    /// Recrutement dans le pipeline (voir SaveAsync's own commentaire d'ordre). Achat : rejoint la réserve
    /// (jamais assigné à un guerrier ici). Vente : livre des règles - "Warriors can automatically sell
    /// equipment for half its listed price... the warband receives half of the basic cost only" pour un
    /// prix variable - retire de la réserve (WarbandEquipment) OU du guerrier qui le porte
    /// (WarriorEquipment, "trade in weapons and equipment... swapped around the warband" - pas seulement
    /// la réserve), crédite SellPrice (déjà calculé sur le candidat, jamais recalculé ici).
    ///
    /// **Vente partielle** (retour utilisateur 2026-09-21, revu par rapport à une première version "ligne
    /// entière uniquement") : candidate.SelectedQuantity peut être inférieur à la quantité réelle de la
    /// ligne d'origine - supprime toujours la ligne existante puis recrée le reliquat (aucune méthode de
    /// service dédiée à la réduction partielle, voir IWarbandService), MaterialRule/FoundValueOverride
    /// reportés sur la ligne recréée. Ne préserve PAS BlessingRule sur le reliquat porté (AddWarriorEquipmentAsync
    /// ne l'accepte pas) - lacune acceptée, une arme bénie fait rarement partie d'une pile de quantité &gt; 1.
    ///
    /// Un candidat IsFromExploration (trouvaille de cette même partie) n'a pas de StashItem connu à
    /// l'ouverture du dialog - ApplyExplorationOutcomeAsync ne l'a ajouté en base que juste avant cette
    /// étape (voir SaveAsync's own commentaire d'ordre) - re-résout le(s) vrai(s) WarbandEquipment
    /// fraîchement créé(s) via un fetch tardif, même idiome que ConsumeReserveOrBuyAsync (jamais réutiliser
    /// un id connu à l'ouverture du dialog, qui n'existait pas encore) ; peut consommer plusieurs lignes
    /// (une trouvaille peut avoir été ajoutée en plusieurs AddWarbandEquipmentAsync distincts - objet
    /// principal + objet bonus identiques par exemple) et réduire partiellement la dernière.</summary>
    private async Task ApplyEquipmentTradingAsync(string language, List<string> sentences)
    {
        if (Warband is null) return;

        var netGold = 0;
        List<WarbandEquipment>? freshStashForExplorationSales = null;

        foreach (var pick in PurchasedReserveItems)
        {
            await _warbandService.AddWarbandEquipmentAsync(Warband.Id, pick.Item, materialRule: pick.MaterialRule);
            netGold -= pick.Cost;
            sentences.Add(string.Format(Loc["HistoryEquipmentPurchasedSentence"], pick.Name));
        }

        foreach (var candidate in PendingSales)
        {
            if (candidate.ReserveSource is { } line)
            {
                // La vente a déjà décrémenté line.Quantity en direct (AddSaleEquipment/ReserveCollection.
                // RemoveLine) - il ne reste qu'à synchroniser la vraie ligne DB (SourceId, toujours réel
                // pour cette provenance) sur cette quantité finale, jamais besoin de re-lire SelectedQuantity ici.
                await _warbandService.RemoveWarbandEquipmentAsync(line.SourceId!.Value);
                if (line.Quantity > 0)
                    await _warbandService.AddWarbandEquipmentAsync(Warband.Id, line.Item, quantity: line.Quantity, materialRule: line.MaterialRule, foundValueOverride: line.FoundValueOverride);
            }
            else if (candidate.CarriedItem is { } carriedItem)
            {
                await _warbandService.RemoveWarriorEquipmentAsync(carriedItem.Id);
                var leftover = carriedItem.Quantity - candidate.SelectedQuantity;
                if (leftover > 0)
                    await _warbandService.AddWarriorEquipmentAsync(carriedItem.WarriorId, carriedItem.Item, quantity: leftover, materialRule: carriedItem.MaterialRule, foundValueOverride: carriedItem.FoundValueOverride);
            }
            else if (candidate.IsFromExploration || candidate.IsFromDismissal)
            {
                // Même mécanisme pour les deux provenances : ni l'une ni l'autre n'a de WarbandEquipment.Id
                // stable au moment de construire ce candidat (ApplyExplorationOutcomeAsync/
                // ApplyDismissalsAsync ne le crée qu'à Terminer, juste avant cette méthode) - re-résolu ici
                // par un fetch tardif plutôt qu'un id connu à l'avance.
                freshStashForExplorationSales ??= await _warbandService.GetWarbandEquipmentAsync(Warband.Id, language);
                var remaining = candidate.SelectedQuantity;
                foreach (var row in freshStashForExplorationSales.Where(w => w.Item.Id == candidate.Item.Id && w.MaterialRule?.Id == candidate.MaterialRule?.Id).ToList())
                {
                    if (remaining <= 0) break;
                    await _warbandService.RemoveWarbandEquipmentAsync(row.Id);
                    freshStashForExplorationSales.Remove(row);
                    if (row.Quantity > remaining)
                    {
                        var recreated = await _warbandService.AddWarbandEquipmentAsync(Warband.Id, row.Item, quantity: row.Quantity - remaining, materialRule: row.MaterialRule, foundValueOverride: row.FoundValueOverride);
                        freshStashForExplorationSales.Add(recreated);
                        remaining = 0;
                    }
                    else
                    {
                        remaining -= row.Quantity;
                    }
                }
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
    /// (Héros) : brouillon en mémoire depuis EndOfGamePageViewModel.Recruitment.cs, appliqué ici
    /// seulement à Terminer. Groupes existants (ExistingHenchmanTopUps, budget vétérans déjà validé côté
    /// wizard, SEUL endroit où l'équipement se calcule automatiquement) puis un éventuel nouveau groupe
    /// (HenchmanRecruitRows/HenchmanGroupDrafts, équipé à l'onglet Équipement de la même étape - réserve en
    /// priorité comme les groupes existants ci-dessous, voir ConsumeReserveOrBuyAsync's own doc, sinon
    /// plein tarif). Réserve rechargée FRAÎCHEMENT depuis la base ici (pas un instantané séparé propre à cette étape
    /// _warbandInventory, un instantané figé à l'ouverture du wizard) pour refléter tout ce qu'un apply
    /// step précédent dans CE MÊME pipeline (Achat/Vente d'équipement, "Céder du matériel"...) a déjà
    /// retiré/ajouté à la réserve.</summary>
    private async Task ApplyHenchmanRecruitmentAsync(string language, List<string> sentences)
    {
        if (Warband is null) return;

        var stashPool = await _warbandService.GetWarbandEquipmentAsync(Warband.Id, language);
        var totalCost = 0;

        foreach (var topUp in ExistingHenchmanTopUps.Where(t => t.AddCount > 0))
        {
            var warrior = topUp.Row.Warrior;
            // Capturé AVANT l'incrément ci-dessous : un groupe est UN SEUL Warrior (HeadCount = l'effectif),
            // donc WarriorEquipment.Quantity y est déjà le TOTAL pour tout le groupe (ex. 3 Épées pour 3
            // Guerriers), jamais "par modèle" - diviser par l'effectif ACTUEL (avant ce top-up) retrouve la
            // quantité par modèle, voir EndOfGamePageViewModel.GetTopUpBreakdown (même bug/même fix, même
            // retour utilisateur 2026-09-05 - "on rajoute un guerrier... l'épée coute 30, hors l'épée coute
            // 10"). Muter HeadCount avant de lire cette quantité aurait faussé le calcul.
            var groupHeadCount = Math.Max(1, warrior.HeadCount);
            warrior.HeadCount += topUp.AddCount;
            await _warbandService.SaveWarriorAsync(warrior);

            foreach (var equipment in topUp.CurrentEquipment)
                totalCost += await ConsumeReserveOrBuyAsync(stashPool, warrior.Id, equipment.Item, equipment.MaterialRule,
                    quantity: topUp.AddCount * equipment.Quantity / groupHeadCount, applyFreeDaggerRule: true);

            totalCost += topUp.AddCount * (topUp.ArchetypeCost + 2 * topUp.GroupExperience);
            sentences.Add(string.Format(Loc["HistoryHenchmanTopUpSentence"], topUp.AddCount, warrior.Name));
        }

        foreach (var row in HenchmanRecruitRows)
        {
            foreach (var group in row.HenchmanGroupDrafts)
            {
                var name = group.Name.Trim();
                var warrior = await _warbandService.RecruitWarriorAsync(Warband.Id, row.Archetype, name, headCount: group.Count);
                totalCost += group.Count * row.Cost;

                // Équipement acheté à l'onglet Équipement de l'étape Recrutement - réserve en priorité
                // (ConsumeReserveOrBuyAsync), sinon plein tarif. Quantity = group.Count (pas 1) : un
                // groupe est UN SEUL Warrior (HeadCount = l'effectif), donc Quantity y représente déjà le
                // TOTAL pour tout le groupe - même convention que le fix ci-dessus sur les groupes
                // existants (bug 2026-09-05, "l'épée coute 30, hors l'épée coute 10").
                foreach (var pick in group.Equipment)
                {
                    if (pick.IsFree)
                    {
                        // Dague gratuite déjà déterminée au moment du pick (AddRecruitEquipment) - jamais
                        // re-dérivée ici, voir ApplyRecruitmentAsync's même logique côté Héros.
                        await _warbandService.AddWarriorEquipmentAsync(warrior.Id, pick.Item, quantity: group.Count, materialRule: pick.MaterialRule);
                        continue;
                    }
                    totalCost += await ConsumeReserveOrBuyAsync(stashPool, warrior.Id, pick.Item, pick.MaterialRule, quantity: group.Count, applyFreeDaggerRule: false);
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


    /// <summary>Étape "Réallouer l'équipement" (livre, étape 9) - TOUT DERNIER dans le pipeline (voir
    /// EndOfGame's commentaire d'ordre), pour voir l'état final de tout ce que Renvoyer/Vente/Recrutement
    /// ont déjà appliqué. WarriorRows[i].Warrior.Equipment/ReallocationReserve ont été
    /// mutés directement en mémoire pendant la session interactive (voir EndOfGamePageViewModel.
    /// Reallocation.cs) - jamais persisté avant cet appel. Diffe chaque ligne courante contre son
    /// instantané d'origine (WarriorOutcomeRow.OriginalEquipmentIds/OriginalEquipmentQuantities côté
    /// Héros, _originalReserveSnapshot côté Réserve, tous capturés avant toute interaction) : un id
    /// d'origine absent maintenant OU présent avec une Quantity réduite (déplacement PARTIEL, 2026-09-23
    /// - voir MoveReallocationItem) = tout ou partie parti ailleurs (RemoveXxxAsync, puis
    /// AddXxxAsync du reliquat si &gt; 0) ; un id NÉGATIF présent maintenant = arrivé d'ailleurs
    /// (AddXxxAsync, jamais un vrai id avant cet appel - voir ReallocatableItem/
    /// AddToReallocationCarrier). Les recrues n'ont rien de spécial ici : ApplyRecruitmentAsync (plus haut
    /// dans le pipeline) a déjà traité WarriorNameSlot.Equipment tel quel, cette étape n'y touche que
    /// PENDANT la session interactive (Add/Remove direct sur ce même brouillon), jamais à Terminer.
    ///
    /// Cas particulier - "extras" de réserve (ReserveLine.SourceId null, voir EndOfGamePageViewModel.
    /// Reallocation.cs's EnsureReallocationReserveExtrasAdded) : un objet trouvé à l'Exploration/renvoyé/
    /// acheté PENDANT ce même wizard n'a pas encore de vraie ligne WarbandEquipment au moment d'ouvrir
    /// cette étape, mais EN AURA une par les étapes plus tôt dans CE MÊME pipeline
    /// (ApplyExplorationOutcomeAsync/ApplyDismissalsAsync/ApplyEquipmentTradingAsync, toutes avant
    /// celle-ci) - jamais recréé ici si le joueur ne l'a pas déplacé (déjà couvert), mais s'il l'a déplacé
    /// vers un Héros/une recrue, la VRAIE ligne (déjà créée) doit être retrouvée par une requête fraîche
    /// (même idiome que SellableEquipmentCandidate.IsFromExploration/IsFromDismissal dans
    /// ApplyEquipmentTradingAsync) et supprimée - un simple diff par id est impossible ici puisque toutes
    /// les entrées "extras" partagent SourceId == null.</summary>
    private async Task ApplyEquipmentReallocationAsync(string language, List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var row in WarriorRows.Where(r => r.IsHero))
        {
            // Compare chaque id d'origine à son état courant (absent = déplacé entièrement ailleurs,
            // Quantity réduite = déplacement PARTIEL - voir MoveReallocationItem/
            // RemoveFromReallocationCarrier) - les deux cas se synchronisent de la même façon (delete +
            // recreate, seule primitive disponible côté service, pas d'update de quantité en place) ;
            // inchangé (même Quantity qu'à l'ouverture du wizard) ne fait rien.
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

        // Même principe côté Réserve : _originalReserveSnapshot porte déjà SourceId+Quantity figés (pas
        // besoin d'un second dictionnaire comme côté Héros) - absente ou Quantity réduite synchronisent
        // toutes deux vers la DB, inchangée ne fait rien.
        foreach (var original in _originalReserveSnapshot)
        {
            var current = ReallocationReserve.FirstOrDefault(w => w.SourceId == original.SourceId);
            if (current is not null && current.Quantity == original.Quantity) continue;

            await _warbandService.RemoveWarbandEquipmentAsync(original.SourceId!.Value);
            if (current is { Quantity: > 0 })
                await _warbandService.AddWarbandEquipmentAsync(Warband.Id, current.Item, current.Quantity, current.MaterialRule, current.FoundValueOverride);
        }

        foreach (var arrived in ReallocationReserve.Where(w => w.SourceId < 0).ToList())
        {
            await _warbandService.AddWarbandEquipmentAsync(Warband.Id, arrived.Item, arrived.Quantity, arrived.MaterialRule, arrived.FoundValueOverride);
            sentences.Add(string.Format(Loc["HistoryEquipmentReallocatedSentence"], arrived.NameDisplay, Loc["EndOfGameEquipmentTradingStashSource"]));
        }

        if (ReallocationReserveExtrasOriginal.Count > 0)
        {
            List<WarbandEquipment>? freshStash = null;
            foreach (var group in ReallocationReserveExtrasOriginal.GroupBy(x => (x.Item.Id, MaterialRuleId: x.MaterialRule?.Id)))
            {
                var originalQty = group.Sum(x => x.Quantity);
                var stillThereQty = ReallocationReserve
                    .Where(w => w.SourceId is null && w.Item.Id == group.Key.Id && w.MaterialRule?.Id == group.Key.MaterialRuleId)
                    .Sum(w => w.Quantity);
                var movedAwayQty = originalQty - stillThereQty;
                if (movedAwayQty <= 0) continue;

                freshStash ??= await _warbandService.GetWarbandEquipmentAsync(Warband.Id, language);
                var remaining = movedAwayQty;
                foreach (var row in freshStash.Where(w => w.Item.Id == group.Key.Id && w.MaterialRule?.Id == group.Key.MaterialRuleId).ToList())
                {
                    if (remaining <= 0) break;
                    await _warbandService.RemoveWarbandEquipmentAsync(row.Id);
                    freshStash.Remove(row);
                    if (row.Quantity > remaining)
                    {
                        var recreated = await _warbandService.AddWarbandEquipmentAsync(Warband.Id, row.Item, quantity: row.Quantity - remaining, materialRule: row.MaterialRule, foundValueOverride: row.FoundValueOverride);
                        freshStash.Add(recreated);
                        remaining = 0;
                    }
                    else
                    {
                        remaining -= row.Quantity;
                    }
                }
            }
        }
    }
}
