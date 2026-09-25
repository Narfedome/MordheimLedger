using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Pipeline de persistance de Fin de Partie (Apply*Async) - étape Exploration. Scindé
/// depuis EndOfGamePageViewModel.Apply.cs (2026-09-23, retour utilisateur - "on a un view model
/// assez énorme est ce qu'on peut le découper") : pur déplacement de méthode(s), aucun changement
/// de comportement. Voir EndOfGamePageViewModel.cs's Finish() pour l'ordre d'appel complet du
/// pipeline.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Étape Exploration : au plus une Outcome "principale" résolue par jet (ExplorationChart.
    /// DetectMultiples ne déclenche jamais plusieurs entrées de la table à la fois, voir Core.Rules),
    /// plus un éventuel objet bonus sur ce même jet (Boutique - voir BonusItemOutcome). L'or/objet/
    /// pierre magique trouvé de cette façon s'ajoute à la trésorerie/à l'inventaire exactement comme
    /// n'importe quel autre gain de la partie.</summary>
    private async Task ApplyExplorationOutcomeAsync(List<EquipmentItem> englishEquipment,
        IReadOnlyDictionary<string, EquipmentItem> equipmentItemsByEnglishName, List<SpecialRule> englishSpecialRules, List<string> sentences)
    {
        if (Warband is null) return;

        // Le dé bonus en attente (Traînard, voir Warband.PendingExplorationBonusDie) a été montré comme
        // rappel textuel à cette étape (ShowPendingExplorationBonusDieReminder) - une
        // fois cette Fin de Partie sauvegardée, il est consommé qu'il ait servi ou non (même logique que
        // n'importe quelle ressource trouvée-mais-pas-utilisée).
        if (Warband.PendingExplorationBonusDie)
        {
            Warband.PendingExplorationBonusDie = false;
            await _warbandService.SaveWarbandAsync(Warband);
        }

        // Même idiome pour le pense-bête "prochaine partie" (Cimetière, catch-all : "Chasseurs de
        // Sorcières/Sœurs de Sigmar vous haïssent" - voir Warband.NextGameNote/ExplorationOutcome.
        // NextGameNoteText) - affiché en bannière sur la fiche de bande (WarbandDetailPage) depuis LA
        // partie précédente, consommé qu'il ait servi ou non maintenant que cette nouvelle partie a lieu.
        // Reposé plus bas si CETTE partie déclenche elle-même un nouveau pense-bête.
        if (Warband.NextGameNote is not null)
        {
            Warband.NextGameNote = null;
            await _warbandService.SaveWarbandAsync(Warband);
        }

        // "Add the results together and consult the chart..." (Core.Rules.WyrdstoneShardsTable) - un
        // SECOND effet du même jet, entièrement additif à l'éventuel résultat de doublons/triplets
        // ci-dessous (TriggeredExplorationResult) : s'applique TOUJOURS, même sans aucun résultat
        // spécial déclenché. BaselineWyrdstoneShardsFound vaut 0 si aucun Héros n'a
        // survécu (aucun dé lancé), donc rien à faire dans ce cas.
        if (BaselineWyrdstoneShardsFound > 0)
        {
            Warband.WyrdstoneShards += BaselineWyrdstoneShardsFound;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryBaselineWyrdstoneSentence"], BaselineWyrdstoneShardsFound));
        }

        // Phrase d'Historique seulement : l'objet lui-même entre dans la réserve via PendingExplorationItems
        // (même résolution, déjà faite pendant le wizard), créée en base une seule fois par
        // ApplyReserveAsync - plus aucune écriture de WarbandEquipment ici depuis le 2026-09-25.
        // equipmentItemsByEnglishName donne directement l'item résolu dans la langue courante. Les
        // paramètres matériau/valeur trouvée restent pour garder les appels ci-dessous inchangés.
        Task AddOneItemToInventoryAsync(string itemName, int quantity, string? materialRuleName, int? foundValueOverride = null)
        {
            if (quantity > 0 && englishEquipment.Any(e => e.Name == itemName))
            {
                var displayName = equipmentItemsByEnglishName.GetValueOrDefault(itemName)?.Name ?? itemName;
                sentences.Add(string.Format(Loc["HistoryExplorationItemSentence"], quantity, displayName));
            }
            return Task.CompletedTask;
        }

        if (ResolvedExplorationOutcome is { } outcome)
        {
            if (outcome.Kind == ExplorationOutcomeKind.Gold
                && int.TryParse(ExplorationGoldAmount, out var gold) && gold != 0)
            {
                Warband.Treasury += gold;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryTreasurySentence"], gold));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.Item
                && ChosenExplorationItemName is { } primaryName
                && int.TryParse(ExplorationItemQuantity, out var quantity))
            {
                // ChosenExplorationItemName plutôt que outcome.EquipmentItemName brut : tient compte d'un
                // éventuel choix du joueur entre deux objets (ex. Armurerie 1-2 : Bouclier OU Rondache,
                // voir ExplorationOutcome.AlternativeEquipmentItemName). foundValueOverride : seulement
                // pour une branche dont la valeur trouvée n'est pas le Cost fixe du catalogue (ex.
                // Bijoutier - Pierres de Quartz/Rubis, voir ExplorationOutcome.FoundValueFormula/
                // WarbandEquipment.FoundValueOverride) - null pour toute autre branche Item.
                int? foundValueOverride = outcome.FoundValueFormula is not null
                    && int.TryParse(ExplorationItemFoundValue, out var foundValue) ? foundValue : null;
                await AddOneItemToInventoryAsync(primaryName, quantity, outcome.MaterialRuleName, foundValueOverride);
            }
            else if (outcome.TriggersArtefactRoll && ResolvedArtefactItemName is { } artefactName)
            {
                // Villa d'un Noble, sous-jet 5-6 : l'objet précis vient du second D6 sur la table des
                // Artefacts Magiques (voir Core.Rules.MagicalArtefactTable), jamais de
                // ChosenExplorationItemName - c'est pourquoi ce cas ne tombe pas dans le "else if"
                // Kind.Item ci-dessus (EquipmentItemName reste null sur cette branche).
                await AddOneItemToInventoryAsync(artefactName, 1, null);
            }
            else if (outcome.Kind == ExplorationOutcomeKind.Wyrdstone
                && int.TryParse(ExplorationWyrdstoneAmount, out var shards) && shards != 0)
            {
                Warband.WyrdstoneShards += shards;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryExplorationWyrdstoneSentence"], shards));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.CausesSickness && StatTestSickHero is { } sickHero)
            {
                // Puits en échec (test d'Endurance) - voir WarriorStatus.Sick. Le statut lui-même n'est
                // PAS posé ici : ApplySicknessLifecycleAsync le pose plus tard, APRÈS
                // ApplyWarriorOutcomesAsync qui resynchronise sinon warrior.Status depuis row.Status
                // (Actif/Mort uniquement) et écraserait silencieusement Sick en Actif (bug trouvé le
                // 2026-08-18 : le statut Malade ne "prenait" jamais).
                sentences.Add(string.Format(Loc["HistorySicknessSentence"], sickHero.Name));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.CausesDeath && PitDevouredHero is { } devouredHero)
            {
                // La Fosse, sous-jet 1 (Héros dévoré) - même principe que la Maladie du Puits ci-dessus :
                // le statut Mort n'est PAS posé ici, ApplyPitDeathAsync le pose plus tard, APRÈS
                // ApplyWarriorOutcomesAsync pour la même raison (celle-ci resynchronise sinon
                // warrior.Status depuis row.Status et écraserait silencieusement Mort en Actif).
                sentences.Add(string.Format(Loc["HistoryPitDevouredSentence"], devouredHero.Name));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsNextExplorationBonusDie)
            {
                // Traînard, branche "autres bandes" - voir Warband.PendingExplorationBonusDie, consommé
                // (et son rappel affiché) au tout début de cette méthode lors de la PROCHAINE Fin de
                // Partie, pas celle-ci.
                Warband.PendingExplorationBonusDie = true;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], ExplorationNoteText));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsLeaderExperience is { } leaderXp)
            {
                // Traînard, branche Possédés - même idiome que BonusStatTestLeader (Bâtiment Éventré) :
                // pas d'erreur bloquante si le chef n'est pas disponible cette partie (mort/malade/hors
                // de combat), le bonus est simplement indisponible.
                var leader = _allActiveWarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);
                if (leader is not null)
                {
                    leader.Warrior.Experience += leaderXp;
                    await _warbandService.SaveWarriorAsync(leader.Warrior);
                    sentences.Add(string.Format(Loc["HistoryLeaderExperienceSentence"], leader.Warrior.Name, leaderXp));
                }
                else
                {
                    sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], ExplorationNoteText));
                }
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsFreeHenchmanArchetypeName is { } henchmanName)
            {
                // Traînard/Prisonniers, branche Morts-Vivants ("Zombie") - fusionne dans un groupe
                // d'Hommes de main déjà existant de ce même archétype plutôt que de créer une ligne
                // séparée : un Zombie ne peut porter aucun équipement (CanUseEquipment false), donc deux
                // groupes du même archétype seraient de toute façon rigoureusement identiques. Quantité :
                // 1 fixe (Traînard) ou un jet du joueur (D3, Prisonniers - voir ExplorationOutcome.
                // ItemQuantityFormula réutilisé tel quel, ExplorationItemQuantity) ; repli sur 1 si vide
                // (jamais le cas en pratique - ValidateExplorationResultStep bloque déjà sinon).
                var archetype = (await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, "en"))
                    .FirstOrDefault(a => a.Name == henchmanName);
                if (archetype is not null)
                {
                    var henchmanQuantity = int.TryParse(ExplorationItemQuantity, out var parsedQuantity) ? parsedQuantity : 1;
                    var existingGroup = Henchmen.FirstOrDefault(r => r.Warrior.WarriorArchetypeId == archetype.Id);
                    if (existingGroup is not null)
                    {
                        existingGroup.Warrior.HeadCount += henchmanQuantity;
                        await _warbandService.SaveWarriorAsync(existingGroup.Warrior);
                    }
                    else
                    {
                        await _warbandService.RecruitWarriorAsync(Warband.Id, archetype, archetype.Name, headCount: henchmanQuantity);
                    }
                    sentences.Add(string.Format(Loc["HistoryFreeHenchmanSentence"], henchmanQuantity, archetype.Name));
                }
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsDistributedHeroExperienceFormula is not null)
            {
                // Prisonniers, branche Possédés - le total (D3) a déjà été réparti par le joueur via le
                // steppeur +/- de chaque Héros (DistributedExperienceRemaining vérifié à
                // 0 par ValidateExplorationResultStep) ; ici on ne fait qu'appliquer chaque allocation.
                var recipients = WarriorRows.Where(r => r.DistributedExplorationExperience > 0).ToList();
                foreach (var recipient in recipients)
                {
                    recipient.Warrior.Experience += recipient.DistributedExplorationExperience;
                    await _warbandService.SaveWarriorAsync(recipient.Warrior);
                }
                if (recipients.Count > 0)
                {
                    var breakdown = string.Join(", ", recipients.Select(r => $"{r.Warrior.Name} (+{r.DistributedExplorationExperience})"));
                    sentences.Add(string.Format(Loc["HistoryDistributedExperienceSentence"], breakdown));
                }
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None)
            {
                sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], ExplorationNoteText));
            }

            // Second objet du même branch, INDÉPENDANT du Kind ci-dessus (ex. Charrette Renversée, Kind
            // Item : Épée + Dague ornées ; Laboratoire de l'Alchimiste, Kind Gold : Or + Carnet de
            // l'Alchimiste) - toujours en un seul exemplaire, jamais soumis à un choix du joueur
            // (SecondaryEquipmentItemName est toujours un "ET", jamais un "OU" - contrairement à
            // EquipmentItemName/AlternativeEquipmentItemName ci-dessus). Le Carnet de l'Alchimiste n'a
            // besoin d'aucune autre logique ici : c'est un objet du catalogue comme un autre
            // (EquipmentItem.GrantsSkillCategory, voir Core.Rules.SkillEligibility) - une fois porté par
            // un guerrier, l'étape Progression existante (PickAdvanceSkill) en tient compte
            // automatiquement, rien à mémoriser côté Warrior/Warband à la sauvegarde de CE wizard.
            if (outcome.SecondaryEquipmentItemName is { } secondaryName)
                await AddOneItemToInventoryAsync(secondaryName, 1, outcome.MaterialRuleName);

            // Prisonniers, branche "autres bandes" - le prisonnier rejoint gratuitement le groupe
            // d'Hommes de main choisi par le joueur (+1 HeadCount, voir EndOfGamePageViewModel.
            // SelectedEquippedHenchmanGroupOption) ; seul le coût de l'équipement répliqué (déjà validé
            // affordable, voir CanAffordEquippedHenchman) est déduit de la trésorerie - jamais de Cost
            // d'archétype, contrairement à un recrutement normal. Indépendant du Kind ci-dessus (coexiste
            // avec l'or de l'escorte, Kind.Gold), même principe que SecondaryEquipmentItemName.
            if (outcome.GrantsOptionalEquippedHenchman && SelectedEquippedHenchmanGroupOption?.Group is { } recruitGroup)
            {
                recruitGroup.Warrior.HeadCount += 1;
                await _warbandService.SaveWarriorAsync(recruitGroup.Warrior);

                var equipmentCost = SelectedEquippedHenchmanGroupOption.EquipmentCost;
                if (equipmentCost > 0)
                {
                    Warband.Treasury -= equipmentCost;
                    await _warbandService.SaveWarbandAsync(Warband);
                }
                sentences.Add(string.Format(Loc["HistoryEquippedHenchmanSentence"], recruitGroup.ArchetypeName, equipmentCost));
            }

            // Sanctuaire, branche Sœurs de Sigmar/Chasseurs de Sorcières - attache la SpecialRule
            // "Blessed Weapon" sur l'arme choisie (déjà portée par un Héros, voir EndOfGamePageViewModel.
            // WeaponBlessingOptions), même mécanisme qu'un achat en Gromril/Ithilmar plutôt qu'un nouveau
            // champ - indépendant du Kind ci-dessus (coexiste avec l'or des reliques, Kind.Gold).
            if (outcome.GrantsWeaponBlessing && SelectedWeaponBlessingOption?.Equipment is { } blessedEquipment
                && englishSpecialRules.FirstOrDefault(r => r.Name == "Blessed Weapon") is { } blessedRule)
            {
                await _warbandService.SetWarriorEquipmentBlessingRuleAsync(blessedEquipment.Id, blessedRule.Id);
                sentences.Add(string.Format(Loc["HistoryWeaponBlessingSentence"],
                    SelectedWeaponBlessingOption.Hero!.Name, blessedEquipment.Item.Name));
            }

            // Entrée des Catacombes - relance permanente (voir Warband.HasCatacombReroll), acquise une
            // seule fois : une 2e entrée trouvée ne fait rien de plus (le bool est déjà vrai), aucune
            // phrase d'Historique redondante dans ce cas.
            if (outcome.GrantsCatacombReroll && !Warband.HasCatacombReroll)
            {
                Warband.HasCatacombReroll = true;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(Loc["HistoryCatacombRerollSentence"]);
            }

            // Rappel "prochaine partie" (Cimetière catch-all) - indépendant du Kind ci-dessus, même
            // principe que SecondaryEquipmentItemName/GrantsOptionalEquippedHenchman.
            if (outcome.NextGameNoteText is { } nextGameNote)
            {
                Warband.NextGameNote = nextGameNote;
                await _warbandService.SaveWarbandAsync(Warband);
            }

            // Une Faveur Rendue (3,6) - Franc-Tireur gratuit pour la prochaine bataille (voir
            // EndOfGamePageViewModel.Exploration.cs) - indépendant du Kind ci-dessus, même principe que
            // GrantsCatacombReroll/NextGameNoteText. Gratuit (HireCost jamais déduit) ; HiredSwordUpkeepPrepaid
            // couvre déjà sa toute prochaine solde (voir ApplyHiredSwordUpkeepAsync).
            if (outcome.GrantsFreeHiredSword && SelectedFreeHiredSword is { } freeHiredSword)
            {
                var localizedEquipment = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);
                var startingEquipment = localizedEquipment.Where(e => freeHiredSword.StartingEquipmentIds.Contains(e.Id)).ToList();
                // Pas de nom saisi par le joueur - garde le nom du Franc-Tireur (ex. "Gladiateur"), même
                // idiome que GrantsFreeHenchmanArchetypeName (Zombie).
                var name = freeHiredSword.Name;

                var recruited = await _warbandService.RecruitHiredSwordAsync(Warband.Id, freeHiredSword, name, startingEquipment);
                recruited.HiredSwordUpkeepPrepaid = true;
                await _warbandService.SaveWarriorAsync(recruited);
                sentences.Add(string.Format(Loc["HistoryHiredSwordFreeGrantSentence"], name));
            }
        }

        // Trésor Caché/Bande Massacrée - "roll for every item on the list separately" (voir
        // EndOfGamePageViewModel.IsIndependentThresholdResult) : plusieurs lignes peuvent avoir
        // franchi leur propre seuil à la fois, contrairement au Kind unique ci-dessus (jamais résolu
        // pour cette forme, les deux blocs sont mutuellement exclusifs en pratique).
        if (IsIndependentThresholdResult)
        {
            foreach (var entry in IndependentOutcomeEntries.Where(e => e.ShowResult))
            {
                if (entry.IsGold && int.TryParse(entry.AmountRoll, out var gold) && gold != 0)
                {
                    Warband.Treasury += gold;
                    await _warbandService.SaveWarbandAsync(Warband);
                    sentences.Add(string.Format(Loc["HistoryTreasurySentence"], gold));
                }
                else if (entry.IsWyrdstone && int.TryParse(entry.AmountRoll, out var shards) && shards != 0)
                {
                    Warband.WyrdstoneShards += shards;
                    await _warbandService.SaveWarbandAsync(Warband);
                    sentences.Add(string.Format(Loc["HistoryExplorationWyrdstoneSentence"], shards));
                }
                else if (entry.IsArtefact && entry.ResolvedArtefactItemName is { } artefactName)
                {
                    await AddOneItemToInventoryAsync(artefactName, 1, null);
                }
                else if (entry.IsItem && entry.Outcome.EquipmentItemName is { } itemName
                    && int.TryParse(entry.ItemQuantity, out var quantity))
                {
                    await AddOneItemToInventoryAsync(itemName, quantity, entry.Outcome.MaterialRuleName);
                }
            }
        }

        if (BonusItemOutcome is { } bonusOutcome && bonusOutcome.EquipmentItemName is { } bonusItemName)
            await AddOneItemToInventoryAsync(bonusItemName, 1, bonusOutcome.MaterialRuleName);

        // Test de Commandement additionnel du chef (ex. Bâtiment Éventré : Chien de guerre si réussi) -
        // voir ExplorationResult.BonusStatTestField/EndOfGamePageViewModel.BonusStatTestOutcome,
        // indépendant du Kind principal ci-dessus (coexiste avec les pierres magiques, ne les remplace
        // pas).
        if (BonusStatTestOutcome is { } bonusStatOutcome && bonusStatOutcome.EquipmentItemName is { } bonusStatItemName)
            await AddOneItemToInventoryAsync(bonusStatItemName, 1, bonusStatOutcome.MaterialRuleName);
    }
}
