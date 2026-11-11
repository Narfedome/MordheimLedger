using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
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
    /// plus un éventuel objet bonus sur ce même jet (Boutique - voir BonusItemOutcome). Le wizard décrit ici
    /// les récompenses (ExplorationRewards) et écrit l'Historique ; ExplorationRewardService (Core, testé)
    /// les applique en base. Les objets trouvés n'y figurent qu'en phrase d'Historique : ils entrent dans la
    /// réserve via PendingExplorationItems/ApplyReserveAsync.</summary>
    private async Task ApplyExplorationOutcomeAsync(List<string> sentences)
    {
        if (Warband is null) return;

        var gold = 0;
        var shards = 0;
        string? nextGameNote = null;
        var grantsBonusDie = false;
        var grantsCatacomb = false;
        (Warrior, int)? leaderExperience = null;
        var distributed = new List<(Warrior, int)>();
        (WarriorArchetype, int)? freeHenchmen = null;
        (Warrior, int)? equippedHenchman = null;
        (int, int)? weaponBlessing = null;
        (HiredSword, IReadOnlyList<EquipmentItem>)? freeHiredSword = null;

        // "Add the results together and consult the chart..." (Core.Rules.WyrdstoneShardsTable) - s'applique
        // TOUJOURS, en plus de l'éventuel résultat de doublons/triplets ci-dessous.
        if (BaselineWyrdstoneShardsFound > 0)
        {
            shards += BaselineWyrdstoneShardsFound;
            sentences.Add(string.Format(Loc["HistoryBaselineWyrdstoneSentence"], BaselineWyrdstoneShardsFound));
        }

        void AddOneItemToHistory(string itemOfficialId, int quantity)
        {
            if (quantity > 0 && _equipmentItemsByOfficialId.TryGetValue(itemOfficialId, out var item))
                sentences.Add(string.Format(Loc["HistoryExplorationItemSentence"], quantity, item.Name));
        }

        if (ResolvedExplorationOutcome is { } outcome)
        {
            if (outcome.Kind == ExplorationOutcomeKind.Gold
                && int.TryParse(ExplorationGoldAmount, out var foundGold) && foundGold != 0)
            {
                gold += foundGold;
                sentences.Add(string.Format(Loc["HistoryTreasurySentence"], foundGold));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.Item
                && ChosenExplorationItemName is { } primaryName
                && int.TryParse(ExplorationItemQuantity, out var quantity))
            {
                AddOneItemToHistory(primaryName, quantity);
            }
            else if (outcome.TriggersArtefactRoll && ResolvedArtefactItemName is { } artefactName)
            {
                AddOneItemToHistory(artefactName, 1);
            }
            else if (outcome.Kind == ExplorationOutcomeKind.Wyrdstone
                && int.TryParse(ExplorationWyrdstoneAmount, out var foundShards) && foundShards != 0)
            {
                shards += foundShards;
                sentences.Add(string.Format(Loc["HistoryExplorationWyrdstoneSentence"], foundShards));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.CausesSickness && StatTestSickHero is { } sickHero)
            {
                // Puits en échec : le statut Malade est posé plus tard par ApplySicknessLifecycleAsync (après
                // ApplyWarriorOutcomesAsync, qui écraserait sinon le statut).
                sentences.Add(string.Format(Loc["HistorySicknessSentence"], sickHero.Name));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.CausesDeath && PitDevouredHero is { } devouredHero)
            {
                // La Fosse, Héros dévoré : même principe, statut Mort posé plus tard.
                sentences.Add(string.Format(Loc["HistoryPitDevouredSentence"], devouredHero.Name));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsNextExplorationBonusDie)
            {
                grantsBonusDie = true;
                sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], ExplorationNoteText));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsLeaderExperience is { } leaderXp)
            {
                // Traînard, Possédés : bonus simplement perdu si le chef n'est pas disponible cette partie.
                if (SurvivingLeader is { } leader)
                {
                    leaderExperience = (leader.Warrior, leaderXp);
                    sentences.Add(string.Format(Loc["HistoryLeaderExperienceSentence"], leader.Warrior.Name, leaderXp));
                }
                else
                {
                    sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], ExplorationNoteText));
                }
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsFreeHenchmanArchetypeOfficialId is not null)
            {
                // Traînard/Prisonniers, Morts-Vivants (Zombies) : quantité fixe ou jet D3 (ExplorationItemQuantity).
                if (ResolvedFreeHenchman is { } archetype)
                {
                    var henchmanQuantity = int.TryParse(ExplorationItemQuantity, out var parsedQuantity) ? parsedQuantity : 1;
                    freeHenchmen = (archetype, henchmanQuantity);
                    sentences.Add(string.Format(Loc["HistoryFreeHenchmanSentence"], henchmanQuantity, archetype.Name));
                }
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsDistributedHeroExperienceFormula is not null)
            {
                // Prisonniers, Possédés : le total a déjà été réparti par le joueur entre ses Héros.
                var recipients = WarriorRows.Where(r => r.DistributedExplorationExperience > 0).ToList();
                distributed.AddRange(recipients.Select(r => (r.Warrior, r.DistributedExplorationExperience)));
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

            // Second objet du même résultat (« ET », jamais un choix) - indépendant du Kind ci-dessus.
            if (outcome.SecondaryEquipmentItemOfficialId is { } secondaryName)
                AddOneItemToHistory(secondaryName, 1);

            // Prisonniers, autres bandes : le prisonnier rejoint le groupe choisi, seul son équipement est payé.
            if (outcome.GrantsOptionalEquippedHenchman && SelectedEquippedHenchmanGroupOption?.Group is { } recruitGroup)
            {
                var equipmentCost = SelectedEquippedHenchmanGroupOption.EquipmentCost;
                equippedHenchman = (recruitGroup.Warrior, equipmentCost);
                sentences.Add(string.Format(Loc["HistoryEquippedHenchmanSentence"], recruitGroup.ArchetypeName, equipmentCost));
            }

            // Sanctuaire, Sœurs de Sigmar/Chasseurs de Sorcières : « Arme bénie » sur l'arme choisie.
            if (outcome.GrantsWeaponBlessing && SelectedWeaponBlessingOption?.Equipment is { } blessedEquipment
                && _specialRulesByOfficialId.GetValueOrDefault(OfficialIds.BlessedWeaponRule) is { } blessedRule)
            {
                weaponBlessing = (blessedEquipment.Id, blessedRule.Id);
                sentences.Add(string.Format(Loc["HistoryWeaponBlessingSentence"],
                    SelectedWeaponBlessingOption.Hero!.Name, blessedEquipment.Item.Name));
            }

            // Entrée des Catacombes : acquise une seule fois, pas de phrase redondante ensuite.
            if (outcome.GrantsCatacombReroll && !Warband.HasCatacombReroll)
            {
                grantsCatacomb = true;
                sentences.Add(Loc["HistoryCatacombRerollSentence"]);
            }

            nextGameNote = outcome.NextGameNoteText;

            // Une Faveur Rendue : Franc-Tireur offert, garde son nom (ex. « Gladiateur »), solde prépayée.
            if (outcome.GrantsFreeHiredSword && SelectedFreeHiredSword is { } hiredSword)
            {
                var localizedEquipment = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);
                freeHiredSword = (hiredSword, localizedEquipment.Where(e => hiredSword.StartingEquipmentIds.Contains(e.Id)).ToList());
                sentences.Add(string.Format(Loc["HistoryHiredSwordFreeGrantSentence"], hiredSword.Name));
            }
        }

        // Trésor Caché/Bande Massacrée : chaque ligne qui a franchi son propre seuil.
        if (IsIndependentThresholdResult)
        {
            foreach (var entry in IndependentOutcomeEntries.Where(e => e.ShowResult))
            {
                if (entry.IsGold && int.TryParse(entry.AmountRoll, out var lineGold) && lineGold != 0)
                {
                    gold += lineGold;
                    sentences.Add(string.Format(Loc["HistoryTreasurySentence"], lineGold));
                }
                else if (entry.IsWyrdstone && int.TryParse(entry.AmountRoll, out var lineShards) && lineShards != 0)
                {
                    shards += lineShards;
                    sentences.Add(string.Format(Loc["HistoryExplorationWyrdstoneSentence"], lineShards));
                }
                else if (entry.IsArtefact && entry.ResolvedArtefactItemName is { } artefactName)
                {
                    AddOneItemToHistory(artefactName, 1);
                }
                else if (entry.IsItem && entry.Outcome.EquipmentItemOfficialId is { } itemName
                    && int.TryParse(entry.ItemQuantity, out var quantity))
                {
                    AddOneItemToHistory(itemName, quantity);
                }
            }
        }

        if (BonusItemOutcome is { } bonusOutcome && bonusOutcome.EquipmentItemOfficialId is { } bonusItemName)
            AddOneItemToHistory(bonusItemName, 1);
        if (BonusStatTestOutcome is { } bonusStatOutcome && bonusStatOutcome.EquipmentItemOfficialId is { } bonusStatItemName)
            AddOneItemToHistory(bonusStatItemName, 1);

        await new ExplorationRewardService(_warbandService).ApplyAsync(Warband, WarriorRows.Select(r => r.Warrior), new ExplorationRewards
        {
            WyrdstoneShards = shards,
            Gold = gold,
            GrantsNextExplorationBonusDie = grantsBonusDie,
            NextGameNote = nextGameNote,
            GrantsCatacombReroll = grantsCatacomb,
            LeaderExperience = leaderExperience,
            DistributedExperience = distributed,
            FreeHenchmen = freeHenchmen,
            EquippedHenchman = equippedHenchman,
            WeaponBlessing = weaponBlessing,
            FreeHiredSword = freeHiredSword,
        });
    }
}
