using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands.EndOfGame;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Commande "Fin de partie" (Séquence d'après-bataille) - décomposée en phases nommées après
/// deux bugs réels d'ordonnancement trouvés le 2026-08-18 (bonus Boutique déclenché à tort par le jet
/// d'or de Cadavre ; statut Sick du Puits écrasé par la resynchronisation de statut de la boucle
/// principale). Extrait de WarbandDetailViewModel.cs (refactor de découpage, voir CLAUDE.md) : aucun
/// changement de comportement, pur déplacement de méthode + découpage en phases. L'ordre relatif
/// (Exploration -> Guerriers -> Statut Malade) est celui qui existe déjà après le correctif du bug de
/// statut - rendu explicite ici plutôt qu'implicite dans l'ordre du code.</summary>
public partial class WarbandDetailViewModel
{
    [RelayCommand]
    private async Task EndOfGame()
    {
        if (Warband is null) return;

        // Un guerrier Malade (voir WarriorStatus.Sick) manque LA bataille que ce wizard s'apprête à
        // enregistrer - le filtre Status == Active d'activeWarriorRows ci-dessous l'exclut déjà tout
        // seul (aucune étape Blessure/Expérience/Hors de combat/Exploration ne le concerne cette fois),
        // rien de plus à faire pour le "masquer". Capturé ICI, avant tout traitement, pour ne nettoyer
        // en fin de méthode QUE les guerriers déjà Malades en entrant - jamais un guerrier qui vient de
        // le devenir PENDANT cette même session (ex. Puits en échec) : celui-là doit rester Malade pour
        // la prochaine fin de partie, pas celle-ci (revenu sur ce point le 2026-08-18 : l'ancienne
        // version effaçait le statut avant même de construire activeWarriorRows, donc le guerrier
        // participait normalement à la fin de partie censée représenter la partie qu'il ratait).
        var previouslySickWarriors = AllActiveWarriorRows.Where(r => r.Warrior.Status == WarriorStatus.Sick).ToList();

        var activeWarriorRows = AllActiveWarriorRows
            .Where(r => r.Warrior.Status == WarriorStatus.Active)
            .ToList();
        if (activeWarriorRows.Count == 0)
        {
            await ShowInfoAsync(Loc["EndOfGameTitle"], Loc["EndOfGameNoWarriors"]);
            return;
        }

        var language = LocalizationService.Instance.Language;
        var explorationResults = await _libraryService.GetExplorationResultsAsync(language);

        // ExplorationOutcome.EquipmentItemName référence le catalogue par nom ANGLAIS brut (voir sa
        // doc) plutôt que par Id - construit une seule fois ici (Id introuvable autrement en anglais
        // uniquement) et transmis au wizard sous forme d'EquipmentItem entier (pas juste son nom) pour
        // qu'il affiche un vrai ChipView tapable (icône + popup détail) résolu dans la langue courante,
        // au lieu du nom anglais tel quel (ex. "Axe" affiché même en français avant ce correctif).
        var englishEquipment = await _libraryService.GetEquipmentItemsAsync("en");
        var localizedEquipment = language == "en" ? englishEquipment : await _libraryService.GetEquipmentItemsAsync(language);
        var equipmentItemsByEnglishName = englishEquipment.ToDictionary(e => e.Name,
            e => localizedEquipment.FirstOrDefault(l => l.Id == e.Id) ?? e);

        // Même besoin pour ExplorationOutcome.MaterialRuleName (ex. "Ornate Weapon") - permet au wizard
        // d'afficher "Épée (O)" plutôt que le nom nu, comme n'importe quel objet en Gromril/Ithilmar.
        var englishSpecialRules = await _libraryService.GetSpecialRulesAsync("en");
        var localizedSpecialRules = language == "en" ? englishSpecialRules : await _libraryService.GetSpecialRulesAsync(language);
        var specialRulesByEnglishName = englishSpecialRules.ToDictionary(r => r.Name,
            r => localizedSpecialRules.FirstOrDefault(l => l.Id == r.Id) ?? r);

        // Pour EquipmentItem.GrantsSpecificSkillName (ex. Haggle du symbole de la Maison du Marchand) -
        // seul l'Id compte pour le picker de compétence (son propre catalogue est déjà localisé), pas
        // besoin de résoudre un objet Skill localisé comme les deux dictionnaires ci-dessus.
        var skillIdsByEnglishName = (await _libraryService.GetSkillsAsync("en")).ToDictionary(s => s.Name, s => s.Id);

        // ExplorationOutcome.RestrictedToWarbandArchetypeNames (Groupe B "conditionné par la bande" -
        // Traînard, Prisonniers, Cimetière, bénédiction du Sanctuaire) matche par nom anglais, pas Id -
        // même besoin que les dictionnaires ci-dessus.
        var warbandArchetypeName = (await _libraryService.GetWarbandArchetypesAsync("en"))
            .First(a => a.Id == Warband.WarbandArchetypeId).Name;

        // Pour ExplorationOutcome.GrantsFreeHenchmanArchetypeName (ex. "Zombie", Traînard) - limité aux
        // archétypes de CETTE bande (jamais besoin d'un autre archétype pour ce genre de branche).
        var englishWarriorArchetypes = await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, "en");
        var localizedWarriorArchetypes = language == "en" ? englishWarriorArchetypes : await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, language);
        var warriorArchetypesByEnglishName = englishWarriorArchetypes.ToDictionary(a => a.Name,
            a => localizedWarriorArchetypes.FirstOrDefault(l => l.Id == a.Id) ?? a);

        // Pour l'aperçu en direct des SpecialRules attachées à une branche de Blessure Grave (ex.
        // Folie 24 -> Stupidité/Frénésie) - déjà pleinement résolu (SpecialRules incluses, voir
        // LibraryService.GetInjuriesAsync) donc réutilisable tel quel par WarriorOutcomeRow, via
        // InjuryCatalogLookup (même logique de correspondance par jet que GetOrCreateInjuryAsync plus
        // bas, partagée pour ne pas dupliquer le parseur de RollRange).
        var injuryCatalog = await _libraryService.GetInjuriesAsync(language);

        // Pour la comparaison de profil de "Vendu aux Fosses" (65) - le Gladiateur ("Pit Fighter",
        // catalogue Franc-Tireur/HiredSword) est un adversaire éphémère (jamais recruté dans la bande,
        // voir Models.Library.HiredSword), simple lookup par nom anglais comme les dictionnaires
        // ci-dessus, pas de résolution par Id nécessaire ici (un seul profil consommé, pas une
        // collection indexée par nom).
        var englishHiredSwords = await _libraryService.GetHiredSwordsAsync("en");
        // Catalogue complet localisé - à la fois pour le Gladiateur éphémère ci-dessous (Vendu aux Fosses)
        // ET, sans rapport avec lui, pour la nouvelle étape "Francs-Tireurs" (upkeep/recrutement réel dans
        // NOTRE bande - voir EndOfGameDialogViewModel.HiredSwords.cs).
        var localizedHiredSwords = language == "en" ? englishHiredSwords : await _libraryService.GetHiredSwordsAsync(language);
        var pitFighterEnglish = englishHiredSwords.FirstOrDefault(h => h.Name == "Pit Fighter");
        var pitFighterProfile = pitFighterEnglish is null
            ? null
            : localizedHiredSwords.FirstOrDefault(h => h.Id == pitFighterEnglish.Id) ?? pitFighterEnglish;

        // Équipement de départ du Gladiateur, déjà résolu en vrais EquipmentItem (localisés) pour la
        // même carte comparative - même idiome que localizedEquipment ci-dessus.
        var pitFighterEquipment = pitFighterProfile is null
            ? new List<EquipmentItem>()
            : localizedEquipment.Where(e => pitFighterProfile.StartingEquipmentIds.Contains(e.Id)).ToList();

        // Étape "Dramatis Personae" (upkeep, voir EndOfGameDialogViewModel.DramatisPersonae.cs) + choix
        // "payer avec l'objet alternatif" de l'étape Achat (RareItemSearchEntry.HasAlternativePaymentOption) -
        // même catalogue localisé que localizedHiredSwords ci-dessus, même raison (résout FeeKind/Upkeep/
        // AlternativePaymentItemId d'un personnage déjà recruté, jamais stockés sur Warrior lui-même).
        var dramatisPersonaCatalog = await _libraryService.GetDramatisPersonaeAsync(language);
        // Équipement de départ de chaque Dramatis Persona (Marquand & Ulli notamment, "Duel avec le
        // meneur" - retour utilisateur 2026-09-03 : "les armes ne sont pas affichées... dans le combat
        // contre la paire") - résolu ici en amont, même idiome que pitFighterEquipment ci-dessus :
        // DramatisPersona.StartingEquipmentIds reste une simple liste d'ids catalogue (voir sa doc), le
        // modèle n'a jamais porté sa propre résolution en vrais EquipmentItem. Même helper que
        // DetailDialogService.ShowDramatisPersonaDetailDialogAsync (EquipmentQuantityChip.GroupFrom, gère
        // les doublons - ex. Bertha "x2 Marteaux de Sigmarite" - contrairement au simple Where+Contains
        // utilisé pour le Gladiateur), résolu pour TOUT le catalogue plutôt que seulement la paire :
        // aussi peu coûteux, et réutilisable si un futur écran de ce wizard a besoin d'un autre Dramatis
        // Persona.
        var dramatisPersonaStartingEquipmentById = dramatisPersonaCatalog.ToDictionary(p => p.Id,
            p => p.StartingEquipmentIds.Count == 0 ? new List<EquipmentQuantityChip>() : EquipmentQuantityChip.GroupFrom(p.StartingEquipmentIds, localizedEquipment));
        var ownedEquipmentItemIds = Inventory.Select(w => w.Item.Id).ToHashSet();
        // Délai de re-recherche (2026-09-01, Aenur/Ulli & Marquand - voir DramatisPersona.
        // RequiresCooldownBeforeResearch) - exclut du picker "Personnage spécial" tout personnage encore
        // en cooldown pour CETTE bande.
        var cooldownDramatisPersonaIds = await _warbandService.GetDramatisPersonaCooldownIdsAsync(Warband.Id);

        var dialogViewModel = new EndOfGameDialogViewModel(activeWarriorRows, _skillPicker, _detailDialogs, _libraryService, _hiredSwordPicker, _equipmentPicker, _dramatisPersonaPicker, Warband.WarbandArchetypeId,
            warbandArchetypeName, Warband.PendingExplorationBonusDie, Warband.HasCatacombReroll, Warband.Treasury, Warband.WyrdstoneShards, explorationResults, equipmentItemsByEnglishName, specialRulesByEnglishName,
            warriorArchetypesByEnglishName, skillIdsByEnglishName, injuryCatalog, localizedHiredSwords, dramatisPersonaCatalog, dramatisPersonaStartingEquipmentById, ownedEquipmentItemIds, cooldownDramatisPersonaIds, Inventory.ToList(), pitFighterProfile, pitFighterEquipment);
        if (await ShowDialogAsync(new EndOfGameDialog(dialogViewModel)) != true) return;

        await Loading.RunAsync(async () =>
        {
            var sentences = new List<string> { string.Format(Loc["HistoryResultSentence"], dialogViewModel.SelectedResult) };

            await ApplyExplorationOutcomeAsync(dialogViewModel, englishEquipment, equipmentItemsByEnglishName, englishSpecialRules, sentences);
            await ApplyWarriorOutcomesAsync(dialogViewModel, language, sentences);
            await ApplyCapturedEnemiesAsync(dialogViewModel, warriorArchetypesByEnglishName, sentences);
            await ApplyWyrdstoneSaleAsync(dialogViewModel, sentences);
            ApplyAvailableVeterans(dialogViewModel, sentences);
            await ApplyRareItemSearchAsync(dialogViewModel, localizedEquipment, sentences);
            // Délai de re-recherche (Aenur/Ulli & Marquand) : reste d'abord toute trace de cooldown
            // PRÉEXISTANTE (posée à une Fin de Partie antérieure) - le picker de CETTE Fin de Partie les
            // a déjà exclus, donc atteindre ce point veut dire "une bataille a été jouée sans eux",
            // satisfaisant la condition. Doit rester AVANT ApplyWandererDeparturesAsync, qui peut poser un
            // NOUVEAU cooldown pour un départ de CETTE Fin de Partie - sinon ce nettoyage l'effacerait
            // aussitôt.
            await _warbandService.ClearAllDramatisPersonaCooldownsAsync(Warband.Id);
            await ApplyWandererDeparturesAsync(dialogViewModel, sentences);
            await ApplyDramatisPersonaUpkeepAsync(dialogViewModel, sentences);
            await ApplyPairEquipmentSeizureIfNeededAsync(dialogViewModel, sentences);
            await ApplyPairDuelIfNeededAsync(dialogViewModel, sentences);
            await ApplyHiredSwordUpkeepAsync(dialogViewModel, sentences);
            // Doit rester APRÈS ApplyWarriorOutcomesAsync : cette dernière resynchronise Warrior.Status
            // depuis l'étape Blessure (Actif/Mort) et écraserait Sick si elle passait avant (bug du
            // 2026-08-18) - invariant maintenant explicite ici plutôt qu'implicite dans l'ordre du code.
            await ApplySicknessLifecycleAsync(dialogViewModel, previouslySickWarriors);

            // La partie est terminée : redonne la main à "Lancer la partie" sur cette page (voir
            // Warband.GameInProgress) - sans effet si elle n'avait jamais été lancée (Fin de Partie
            // reste utilisable seule, aucune dépendance stricte à StartGame).
            Warband.GameInProgress = false;
            await _warbandService.SaveWarbandAsync(Warband);

            await _warbandService.AddHistoryEntryAsync(Warband.Id, string.Join(" ", sentences));
            await LoadAsync(Warband.Id);
        });
    }

    /// <summary>Étape Exploration : au plus une Outcome "principale" résolue par jet (ExplorationChart.
    /// DetectMultiples ne déclenche jamais plusieurs entrées de la table à la fois, voir Core.Rules),
    /// plus un éventuel objet bonus sur ce même jet (Boutique - voir BonusItemOutcome). L'or/objet/
    /// pierre magique trouvé de cette façon s'ajoute à la trésorerie/à l'inventaire exactement comme
    /// n'importe quel autre gain de la partie.</summary>
    private async Task ApplyExplorationOutcomeAsync(EndOfGameDialogViewModel dialogViewModel, List<EquipmentItem> englishEquipment,
        Dictionary<string, EquipmentItem> equipmentItemsByEnglishName, List<SpecialRule> englishSpecialRules, List<string> sentences)
    {
        if (Warband is null) return;

        // Le dé bonus en attente (Traînard, voir Warband.PendingExplorationBonusDie) a été montré comme
        // rappel textuel à cette étape (dialogViewModel.ShowPendingExplorationBonusDieReminder) - une
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
        // spécial déclenché. dialogViewModel.BaselineWyrdstoneShardsFound vaut 0 si aucun Héros n'a
        // survécu (aucun dé lancé), donc rien à faire dans ce cas.
        if (dialogViewModel.BaselineWyrdstoneShardsFound > 0)
        {
            Warband.WyrdstoneShards += dialogViewModel.BaselineWyrdstoneShardsFound;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryBaselineWyrdstoneSentence"], dialogViewModel.BaselineWyrdstoneShardsFound));
        }

        // Même résolution nom-anglais-vers-Id que le chargement de la page, réutilisée ici pour
        // AddWarbandEquipmentAsync (seul l'Id compte, voir WarbandService) et pour la phrase
        // d'Historique (equipmentItemsByEnglishName donne directement l'item résolu dans la langue
        // courante). Partagée entre la branche Objet "normale" (ResolvedExplorationOutcome) et l'objet
        // bonus sur le même dé que l'or (BonusItemOutcome, ex. Boutique - voir EndOfGameDialogViewModel).
        async Task AddOneItemToInventoryAsync(string itemName, int quantity, string? materialRuleName, int? foundValueOverride = null)
        {
            if (quantity <= 0) return;

            var englishItem = englishEquipment.FirstOrDefault(e => e.Name == itemName);
            if (englishItem is null) return;

            // Même mécanisme que pour les objets achetés normalement (voir WarriorEquipment.
            // MaterialRule) : "Hache de Gromril" est une Hache de base + la SpecialRule "Gromril Weapon",
            // pas un objet distinct du catalogue - "Épée Ornée" (Charrette Renversée) suit le même
            // principe, et sa vendabilité vient uniquement de SpecialRule.IsResaleUpgrade sur ce
            // matériau (voir WarbandEquipment.IsSellable), pas d'un champ à part sur l'Outcome.
            var materialRule = materialRuleName is { } name
                ? englishSpecialRules.FirstOrDefault(r => r.Name == name) : null;

            await _warbandService.AddWarbandEquipmentAsync(Warband.Id, englishItem, quantity, materialRule, foundValueOverride);
            var displayName = equipmentItemsByEnglishName.GetValueOrDefault(itemName)?.Name ?? itemName;
            sentences.Add(string.Format(Loc["HistoryExplorationItemSentence"], quantity, displayName));
        }

        if (dialogViewModel.ResolvedExplorationOutcome is { } outcome)
        {
            if (outcome.Kind == ExplorationOutcomeKind.Gold
                && int.TryParse(dialogViewModel.ExplorationGoldAmount, out var gold) && gold != 0)
            {
                Warband.Treasury += gold;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryTreasurySentence"], gold));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.Item
                && dialogViewModel.ChosenExplorationItemName is { } primaryName
                && int.TryParse(dialogViewModel.ExplorationItemQuantity, out var quantity))
            {
                // ChosenExplorationItemName plutôt que outcome.EquipmentItemName brut : tient compte d'un
                // éventuel choix du joueur entre deux objets (ex. Armurerie 1-2 : Bouclier OU Rondache,
                // voir ExplorationOutcome.AlternativeEquipmentItemName). foundValueOverride : seulement
                // pour une branche dont la valeur trouvée n'est pas le Cost fixe du catalogue (ex.
                // Bijoutier - Pierres de Quartz/Rubis, voir ExplorationOutcome.FoundValueFormula/
                // WarbandEquipment.FoundValueOverride) - null pour toute autre branche Item.
                int? foundValueOverride = outcome.FoundValueFormula is not null
                    && int.TryParse(dialogViewModel.ExplorationItemFoundValue, out var foundValue) ? foundValue : null;
                await AddOneItemToInventoryAsync(primaryName, quantity, outcome.MaterialRuleName, foundValueOverride);
            }
            else if (outcome.TriggersArtefactRoll && dialogViewModel.ResolvedArtefactItemName is { } artefactName)
            {
                // Villa d'un Noble, sous-jet 5-6 : l'objet précis vient du second D6 sur la table des
                // Artefacts Magiques (voir Core.Rules.MagicalArtefactTable), jamais de
                // ChosenExplorationItemName - c'est pourquoi ce cas ne tombe pas dans le "else if"
                // Kind.Item ci-dessus (EquipmentItemName reste null sur cette branche).
                await AddOneItemToInventoryAsync(artefactName, 1, null);
            }
            else if (outcome.Kind == ExplorationOutcomeKind.Wyrdstone
                && int.TryParse(dialogViewModel.ExplorationWyrdstoneAmount, out var shards) && shards != 0)
            {
                Warband.WyrdstoneShards += shards;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryExplorationWyrdstoneSentence"], shards));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.CausesSickness && dialogViewModel.StatTestSickHero is { } sickHero)
            {
                // Puits en échec (test d'Endurance) - voir WarriorStatus.Sick. Le statut lui-même n'est
                // PAS posé ici : ApplySicknessLifecycleAsync le pose plus tard, APRÈS
                // ApplyWarriorOutcomesAsync qui resynchronise sinon warrior.Status depuis row.Status
                // (Actif/Mort uniquement) et écraserait silencieusement Sick en Actif (bug trouvé le
                // 2026-08-18 : le statut Malade ne "prenait" jamais).
                sentences.Add(string.Format(Loc["HistorySicknessSentence"], sickHero.Name));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.CausesDeath && dialogViewModel.PitDevouredHero is { } devouredHero)
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
                sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], dialogViewModel.ExplorationNoteText));
            }
            else if (outcome.Kind == ExplorationOutcomeKind.None && outcome.GrantsLeaderExperience is { } leaderXp)
            {
                // Traînard, branche Possédés - même idiome que BonusStatTestLeader (Bâtiment Éventré) :
                // pas d'erreur bloquante si le chef n'est pas disponible cette partie (mort/malade/hors
                // de combat), le bonus est simplement indisponible.
                var leader = AllActiveWarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);
                if (leader is not null)
                {
                    leader.Warrior.Experience += leaderXp;
                    await _warbandService.SaveWarriorAsync(leader.Warrior);
                    sentences.Add(string.Format(Loc["HistoryLeaderExperienceSentence"], leader.Warrior.Name, leaderXp));
                }
                else
                {
                    sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], dialogViewModel.ExplorationNoteText));
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
                    var henchmanQuantity = int.TryParse(dialogViewModel.ExplorationItemQuantity, out var parsedQuantity) ? parsedQuantity : 1;
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
                // steppeur +/- de chaque Héros (dialogViewModel.DistributedExperienceRemaining vérifié à
                // 0 par ValidateExplorationResultStep) ; ici on ne fait qu'appliquer chaque allocation.
                var recipients = dialogViewModel.WarriorRows.Where(r => r.DistributedExplorationExperience > 0).ToList();
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
                sentences.Add(string.Format(Loc["HistoryExplorationNoteSentence"], dialogViewModel.ExplorationNoteText));
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
            // d'Hommes de main choisi par le joueur (+1 HeadCount, voir EndOfGameDialogViewModel.
            // SelectedEquippedHenchmanGroupOption) ; seul le coût de l'équipement répliqué (déjà validé
            // affordable, voir CanAffordEquippedHenchman) est déduit de la trésorerie - jamais de Cost
            // d'archétype, contrairement à un recrutement normal. Indépendant du Kind ci-dessus (coexiste
            // avec l'or de l'escorte, Kind.Gold), même principe que SecondaryEquipmentItemName.
            if (outcome.GrantsOptionalEquippedHenchman && dialogViewModel.SelectedEquippedHenchmanGroupOption?.Group is { } recruitGroup)
            {
                recruitGroup.Warrior.HeadCount += 1;
                await _warbandService.SaveWarriorAsync(recruitGroup.Warrior);

                var equipmentCost = dialogViewModel.SelectedEquippedHenchmanGroupOption.EquipmentCost;
                if (equipmentCost > 0)
                {
                    Warband.Treasury -= equipmentCost;
                    await _warbandService.SaveWarbandAsync(Warband);
                }
                sentences.Add(string.Format(Loc["HistoryEquippedHenchmanSentence"], recruitGroup.ArchetypeName, equipmentCost));
            }

            // Sanctuaire, branche Sœurs de Sigmar/Chasseurs de Sorcières - attache la SpecialRule
            // "Blessed Weapon" sur l'arme choisie (déjà portée par un Héros, voir EndOfGameDialogViewModel.
            // WeaponBlessingOptions), même mécanisme qu'un achat en Gromril/Ithilmar plutôt qu'un nouveau
            // champ - indépendant du Kind ci-dessus (coexiste avec l'or des reliques, Kind.Gold).
            if (outcome.GrantsWeaponBlessing && dialogViewModel.SelectedWeaponBlessingOption?.Equipment is { } blessedEquipment
                && englishSpecialRules.FirstOrDefault(r => r.Name == "Blessed Weapon") is { } blessedRule)
            {
                await _warbandService.SetWarriorEquipmentBlessingRuleAsync(blessedEquipment.Id, blessedRule.Id);
                sentences.Add(string.Format(Loc["HistoryWeaponBlessingSentence"],
                    dialogViewModel.SelectedWeaponBlessingOption.Hero!.Name, blessedEquipment.Item.Name));
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
            // EndOfGameDialogViewModel.Exploration.cs) - indépendant du Kind ci-dessus, même principe que
            // GrantsCatacombReroll/NextGameNoteText. Gratuit (HireCost jamais déduit) ; HiredSwordUpkeepPrepaid
            // couvre déjà sa toute prochaine solde (voir ApplyHiredSwordUpkeepAsync).
            if (outcome.GrantsFreeHiredSword && dialogViewModel.SelectedFreeHiredSword is { } freeHiredSword)
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
        // EndOfGameDialogViewModel.IsIndependentThresholdResult) : plusieurs lignes peuvent avoir
        // franchi leur propre seuil à la fois, contrairement au Kind unique ci-dessus (jamais résolu
        // pour cette forme, les deux blocs sont mutuellement exclusifs en pratique).
        if (dialogViewModel.IsIndependentThresholdResult)
        {
            foreach (var entry in dialogViewModel.IndependentOutcomeEntries.Where(e => e.ShowResult))
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

        if (dialogViewModel.BonusItemOutcome is { } bonusOutcome && bonusOutcome.EquipmentItemName is { } bonusItemName)
            await AddOneItemToInventoryAsync(bonusItemName, 1, bonusOutcome.MaterialRuleName);

        // Test de Commandement additionnel du chef (ex. Bâtiment Éventré : Chien de guerre si réussi) -
        // voir ExplorationResult.BonusStatTestField/EndOfGameDialogViewModel.BonusStatTestOutcome,
        // indépendant du Kind principal ci-dessus (coexiste avec les pierres magiques, ne les remplace
        // pas).
        if (dialogViewModel.BonusStatTestOutcome is { } bonusStatOutcome && bonusStatOutcome.EquipmentItemName is { } bonusStatItemName)
            await AddOneItemToInventoryAsync(bonusStatItemName, 1, bonusStatOutcome.MaterialRuleName);
    }

    private async Task ApplyWarriorOutcomesAsync(EndOfGameDialogViewModel dialogViewModel, string language, List<string> sentences)
    {
        if (Warband is null) return;

        List<Injury>? injuryCatalog = null;

        // Find-or-create par jet (roll) contre le catalogue Injury seedé depuis Injuries.json (RollRange
        // par entrée, ex. "22", "16, 21", "11-15" - voir InjuryCatalogLookup, partagé avec
        // WarriorOutcomeRow pour l'aperçu en direct dans le wizard) plutôt que par égalité de
        // texte : corrige un bug où la chip de blessure affichait la phrase descriptive complète
        // (row.InjuryResultText, résolue via la clé resx InjurySeriousXX, ex. "Blessure à la jambe :
        // Mouvement -1 de façon permanente.") au lieu du nom court du catalogue ("Blessure à la jambe") -
        // l'ancienne comparaison par nom ne matchait jamais contre le catalogue (Name y est déjà le nom
        // court), donc créait systématiquement un doublon avec la phrase entière comme Name. fallbackText
        // ne sert plus que si le jet ne matche aucune entrée du catalogue officiel (roll invalide).
        //
        // branchSubRoll : pour Blessure au bras/Jambe écrasée (23/25), le catalogue a 2 entrées par roll
        // (légère "2-6"/grave "1", voir Injury.BranchRange) - non-null sélectionne la bonne, null retombe
        // sur l'entrée générique (BranchRange vide) si elle existe, sinon une entrée arbitraire. Un
        // sous-jet "Blessures multiples" tombant sur 23/25 a bien son propre sous-jet de branche depuis
        // le 2026-09-04 (retour utilisateur - voir ApplyInjuryRollCoreAsync plus bas), donc branchSubRoll
        // n'est plus réservé au seul jet principal.
        async Task<Injury> GetOrCreateInjuryAsync(int roll, bool isHero, string fallbackText, int? branchSubRoll = null)
        {
            injuryCatalog ??= await _libraryService.GetInjuriesAsync(language);
            var category = isHero ? InjuryCategory.Hero : InjuryCategory.Henchman;
            var injury = InjuryCatalogLookup.Find(injuryCatalog, category, roll, branchSubRoll);
            if (injury is null)
            {
                injury = new Injury { Name = fallbackText, Category = category, Source = ContentSource.Official };
                await _libraryService.SaveInjuryAsync(injury, language);
                injuryCatalog.Add(injury);
            }
            return injury;
        }

        foreach (var row in dialogViewModel.WarriorRows)
        {
            var warrior = row.Warrior;
            var changed = false;

            // Blinded in One Eye (31) : le second œil force la retraite plutôt qu'un nouveau -1 Tir -
            // voir SeriousInjuryEffectTable.TryGetOutcome. Recalculé/mis à jour au fil des jets de CE
            // guerrier ci-dessous (jet principal, puis chaque sous-jet "Blessures multiples") plutôt que
            // recalculé une seule fois, pour couvrir le cas (rare) où les deux occurrences tombent dans
            // la même Fin de Partie.
            var alreadyBlindedInOneEye = warrior.Injuries.Any(i => InjuryCatalogLookup.RollRangeMatches(i.Item.RollRange, 31));

            // 2026-09-04, retour utilisateur ("les rolls des blessures doivent être les mêmes qu'une
            // blessure classique... on peut tirer 2 fois Folie qui nous fait roll la folie une fois
            // chacun") : résout + applique UN jet D66 complet (catalogue Injury + effet Palier 1 +
            // branche 23/25/24 + Rancune 56) - partagé par chaque sous-jet "Blessures multiples" de CE
            // guerrier (foreach (var sub in row.MultipleInjuryRolls) plus bas) et par la relance d'un
            // combat de gladiateur perdu (voir ApplyPitFightEntryAsync juste après), pour que les deux se
            // comportent EXACTEMENT comme le jet principal ci-dessus plutôt que la version tronquée
            // (Palier 1 seul, sans branche/Rancune) qui existait avant cette passe. Ne décide PAS de
            // Mort/Capturé/Vendu aux Fosses : chaque appelant garde sa propre branche pour ça, la
            // conséquence (HeadCount/Status/reroll) diffère légèrement selon le contexte (voir plus bas).
            async Task<bool> ApplyInjuryRollCoreAsync(InjurySubRollEntry entry)
            {
                if (string.IsNullOrWhiteSpace(entry.InjuryResultText)) return false;

                var entryChanged = false;
                var hasRoll = int.TryParse(entry.ManualRoll, out var roll);
                int? entryBranchSubRoll = entry.ShowInjuryBranchSubRoll && int.TryParse(entry.InjuryBranchSubRoll, out var branchRoll) ? branchRoll : null;

                var entryOutcome = entry.ShowInjuryBranchSubRoll
                    ? entry.InjuryBranchOutcome
                    : hasRoll && SeriousInjuryEffectTable.TryGetOutcome(roll, alreadyBlindedInOneEye, out var o) ? o : null;
                if (entryOutcome?.Kind == SeriousInjuryEffectKind.MissGamesRollD3)
                    entryOutcome = entryOutcome with { Value = int.TryParse(entry.DeepWoundSubRoll, out var d3) ? d3 : SeriousInjuryEffectTable.RollD3() };

                var entryIsTemporary = entryOutcome?.Kind is SeriousInjuryEffectKind.MissNextGame or SeriousInjuryEffectKind.MissGamesRollD3;
                var entryInjury = await GetOrCreateInjuryAsync(hasRoll ? roll : -1, warrior.IsHero, entry.ResolvedInjuryText, entryBranchSubRoll);
                await _warbandService.AddWarriorInjuryAsync(warrior.Id, entryInjury, entryIsTemporary);
                sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, entry.ResolvedInjuryText));

                if (entryOutcome is not null)
                {
                    entryChanged |= await ApplySeriousInjuryEffectAsync(warrior, entryOutcome);
                    if (entryOutcome.Kind == SeriousInjuryEffectKind.ForcedRetirement)
                        sentences.Add(string.Format(Loc["HistoryForcedRetirementSentence"], warrior.Name));
                }
                if (hasRoll && roll == 31) alreadyBlindedInOneEye = true;

                if (entry.HasHatredTarget)
                {
                    await _warbandService.AddWarriorHatredAsync(warrior.Id, entry.HatredTargetWarbandArchetypeId, entry.HatredTargetFreeText);
                    var hatredLabel = string.Format(Loc["WarriorsHatredChipFormat"], entry.HatredTargetDisplayName);
                    sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, hatredLabel));
                }

                return entryChanged;
            }

            // Vendu aux Fosses (65) pour UNE occurrence - un sous-jet "Blessures multiples" qui tombe sur
            // 65 (jamais le jet principal, qui garde son propre bloc plus bas, inchangé par cette passe).
            // Même issue Victoire/Défaite que le jet principal (voir ce bloc pour la doc détaillée) :
            // victoire = or/XP, équipement intact ; défaite = relance (entry.SoldToPitsRerollRoll),
            // équipement perdu sans condition si le guerrier survit à cette relance.
            async Task<bool> ApplyPitFightEntryAsync(InjurySubRollEntry entry)
            {
                if (entry.WonPitFight)
                {
                    Warband.Treasury += 50;
                    warrior.Experience += 2;
                    sentences.Add(string.Format(Loc["HistorySoldToPitsWonSentence"], warrior.Name));
                    return true;
                }

                if (entry.SoldToPitsRerollRoll.FirstOrDefault() is not { } reroll || string.IsNullOrWhiteSpace(reroll.InjuryResultText))
                    return false;

                await ApplyInjuryRollCoreAsync(reroll);

                if (reroll.IsDeath)
                {
                    warrior.Status = WarriorStatus.Dead;
                    sentences.Add(string.Format(Loc["HistoryDeathSentence"], warrior.Name));
                }
                else if (reroll.ShowCapturedChoice && reroll.IsRansomed && int.TryParse(reroll.RansomAmount, out var rerollRansom))
                {
                    Warband.Treasury -= rerollRansom;
                    sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, rerollRansom));
                }
                else if (reroll.ShowCapturedChoice)
                {
                    warrior.Status = WarriorStatus.Dead;
                    foreach (var equipment in warrior.Equipment.ToList())
                        await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                    warrior.Equipment.Clear();
                    sentences.Add(string.Format(Loc["HistoryCapturedLostSentence"], warrior.Name));
                }
                else
                {
                    foreach (var equipment in warrior.Equipment.ToList())
                        await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                    warrior.Equipment.Clear();
                    sentences.Add(string.Format(Loc["HistorySoldToPitsLostSentence"], warrior.Name));
                }

                return true;
            }

            if (row.ExperienceGained != 0)
            {
                warrior.Experience += row.ExperienceGained;
                sentences.Add(string.Format(Loc["HistoryXpSentence"], warrior.Name, row.ExperienceGained));
                changed = true;
            }

            // Applique un résultat Compétence/Sort/Caractéristique déjà résolu à target - factorisé en
            // méthode locale (2026-08-24) car réutilisé pour 3 cibles distinctes : le guerrier lui-même
            // (AdvanceRolls/ExplorationAdvanceRolls ci-dessous), le nouveau Héros d'une promotion
            // (AdvanceRollEntry.NestedHeroRoll) et le reste du groupe source (NestedHenchmanRoll) - voir
            // le bloc Promotion plus bas. N'appelle jamais SaveWarriorAsync elle-même : c'est à
            // l'appelant de sauvegarder target une fois tous ses résultats appliqués (le guerrier
            // principal via `changed`/le flux existant plus bas, le nouveau Héros/le reste du groupe
            // explicitement dans le bloc Promotion).
            async Task ApplyResolvedAdvanceAsync(Warrior target, AdvanceRollEntry advance)
            {
                var text = advance.SelectedSkills.Count > 0 ? string.Format(Loc["EndOfGameAdvanceSkillResultText"], advance.SelectedSkillsText)
                    : advance.HasSpellSelected ? string.Format(Loc["EndOfGameAdvanceSpellResultText"], advance.SelectedSpell!.Name)
                    : advance.ResolvedField is not null ? $"{advance.ResolvedFieldLabel} +1"
                    : advance.ResultText;
                sentences.Add(string.Format(Loc["HistoryAdvanceSentence"], target.Name, text));

                foreach (var skill in advance.SelectedSkills)
                    await _warbandService.AddWarriorSkillAsync(target.Id, skill);

                if (advance.HasSpellSelected)
                    await _warbandService.AddWarriorSpellAsync(target.Id, advance.SelectedSpell!);

                if (advance.ResolvedField is { } field)
                    ApplyCharacteristicIncrease(target, field);
            }

            // AdvanceRolls (palier franchi par l'XP de bataille normale) + ExplorationAdvanceRolls
            // (palier atteint uniquement grâce à l'XP accordée par la table d'Exploration - voir
            // WarriorOutcomeRow.ExplorationMilestoneCount) : même application pour les deux, aucune
            // distinction nécessaire une fois les jets faits.
            foreach (var advance in row.AdvanceRolls.Concat(row.ExplorationAdvanceRolls))
            {
                if (string.IsNullOrWhiteSpace(advance.ResultText)) continue;

                // Mécanisé (2026-08-24) : un résultat "Compétence" attache une vraie Compétence OU (Héros
                // sorcier) un vrai Sort ; un résultat "Caractéristique" applique un vrai +1 sur le
                // Warrior, dans le respect du maximum racial et (Homme de main) de "jamais deux fois" -
                // voir Core.Rules.CharacteristicIncreaseRules/AdvanceRollEntry.ResolvedField. Une
                // promotion (10-12, "Ce gars est doué") crée un vrai nouveau Héros - voir
                // EntityMapping.CloneAsPromotedHero - et applique son jet de Progression immédiat
                // (NestedHeroRoll) plus, si le groupe comptait plus d'un membre, celui du reste du
                // groupe (NestedHenchmanRoll).
                if (advance.IsPromotionResult && advance.PromotedWarriorPreview is { } promoted && advance.NestedHeroRoll is { } heroRoll)
                {
                    await _warbandService.InsertWarriorAsync(promoted);
                    sentences.Add(string.Format(Loc["HistoryPromotionSentence"], warrior.Name, promoted.Name));

                    await ApplyResolvedAdvanceAsync(promoted, heroRoll);
                    await _warbandService.SaveWarriorAsync(promoted);

                    warrior.HeadCount -= 1;
                    changed = true;

                    if (advance.NestedHenchmanRoll is { } remainderRoll)
                        await ApplyResolvedAdvanceAsync(warrior, remainderRoll);

                    continue;
                }

                await ApplyResolvedAdvanceAsync(warrior, advance);
                if (advance.ResolvedField is not null) changed = true;
            }

            if (row.Status != warrior.Status)
            {
                warrior.Status = row.Status;
                changed = true;
                if (warrior.Status == WarriorStatus.Dead)
                    sentences.Add(string.Format(Loc["HistoryDeathSentence"], warrior.Name));
            }

            if (!string.IsNullOrWhiteSpace(row.InjuryResultText))
            {
                var hasMainRoll = int.TryParse(row.ManualRoll, out var mainRoll);
                int? branchSubRoll = row.ShowInjuryBranchSubRoll && int.TryParse(row.InjuryBranchSubRoll, out var branchRoll) ? branchRoll : null;

                // Palier 1 (voir Core.Rules.SeriousInjuryEffectTable) : mutation réelle correspondant au
                // résultat déjà résolu en texte ci-dessus - TryGetOutcome/TryGetBranchSubRollOutcome
                // renvoient false pour tout résultat hors Palier 1 (Guérison Totale, Capturé, branche
                // grave de 23/25...), qui reste texte de référence pur comme avant cette passe.
                SeriousInjuryOutcome? outcome = row.ShowInjuryBranchSubRoll
                    ? row.InjuryBranchOutcome
                    : hasMainRoll && SeriousInjuryEffectTable.TryGetOutcome(mainRoll, alreadyBlindedInOneEye, out var mainOutcome)
                        ? mainOutcome
                        : null;

                // Blessure profonde (35) : le nombre de parties manquées est un 1D3 saisi par le joueur
                // dans le wizard (row.DeepWoundSubRoll, voir WarriorOutcomeRow) plutôt que tiré à
                // l'aveugle ici - même principe que row.InjuryBranchOutcome ci-dessus, juste pour la
                // valeur plutôt que pour le choix de la branche. Repli sur un jet auto si jamais absent
                // (ne devrait jamais arriver, ValidateInjuryStep l'exige avant de continuer).
                if (outcome?.Kind == SeriousInjuryEffectKind.MissGamesRollD3)
                    outcome = outcome with { Value = int.TryParse(row.DeepWoundSubRoll, out var d3) ? d3 : SeriousInjuryEffectTable.RollD3() };

                // Puce temporaire (voir Models.WarriorInjury.IsTemporary) uniquement pour les effets qui
                // se résorbent d'eux-mêmes une fois la Maladie levée - la branche grave permanente de
                // 23/25 (aucun outcome ici) et tout le reste du Palier 1 restent des puces permanentes.
                var isTemporary = outcome?.Kind is SeriousInjuryEffectKind.MissNextGame or SeriousInjuryEffectKind.MissGamesRollD3;

                var injury = await GetOrCreateInjuryAsync(hasMainRoll ? mainRoll : -1, warrior.IsHero, row.ResolvedInjuryText, branchSubRoll);
                await _warbandService.AddWarriorInjuryAsync(warrior.Id, injury, isTemporary);
                sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, row.ResolvedInjuryText));

                if (outcome is not null)
                {
                    changed |= await ApplySeriousInjuryEffectAsync(warrior, outcome);
                    if (outcome.Kind == SeriousInjuryEffectKind.ForcedRetirement)
                        sentences.Add(string.Format(Loc["HistoryForcedRetirementSentence"], warrior.Name));
                }
                if (hasMainRoll && mainRoll == 31) alreadyBlindedInOneEye = true;
            }

            // Rancune (56) : la cible choisie par le joueur (EndOfGameDialogViewModel.Injury) devient une
            // WarriorHatred - voir Models.WarriorHatred pour pourquoi ce n'est pas une Injury de plus (2
            // sortes de cible possibles, pas un simple texte catalogue).
            if (row.HasHatredTarget)
            {
                await _warbandService.AddWarriorHatredAsync(warrior.Id, row.HatredTargetWarbandArchetypeId, row.HatredTargetFreeText);
                var hatredLabel = string.Format(Loc["WarriorsHatredChipFormat"], row.HatredTargetDisplayName);
                sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, hatredLabel));
            }

            // Capturé (61) : portée revue à la baisse (2026-08-27, voir SERIOUS_INJURIES_STATUS.md) -
            // le livre décrit 5 issues nommées mais toutes racontent une décision du CAPTEUR (une bande
            // adverse que l'appli ne modélise pas comme donnée structurée - voir la note mémoire sur un
            // futur système en réseau). Seule la rançon change réellement quelque chose de notre point
            // de vue (coût négocié, déduit de notre trésorerie, le guerrier revient avec son
            // équipement) - toute autre issue (échangé/vendu/tué/sacrifié) revient au même pour nous :
            // perdu pour de bon, considéré mort, équipement perdu comme Dépouillé (36).
            if (row.ShowCapturedChoice)
            {
                if (row.IsRansomed && int.TryParse(row.RansomAmount, out var ransomAmount))
                {
                    Warband.Treasury -= ransomAmount;
                    sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, ransomAmount));
                }
                else
                {
                    warrior.Status = WarriorStatus.Dead;
                    foreach (var equipment in warrior.Equipment.ToList())
                        await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                    warrior.Equipment.Clear();
                    sentences.Add(string.Format(Loc["HistoryCapturedLostSentence"], warrior.Name));
                }
                changed = true;
            }

            // Vendu aux Fosses (65) : combat de gladiateur contre un Gladiateur ("Pit Fighter",
            // catalogue Franc-Tireur/HiredSword) - adversaire éphémère, jamais recruté dans notre bande
            // quelle que soit l'issue.
            // Victoire : gain d'or/XP, équipement intact. Défaite : un unique sous-jet D66 sur la même
            // table (row.SoldToPitsRerollRoll, réutilise le même traitement Palier 1 que les sous-jets
            // "Blessures multiples" ci-dessous) puis, si le guerrier survit à cette relance, équipement
            // perdu SANS CONDITION ("no armor or weapons", même traitement que Dépouillé/Capturé perdu).
            if (row.ShowSoldToThePits)
            {
                if (row.WonPitFight)
                {
                    Warband.Treasury += 50;
                    warrior.Experience += 2;
                    sentences.Add(string.Format(Loc["HistorySoldToPitsWonSentence"], warrior.Name));
                    changed = true;
                }
                else if (row.SoldToPitsRerollRoll.FirstOrDefault() is { } reroll && !string.IsNullOrWhiteSpace(reroll.InjuryResultText))
                {
                    var hasRerollRoll = int.TryParse(reroll.ManualRoll, out var rerollRoll);
                    var rerollOutcome = hasRerollRoll && SeriousInjuryEffectTable.TryGetOutcome(rerollRoll, alreadyBlindedInOneEye, out var rerollO) ? rerollO : null;
                    if (rerollOutcome?.Kind == SeriousInjuryEffectKind.MissGamesRollD3)
                        rerollOutcome = rerollOutcome with { Value = int.TryParse(reroll.DeepWoundSubRoll, out var rerollD3) ? rerollD3 : SeriousInjuryEffectTable.RollD3() };

                    var rerollInjury = await GetOrCreateInjuryAsync(hasRerollRoll ? rerollRoll : -1, warrior.IsHero, reroll.InjuryResultText);
                    await _warbandService.AddWarriorInjuryAsync(warrior.Id, rerollInjury,
                        rerollOutcome?.Kind is SeriousInjuryEffectKind.MissNextGame or SeriousInjuryEffectKind.MissGamesRollD3);
                    sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, reroll.InjuryResultText));

                    if (rerollOutcome is not null)
                        changed |= await ApplySeriousInjuryEffectAsync(warrior, rerollOutcome);
                    if (hasRerollRoll && rerollRoll == 31) alreadyBlindedInOneEye = true;

                    if (reroll.IsDeath)
                    {
                        warrior.Status = WarriorStatus.Dead;
                        sentences.Add(string.Format(Loc["HistoryDeathSentence"], warrior.Name));
                    }
                    else if (reroll.ShowCapturedChoice && reroll.IsRansomed && int.TryParse(reroll.RansomAmount, out var rerollRansom))
                    {
                        // Sous-jet retombé sur Capturé (61), racheté - le guerrier revient avec son
                        // équipement (pas de perte "sans armure ni armes" dans ce cas précis, même
                        // exception que le traitement normal de Capturé).
                        Warband.Treasury -= rerollRansom;
                        sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, rerollRansom));
                    }
                    else if (reroll.ShowCapturedChoice)
                    {
                        warrior.Status = WarriorStatus.Dead;
                        foreach (var equipment in warrior.Equipment.ToList())
                            await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                        warrior.Equipment.Clear();
                        sentences.Add(string.Format(Loc["HistoryCapturedLostSentence"], warrior.Name));
                    }
                    else
                    {
                        foreach (var equipment in warrior.Equipment.ToList())
                            await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                        warrior.Equipment.Clear();
                        sentences.Add(string.Format(Loc["HistorySoldToPitsLostSentence"], warrior.Name));
                    }
                    changed = true;
                }
            }

            // "Blessures multiples" (16/21) : jusqu'à 6 sous-jets supplémentaires sur la table, chacun
            // devient sa propre Injury en plus du texte "Blessures multiples" ci-dessus - même
            // résolution complète que le jet principal (branche 23/25/24, Rancune 56, Vendu aux Fosses
            // 65 avec sa propre relance, Mort) depuis le 2026-09-04 (retour utilisateur - "les rolls des
            // blessures doivent être les mêmes qu'une blessure classique"), voir ApplyInjuryRollCoreAsync/
            // ApplyPitFightEntryAsync plus haut. Un sous-jet tombant lui-même sur "Blessures multiples"
            // (16/21) n'explose PAS en sous-sous-jets (confirmé explicitement par l'utilisateur - "pour
            // une 2ème blessure multiple, on ne peut pas en envoyer une autre si on est déjà en blessure
            // multiple") : il reste simplement le texte de référence créé par ApplyInjuryRollCoreAsync,
            // sans branche supplémentaire (cette entrée n'a jamais porté de mécanisme d'explosion).
            foreach (var sub in row.MultipleInjuryRolls)
            {
                if (string.IsNullOrWhiteSpace(sub.InjuryResultText)) continue;

                changed |= await ApplyInjuryRollCoreAsync(sub);

                // Mort (11-15) : jusqu'à cette passe, un sous-jet tombant sur la Mort n'était PAS
                // appliqué du tout (seul Capturé l'était) - un vrai manque, pas seulement Rancune/
                // Branche/Vendu aux Fosses. Le jet principal n'a pas besoin de ce bloc : sa propre Mort
                // passe par row.Status (déjà appliqué plus haut, avant ce sous-jet).
                if (sub.IsDeath)
                {
                    warrior.Status = WarriorStatus.Dead;
                    sentences.Add(string.Format(Loc["HistoryDeathSentence"], warrior.Name));
                    changed = true;
                }
                // Capturé (61) : même principe que le jet principal ci-dessus, pour un sous-jet
                // "Blessures multiples" qui tombe lui-même sur 61.
                else if (sub.ShowCapturedChoice)
                {
                    if (sub.IsRansomed && int.TryParse(sub.RansomAmount, out var subRansomAmount))
                    {
                        Warband.Treasury -= subRansomAmount;
                        sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, subRansomAmount));
                    }
                    else
                    {
                        warrior.Status = WarriorStatus.Dead;
                        foreach (var equipment in warrior.Equipment.ToList())
                            await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                        warrior.Equipment.Clear();
                        sentences.Add(string.Format(Loc["HistoryCapturedLostSentence"], warrior.Name));
                    }
                    changed = true;
                }
                // Vendu aux Fosses (65) : même principe, pour un sous-jet qui tombe lui-même sur 65 - sa
                // propre étape de combat de gladiateur (EndOfGameDialogViewModel.Steps, une étape par
                // occurrence, 2026-09-04 retour utilisateur).
                else if (sub.ShowSoldToThePits)
                {
                    changed |= await ApplyPitFightEntryAsync(sub);
                }
            }

            // Un jet D6 par figurine hors de combat dans ce groupe d'Hommes de main (règle confirmée
            // avec l'utilisateur, 2026-08-17 - pas un seul jet pour tout le groupe, voir
            // EndOfGameDialogViewModel.WarriorOutcomeRow.FigureInjuryRolls). Chaque résultat devient sa
            // propre Injury comme pour un Héros ; celles qui tombent sur "Mort" décrémentent HeadCount
            // d'autant plutôt que de faire basculer tout le groupe à WarriorStatus.Dead - le groupe ne
            // passe Mort (via suppression, voir plus bas) que si HeadCount tombe à 0.
            var headCountWiped = false;
            if (row.FigureInjuryRolls.Count > 0)
            {
                var deaths = 0;
                foreach (var figure in row.FigureInjuryRolls)
                {
                    if (string.IsNullOrWhiteSpace(figure.InjuryResultText)) continue;

                    var hasFigureRoll = int.TryParse(figure.ManualRoll, out var figureRoll);
                    var figureInjury = await GetOrCreateInjuryAsync(hasFigureRoll ? figureRoll : -1, warrior.IsHero, figure.InjuryResultText);
                    await _warbandService.AddWarriorInjuryAsync(warrior.Id, figureInjury);
                    if (figure.IsDeath) deaths++;
                }

                if (deaths > 0)
                {
                    warrior.HeadCount -= deaths;
                    changed = true;
                    headCountWiped = warrior.HeadCount <= 0;
                    sentences.Add(headCountWiped
                        ? string.Format(Loc["HistoryHenchmanWipedSentence"], warrior.Name)
                        : string.Format(Loc["HistoryHenchmanDeathSentence"], warrior.Name, deaths, warrior.HeadCount));
                }
            }

            // Couvre aussi le cas "dernier membre du groupe promu Héros" (voir le bloc Promotion
            // ci-dessus, qui décrémente HeadCount lui aussi) - pas seulement les morts par figurine.
            headCountWiped |= warrior.HeadCount <= 0;

            if (headCountWiped)
                await _warbandService.DeleteWarriorAsync(warrior.Id);
            else if (changed)
                await _warbandService.SaveWarriorAsync(warrior);
        }
    }

    /// <summary>Étape "Prisonniers ennemis" (EndOfGameDialogViewModel.IsCaptivesStep) - une seule fois
    /// pour toute la bande, pas par guerrier (contrairement à ApplyWarriorOutcomesAsync ci-dessus).
    /// Contrepartie de Capturé (61, un DE NOS guerriers capturé PAR l'adversaire, simplifié en rançon/
    /// mort faute de vrai capteur modélisé, voir SERIOUS_INJURIES_STATUS.md) : ici NOUS sommes le
    /// capteur, donc notre propre type de bande (EndOfGameDialogViewModel.IsUndeadWarband/
    /// IsPossessedWarband) est une donnée bien réelle - les 4 issues du livre restent toutes
    /// distinguées plutôt que simplifiées.</summary>
    private async Task ApplyCapturedEnemiesAsync(EndOfGameDialogViewModel dialogViewModel,
        IReadOnlyDictionary<string, WarriorArchetype> warriorArchetypesByEnglishName, List<string> sentences)
    {
        if (Warband is null || !dialogViewModel.HasCapturedEnemies) return;

        foreach (var entry in dialogViewModel.CapturedEnemies)
        {
            switch (entry.SelectedFate)
            {
                case CapturedEnemyFate.Ransomed when int.TryParse(entry.GoldAmount, out var ransom):
                    Warband.Treasury += ransom;
                    sentences.Add(string.Format(Loc["HistoryCapturedEnemyRansomedSentence"], ransom));
                    break;

                case CapturedEnemyFate.SoldToSlavers when int.TryParse(entry.GoldAmount, out var sold):
                    Warband.Treasury += sold;
                    sentences.Add(string.Format(Loc["HistoryCapturedEnemySoldSentence"], sold));
                    break;

                // Fusionne dans un groupe de Zombies déjà existant plutôt que de créer une ligne
                // séparée - même principe que GrantsFreeHenchmanArchetypeName (Traînard/Prisonniers) plus
                // haut dans ce fichier, un Zombie ne pouvant de toute façon porter aucun équipement.
                case CapturedEnemyFate.KilledForZombie:
                    if (warriorArchetypesByEnglishName.TryGetValue("Zombie", out var zombieArchetype))
                    {
                        var existingGroup = Henchmen.FirstOrDefault(r => r.Warrior.WarriorArchetypeId == zombieArchetype.Id);
                        if (existingGroup is not null)
                        {
                            existingGroup.Warrior.HeadCount += 1;
                            await _warbandService.SaveWarriorAsync(existingGroup.Warrior);
                        }
                        else
                        {
                            await _warbandService.RecruitWarriorAsync(Warband.Id, zombieArchetype, zombieArchetype.Name, headCount: 1);
                        }
                    }
                    sentences.Add(Loc["HistoryCapturedEnemyKilledSentence"]);
                    break;

                case CapturedEnemyFate.SacrificedForXp:
                    var leader = AllActiveWarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);
                    if (leader is not null)
                    {
                        leader.Warrior.Experience += 1;
                        await _warbandService.SaveWarriorAsync(leader.Warrior);
                        sentences.Add(string.Format(Loc["HistoryCapturedEnemySacrificedSentence"], leader.Warrior.Name));
                    }
                    break;
            }
        }
    }

    /// <summary>Étape "Vente de pierre magique" (livre, étape 4 - EndOfGameDialogViewModel.
    /// IsWyrdstoneSaleStep) : absente du wizard si le stock était vide à l'ouverture (voir Steps), donc
    /// ShardsToSell reste à 0 par défaut dans ce cas - rien à faire, pas besoin de re-vérifier ici.</summary>
    private async Task ApplyWyrdstoneSaleAsync(EndOfGameDialogViewModel dialogViewModel, List<string> sentences)
    {
        if (Warband is null || dialogViewModel.ShardsToSell <= 0) return;

        var gold = dialogViewModel.WyrdstoneSaleValue;
        Warband.WyrdstoneShards -= dialogViewModel.ShardsToSell;
        Warband.Treasury += gold;
        await _warbandService.SaveWarbandAsync(Warband);
        sentences.Add(string.Format(Loc["HistoryWyrdstoneSaleSentence"], dialogViewModel.ShardsToSell, gold));
    }

    /// <summary>Étape "Disponibilité des Vétérans" (livre, étape 5 - EndOfGameDialogViewModel.
    /// IsAvailableVeteransStep) : toujours présente dans le wizard, donc VeteranExperienceRoll est
    /// toujours renseigné ici (bloqué par ValidateAvailableVeteransStep sinon). Contrairement à un
    /// premier essai de ce chantier, ce pool n'est PAS persisté sur Warband - retour utilisateur
    /// 2026-08-28, texte du livre à l'appui ("Nouvelles recrues et groupes d'Hommes de main existants",
    /// p.144) : ce jet ne sert qu'à recruter DURANT CETTE séquence (étape 8, pas encore construite), les
    /// points excédentaires sont perdus, rien ne se cumule d'une Fin de Partie à l'autre. Juste une
    /// entrée d'Historique pour trace du jet, aucun état durable.</summary>
    private void ApplyAvailableVeterans(EndOfGameDialogViewModel dialogViewModel, List<string> sentences)
    {
        if (!int.TryParse(dialogViewModel.VeteranExperienceRoll, out var pool)) return;

        sentences.Add(string.Format(Loc["HistoryAvailableVeteransSentence"], pool));
    }

    /// <summary>Étapes "Objets rares" + "Achat" (livre, étape 6, scindée en 2 - EndOfGameDialogViewModel.
    /// IsRareItemsStep/IsRareItemPurchaseStep, retour utilisateur 2026-08-28 : jamais d'achat automatique
    /// sur un jet réussi, une case "Acheter" décidée par le joueur sur une étape séparée) : chaque
    /// recherche réellement ACHETÉE (RareItemSearchEntry.IsPurchased - jet réussi ET case cochée ET prix
    /// définitivement connu, le step Achat bloquant Next si le total coché dépasserait la trésorerie) va
    /// dans l'inventaire de bande NON assigné (IWarbandService.AddWarbandEquipmentAsync, même flux qu'un
    /// objet trouvé en Exploration - à équiper plus tard via WarbandInventoryDialog), payé (Warband.
    /// Treasury -= EffectiveCost, prix de base ajusté Gromril/Ithilmar si un matériau est attaché PLUS le
    /// supplément de prix variable éventuel - voir RareItemSearchEntry.EffectiveCost/HasVariablePrice) et
    /// emporte la SpecialRule avec elle (MaterialRule, même mécanisme que "Épée Ornée" - Charrette
    /// Renversée). Une recherche sans objet choisi, ratée, ou décochée ne fait rien pour ce Héros -
    /// aucune pénalité au livre dans aucun de ces cas.</summary>
    /// <summary>Étape Achat/Recrutement (EndOfGameDialogViewModel.IsRareItemPurchaseStep) - traite les
    /// deux modes (RareItemSearchEntry.IsSearchingForCharacter) : objets achetés (IsPurchased, inchangé)
    /// ET personnages recrutés (IsRecruited). Un personnage à frais en or (RareItemSearchEntry.
    /// HasHireCost - Johann/Veskit/Marianna, PAS Bertha/None ni Nicodemus/Wyrdstone ni Ulli & Marquand/
    /// Pair) prélève désormais réellement son HireCost sur la trésorerie (2026-09-01, user request),
    /// SAUF s'il a un objet de paiement alternatif ET que le joueur a coché cette option
    /// (IsPayingWithAlternativeItem, ex. Johann/Ombre Cramoisie) - dans ce cas un exemplaire de l'objet
    /// est retiré de l'inventaire de bande à la place (retrait total de la pile, voir DramatisPersona.
    /// AlternativePaymentItemId's own doc - pas de mécanisme de pile partielle dans cette app). Un
    /// personnage sans frais (Bertha) reste inséré gratuitement comme avant.</summary>
    private async Task ApplyRareItemSearchAsync(EndOfGameDialogViewModel dialogViewModel, List<EquipmentItem> localizedEquipment, List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var entry in dialogViewModel.RareItemSearchEntries.Where(e => e.IsPurchased))
        {
            var item = entry.SelectedItem!;
            Warband.Treasury -= entry.EffectiveCost!.Value;
            await _warbandService.SaveWarbandAsync(Warband);
            await _warbandService.AddWarbandEquipmentAsync(Warband.Id, item, materialRule: entry.SelectedMaterial);
            var displayName = entry.SelectedMaterial is { } material ? $"{item.Name} ({material.Abbreviation})" : item.Name;
            sentences.Add(string.Format(Loc["HistoryRareItemFoundSentence"], entry.HeroName, displayName));
        }

        foreach (var entry in dialogViewModel.RareItemSearchEntries.Where(e => e.IsRecruited))
        {
            var persona = entry.SelectedCharacter!;

            // Recrute UN personnage (persona ou son partenaire de paire) - factorisé car Ulli & Marquand
            // (2026-09-01, "vous devez les recruter tous les deux pour une bataille") appellent ceci deux
            // fois pour un seul frais (voir plus bas), tous les autres personnages une seule fois.
            // "id => catalog.First(...)" plutôt que "catalog.Where(id contains)" - ce dernier itère le
            // CATALOGUE (jamais deux fois le même EquipmentItem), donc perdrait silencieusement un doublon
            // (ex. Bertha : StartingEquipmentIds contient deux fois l'id du Marteau de Guerre Sigmarite,
            // voir DramatisPersonae.json - retour utilisateur 2026-09-01, "j'ai pas réussi à le gérer").
            // RecruitDramatisPersonaAsync regroupe ensuite ces doublons en une seule WarriorEquipment row
            // à Quantity=2 plutôt que deux rows identiques.
            async Task RecruitOneAsync(DramatisPersona toRecruit)
            {
                var recruitEquipment = toRecruit.StartingEquipmentIds
                    .Select(itemId => localizedEquipment.FirstOrDefault(e => e.Id == itemId))
                    .Where(item => item is not null)
                    .Select(item => item!)
                    .ToList();
                await _warbandService.RecruitDramatisPersonaAsync(Warband.Id, toRecruit, toRecruit.Name, recruitEquipment, toRecruit.Skills);
            }

            if (entry.HasWyrdstoneCost)
            {
                // Nicodemus - "he has no interest in gold... must be paid a wyrdstone shard when he
                // joins the warband" (2026-09-01, "on a qu'a brancher la wyrstone à son paiement"). Pas
                // de repli sur l'or : il n'a aucune option de paiement en or, contrairement à Johann.
                Warband.WyrdstoneShards -= entry.EffectiveWyrdstoneCostForShards;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryDramatisPersonaRecruitedWyrdstoneSentence"], entry.HeroName, persona.Name));
            }
            else if (!entry.HasHireCost)
            {
                sentences.Add(string.Format(Loc["HistoryDramatisPersonaRecruitedSentence"], entry.HeroName, persona.Name));
            }
            else if (entry.IsPayingWithAlternativeItem
                && Inventory.FirstOrDefault(w => w.Item.Id == persona.AlternativePaymentItemId) is { } paymentStash)
            {
                await _warbandService.RemoveWarbandEquipmentAsync(paymentStash.Id);
                sentences.Add(string.Format(Loc["HistoryDramatisPersonaRecruitedWithItemSentence"], entry.HeroName, persona.Name, paymentStash.Item.Name));
            }
            else
            {
                // Repli sur l'or si "payer avec l'objet" était coché mais que l'objet a entre-temps
                // disparu de l'inventaire (deux Héros cherchant/recrutant le même personnage la même Fin
                // de Partie, cas limite non bloqué par l'UI) - même comportement que si la case n'avait
                // jamais été cochée. Couvre aussi Ulli & Marquand (FeeKind.Pair, "30 Couronnes d'Or pour
                // les deux" - un seul HireCost partagé, jamais doublé) : sentence dédiée nommant les deux
                // quand persona.PairedWithDramatisPersona est renseigné.
                Warband.Treasury -= persona.HireCost!.Value;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(persona.PairedWithDramatisPersona is { } pairPartner
                    ? string.Format(Loc["HistoryDramatisPersonaPairRecruitedGoldSentence"], entry.HeroName, persona.Name, pairPartner.Name, persona.HireCost!.Value)
                    : string.Format(Loc["HistoryDramatisPersonaRecruitedGoldSentence"], entry.HeroName, persona.Name, persona.HireCost!.Value));
            }

            await RecruitOneAsync(persona);
            // "Ulli et Marquand ne se séparent jamais et vous devez les recruter tous les deux pour une
            // bataille" - recrute automatiquement le partenaire caché du picker (voir
            // DramatisPersona.IsHiddenFromSearchPicker), sans frais supplémentaire (déjà couvert ci-dessus).
            if (persona.PairedWithDramatisPersona is { } partner)
                await RecruitOneAsync(partner);
        }
    }

    /// <summary>"Vagabond" departure (voir DramatisPersona.IsWanderer) - retour utilisateur 2026-09-01 :
    /// Bertha "sort complètement de la bande" après CHAQUE bataille (qu'elle ait pu se battre ou non - le
    /// jet d'Aide conditionnelle du Lancement de Partie, voir RatingGapAidTable, décide seulement si elle
    /// a participé à CETTE bataille, pas si elle repart ensuite : elle repart de toute façon), et ne peut
    /// être réintégrée que par une nouvelle recherche de Personnage spécial. Généralisé aux 3 personnages
    /// Vagabonds (Aenur, Ulli &amp; Marquand aussi - IsWanderer documente déjà "never stays with the
    /// warband beyond one battle" pour eux également), pas seulement Bertha - confirmé via
    /// AskUserQuestion. Retrait COMPLET (WarbandService.DeleteWarriorAsync, équipement/compétences avec)
    /// plutôt qu'un statut "Parti(e)" dédié - décision utilisateur (même question) : une future recherche
    /// recrée une fiche neuve, aucun historique de blessure conservé, mais évite d'avoir deux fiches du
    /// même personnage en même temps.
    ///
    /// Scope volontairement dialogViewModel.WarriorRows (le roster Actif figé à L'OUVERTURE de ce wizard)
    /// plutôt que le roster courant : un Personnage spécial fraîchement recruté PENDANT cette même Fin de
    /// Partie (juste au-dessus, ApplyRareItemSearchAsync) n'apparaît jamais dans WarriorRows - il reste
    /// donc pour au moins la prochaine bataille, comme voulu ("A request for Bertha to aid the warband
    /// must be made for EACH battle" implique qu'une fois recrutée elle participe à celle-ci, puis repart
    /// à LA FIN de celle-ci, pas immédiatement). Mort/Retraité exclus : déjà des états terminaux gérés
    /// ailleurs, rien à faire de plus pour eux ici.</summary>
    private async Task ApplyWandererDeparturesAsync(EndOfGameDialogViewModel dialogViewModel, List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var row in dialogViewModel.WarriorRows)
        {
            var warrior = row.Warrior;
            if (!warrior.IsDramatisPersona || warrior.Status is WarriorStatus.Dead or WarriorStatus.Retired) continue;

            var persona = _recruitableDramatisPersonae.FirstOrDefault(p => p.Id == warrior.DramatisPersonaId);
            if (persona is not { IsWanderer: true }) continue;

            await _warbandService.DeleteWarriorAsync(warrior.Id);
            // Délai de re-recherche (2026-09-01, Aenur/Ulli & Marquand - PAS Bertha, voir
            // RequiresCooldownBeforeResearch's own doc) - posé APRÈS le nettoyage global juste avant cet
            // appel (voir EndOfGame()), donc jamais effacé par lui.
            if (persona.RequiresCooldownBeforeResearch)
                await _warbandService.AddDramatisPersonaCooldownAsync(Warband.Id, persona.Id);
            // Une Poignée d'Or (2026-09-01) : un guerrier basculé "hostile" sur sa carte (voir
            // WarbandDetailViewModel.ToggleHostile) part de toute façon comme tout Vagabond, mais avec
            // une phrase d'historique distincte - il quitte parce que l'adversaire l'a corrompu, pas
            // simplement parce qu'il repart de lui-même. Pas de paiement côté CETTE bande (voir
            // EndOfGameDialogViewModel.DramatisPersonae.cs's own doc - seul le camp qui gagne le contrôle
            // paie, jamais celui qui les a recrutés).
            sentences.Add(string.Format(warrior.IsHostileThisBattle ? Loc["HistoryDramatisPersonaCorruptedSentence"] : Loc["HistoryDramatisPersonaDepartedSentence"], warrior.Name));
        }
    }

    /// <summary>Étape "Dramatis Personae" (EndOfGameDialogViewModel.IsDramatisPersonaeStep) - règle la
    /// solde de chaque Dramatis Persona à frais récurrents déjà engagé : en or (Johann/Veskit/Marianna)
    /// OU en pierre magique (Nicodemus, DramatisPersonaUpkeepEntry.IsWyrdstoneFee - 2026-09-01, "on a
    /// qu'a brancher la wyrstone à son paiement"). Même Payer/Renvoyer que ApplyHiredSwordUpkeepAsync
    /// ci-dessous, mais sans équivalent d'IsPrepaidFree (rien comme "Une Faveur Rendue" n'existe pour un
    /// Dramatis Persona) et sans recrutement combiné (un Dramatis Persona se recrute via
    /// ApplyRareItemSearchAsync, pas ici). Solde refusée/impayée = il quitte la bande pour de bon - même
    /// traitement que le refus d'un Franc-Tireur (retrait complet, équipement/compétences avec) : une
    /// future recherche recréera une fiche neuve. Traite aussi, en fin de méthode, l'éventuel paiement
    /// "Une Poignée d'Or" (Corruption ou Rétention de Marquand &amp; Ulli - voir EndOfGameDialogViewModel.
    /// WantsToRecordPairCorruption/PairRetentionAmount, saisis sur l'étape "Une Poignée d'Or" dédiée
    /// depuis le 2026-09-04, voir EndOfGameDialogViewModel.PairEngagement.cs), appliqué ici avec le reste
    /// de la comptabilité Dramatis Personae, sujet indépendant de l'étape qui l'affiche.</summary>
    private async Task ApplyDramatisPersonaUpkeepAsync(EndOfGameDialogViewModel dialogViewModel, List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var entry in dialogViewModel.DramatisPersonaUpkeepEntries)
        {
            var warrior = entry.Warrior;

            if (entry.WillPay == true && entry.IsWyrdstoneFee)
            {
                Warband.WyrdstoneShards -= entry.UpkeepCost;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryDramatisPersonaWyrdstoneUpkeepPaidSentence"], warrior.Name));
            }
            else if (entry.WillPay == true)
            {
                Warband.Treasury -= entry.UpkeepCost;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryHiredSwordUpkeepPaidSentence"], warrior.Name, entry.UpkeepCost));
            }
            else
            {
                await _warbandService.DeleteWarriorAsync(warrior.Id);
                sentences.Add(string.Format(Loc["HistoryDramatisPersonaUpkeepRefusedSentence"], warrior.Name));
            }
        }

        // Une Poignée d'Or (2026-09-01) : case optionnelle pour une bande qui n'a PAS déjà Ulli &
        // Marquand (voir EndOfGameDialogViewModel.ShowPairCorruptionOption) - elle vient de gagner leur
        // contrôle par corruption réussie, donc paie SA PROPRE trésorerie (jamais celle qui les a
        // recrutés à l'origine, voir la SpecialRule "A Fistful of Crowns"). Ne recrute PAS réellement la
        // paire ici (aucune ligne Warrior créée) - purement le paiement, cohérent avec "sur un autre
        // appareil pas celle présente en local" (retour utilisateur) : cette bande n'a pas forcément
        // accès aux fiches Marquand/Ulli elles-mêmes.
        //
        // !IsPairCorruptionUnaffordable (2026-09-04, retour utilisateur - vrai bug trouvé en creusant un
        // signalement sur les valeurs du magot) : payer en or ne se fait QUE si la bande peut réellement
        // payer - "instead" dans "if the controlling warband can't pay a successful bribe... the pair
        // INSTEAD seizes an equal value of equipment" est une lecture stricte confirmée par l'utilisateur
        // (aucun or ne sort du tout dans le cas impayable, Céder du matériel/Duel couvre l'INTÉGRALITÉ du
        // montant) - sans ce garde-fou, la trésorerie était débitée du montant COMPLET ICI, EN PLUS de la
        // saisie d'équipement/du duel appliqués séparément (ApplyPairEquipmentSeizureIfNeededAsync/
        // ApplyPairDuelIfNeededAsync juste en dessous) : double paiement (or perdu ET objets/meneur
        // perdus pour la même dette).
        if (dialogViewModel.WantsToRecordPairCorruption && !dialogViewModel.IsPairCorruptionUnaffordable
            && int.TryParse(dialogViewModel.PairCorruptionAmount, out var corruptionAmount))
        {
            Warband.Treasury -= corruptionAmount;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryPairCorruptionPaidSentence"], corruptionAmount));
        }

        // "C'est l'heure de payer !" (2026-09-01) - cas symétrique pour une bande qui possède DÉJÀ Ulli &
        // Marquand (EndOfGameDialogViewModel.ShowPairRetentionOption) : ce qu'elle a dû payer pour les
        // GARDER après une tentative adverse ("seul le camp qui obtient OU GARDE le contrôle paie", voir
        // la SpecialRule "A Fistful of Crowns"). 0/vide (pas de tentative) ne fait rien - PairRetentionAmount
        // reste alors vide, int.TryParse échoue, ce bloc est un no-op. Appliqué en tout dernier (après la
        // boucle Payer/Renvoyer ci-dessus) - "à la fin de tous les décomptes", retour utilisateur. Même
        // garde-fou !IsPairRetentionUnaffordable que la Corruption ci-dessus (2026-09-04) - double
        // paiement sinon.
        if (!dialogViewModel.IsPairRetentionUnaffordable
            && int.TryParse(dialogViewModel.PairRetentionAmount, out var retentionAmount) && retentionAmount > 0)
        {
            Warband.Treasury -= retentionAmount;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryPairRetentionPaidSentence"], dialogViewModel.PairRetentionLabel, retentionAmount));
        }
    }

    /// <summary>"Où est l'Argent ?" - "Céder du matériel" (2026-09-01, retour utilisateur - remplace
    /// l'ancien simple rappel textuel) : retire réellement du stash de la bande les objets sélectionnés
    /// automatiquement par EndOfGameDialogViewModel.SeizedEquipmentItems (plus petit au plus grand, arrêt
    /// dès la cible couverte - voir sa propre doc). Une ligne = une pile entière (RemoveWarbandEquipmentAsync
    /// supprime la ligne, jamais de retrait partiel, même simplification que partout ailleurs dans l'app).</summary>
    private async Task ApplyPairEquipmentSeizureIfNeededAsync(EndOfGameDialogViewModel dialogViewModel, List<string> sentences)
    {
        var isUnaffordable = dialogViewModel.IsPairCorruptionUnaffordable || dialogViewModel.IsPairRetentionUnaffordable;
        if (Warband is null || !isUnaffordable || !dialogViewModel.WantsEquipmentSeizure) return;

        var seized = dialogViewModel.SeizedEquipmentItems;
        if (seized.Count == 0) return;

        foreach (var item in seized)
            await _warbandService.RemoveWarbandEquipmentAsync(item.Id);

        var pairLabel = dialogViewModel.IsPairCorruptionUnaffordable ? dialogViewModel.PairCorruptionLabel : dialogViewModel.PairRetentionLabel;
        sentences.Add(string.Format(Loc["HistoryPairEquipmentSeizedSentence"], pairLabel, dialogViewModel.SeizedEquipmentTotalValue));
    }

    /// <summary>"Où est l'Argent ?" (2026-09-01) - repli si le montant à payer dépasserait le solde
    /// prévisionnel de la bande, dans l'un ou l'autre des deux cas mutuellement exclusifs (une bande ne
    /// possède jamais Ulli &amp; Marquand ET tente de les débaucher à la fois) : IsPairCorruptionUnaffordable
    /// (ne les possède pas) ou IsPairRetentionUnaffordable (les possède déjà) - toutes deux dans
    /// EndOfGameDialogViewModel.PairEngagement.cs. Voir ApplyPairEquipmentSeizureIfNeededAsync juste
    /// au-dessus pour l'autre choix possible
    /// ("Céder du matériel"). Victoire = rien de plus ; défaite = mort automatique du meneur (2026-09-03,
    /// retour utilisateur - "en cas de défaite, le chef de bande est forcément mort"), pas de jet sur la
    /// table des Blessures Graves contrairement à une première version qui s'inspirait par erreur de
    /// Vendu aux Fosses (WonPitFight/SoldToPitsRerollRoll) - absent du texte de cette règle-ci.</summary>
    private async Task ApplyPairDuelIfNeededAsync(EndOfGameDialogViewModel dialogViewModel, List<string> sentences)
    {
        var isUnaffordable = dialogViewModel.IsPairCorruptionUnaffordable || dialogViewModel.IsPairRetentionUnaffordable;
        if (Warband is null || !isUnaffordable || !dialogViewModel.WantsDuel) return;

        var leaderRow = dialogViewModel.WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);
        if (leaderRow is null) return;
        var warrior = leaderRow.Warrior;

        if (dialogViewModel.WonDuel)
        {
            sentences.Add(string.Format(Loc["HistoryPairDuelWonSentence"], warrior.Name));
            return;
        }

        warrior.Status = WarriorStatus.Dead;
        sentences.Add(string.Format(Loc["HistoryPairDuelLostSentence"], warrior.Name));
        await _warbandService.SaveWarriorAsync(warrior);
    }

    /// <summary>Étape "Francs-Tireurs" (EndOfGameDialogViewModel.IsHiredSwordsStep) - règle la solde de
    /// chaque Franc-Tireur déjà engagé (payée/refusée/déjà prépayée par "Une Faveur Rendue") ET recrute
    /// le nouveau éventuellement choisi à la même étape. Un Franc-Tireur déjà Mort/Retraité (décompté
    /// plus haut par ApplyWarriorOutcomesAsync) n'a plus d'entrée d'upkeep ici (WarriorRows filtre déjà
    /// les guerriers Actifs uniquement) - rien à facturer pour lui.</summary>
    private async Task ApplyHiredSwordUpkeepAsync(EndOfGameDialogViewModel dialogViewModel, List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var entry in dialogViewModel.HiredSwordUpkeepEntries)
        {
            var warrior = entry.Warrior;

            if (entry.IsPrepaidFree)
            {
                // "Une Faveur Rendue" ne couvre que la toute prochaine solde - consommée ici, qu'elle
                // qu'ait été la partie jouée entre-temps (voir Models.Warrior.HiredSwordUpkeepPrepaid).
                warrior.HiredSwordUpkeepPrepaid = false;
                await _warbandService.SaveWarriorAsync(warrior);
                sentences.Add(string.Format(Loc["HistoryHiredSwordUpkeepPrepaidSentence"], warrior.Name));
                continue;
            }

            if (entry.WillPay == true)
            {
                Warband.Treasury -= entry.UpkeepCost;
                await _warbandService.SaveWarbandAsync(Warband);
                sentences.Add(string.Format(Loc["HistoryHiredSwordUpkeepPaidSentence"], warrior.Name, entry.UpkeepCost));
            }
            else
            {
                // Solde refusée/impayée - il quitte la bande pour de bon, perd toute son XP (livre des
                // règles) - même s'il est réengagé plus tard, ce sera une toute nouvelle ligne (voir
                // Models.Warrior.HiredSwordId).
                await _warbandService.DeleteWarriorAsync(warrior.Id);
                sentences.Add(string.Format(Loc["HistoryHiredSwordLeftSentence"], warrior.Name));
            }
        }

        if (dialogViewModel.SelectedNewHiredSword is { } hiredSword)
        {
            var localizedEquipment = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);
            var startingEquipment = localizedEquipment.Where(e => hiredSword.StartingEquipmentIds.Contains(e.Id)).ToList();
            var name = dialogViewModel.NewHiredSwordName.Trim();

            await _warbandService.RecruitHiredSwordAsync(Warband.Id, hiredSword, name, startingEquipment);
            Warband.Treasury -= hiredSword.HireCost;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryHiredSwordHiredSentence"], name, hiredSword.Name));
        }
    }

    /// <summary>Applique la mutation réelle d'un résultat de Blessure Grave Palier 1 (voir Core.Rules.
    /// SeriousInjuryEffectTable) - appelée pour le jet principal (branche 23/25 comprise) et pour
    /// chaque sous-jet "Blessures multiples". Retourne true si warrior a été modifié (pour que
    /// l'appelant sache qu'il doit le sauvegarder, même convention que `changed` dans
    /// ApplyWarriorOutcomesAsync) - LoseAllEquipment se sauvegarde lui-même via RemoveWarriorEquipmentAsync
    /// (une ligne de jointure à la fois) donc ne fait pas remonter `changed` à true pour autant.</summary>
    private async Task<bool> ApplySeriousInjuryEffectAsync(Warrior warrior, SeriousInjuryOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case SeriousInjuryEffectKind.CharacteristicPenalty when outcome.Field is { } field:
                ApplyCharacteristicPenalty(warrior, field);
                return true;

            case SeriousInjuryEffectKind.LoseAllEquipment:
                foreach (var equipment in warrior.Equipment.ToList())
                    await _warbandService.RemoveWarriorEquipmentAsync(equipment.Id);
                warrior.Equipment.Clear();
                return false;

            case SeriousInjuryEffectKind.GainExperience:
                warrior.Experience += 1;
                return true;

            // Cumulatif (+=), pas un simple remplacement : "Blessures multiples" (16/21) peut produire
            // PLUSIEURS sous-résultats qui accordent chacun du temps Malade pour le même guerrier (ex.
            // deux Blessures profondes, ou Blessure au bras légère + Blessure profonde) - le texte du
            // livre est explicite ("cumulez tous les effets obtenus"), un remplacement perdrait
            // silencieusement les parties déjà accumulées par un sous-résultat précédent dans la même
            // résolution. Repéré par analogie avec le correctif Vieille blessure (2026-08-26, un jet par
            // instance portée plutôt qu'un seul par guerrier) - même famille de bug ("plusieurs
            // occurrences du même effet doivent se cumuler, pas s'écraser").
            case SeriousInjuryEffectKind.MissNextGame:
                warrior.Status = WarriorStatus.Sick;
                warrior.SickGamesRemaining += 1;
                return true;

            case SeriousInjuryEffectKind.MissGamesRollD3:
                // outcome.Value porte le 1D3 saisi par le joueur dans le wizard (voir
                // WarriorOutcomeRow.DeepWoundSubRoll/InjurySubRollEntry.DeepWoundSubRoll, injecté par
                // ApplyWarriorOutcomesAsync ci-dessus) - repli défensif sur un jet auto si jamais absent,
                // ne devrait plus arriver depuis que ce sous-jet est visible/validé dans le wizard.
                warrior.Status = WarriorStatus.Sick;
                warrior.SickGamesRemaining += outcome.Value ?? SeriousInjuryEffectTable.RollD3();
                return true;

            case SeriousInjuryEffectKind.ForcedRetirement:
                warrior.Status = WarriorStatus.Retired;
                return true;

            default:
                return false;
        }
    }

    /// <summary>Contrepartie en soustraction d'ApplyCharacteristicIncrease, pour les résultats de
    /// Blessure Grave Palier 1 qui infligent un -1 permanent - pas de vérification de plancher (la
    /// caractéristique peut descendre en dessous de 0, comme sur une vraie feuille de bande) ni de
    /// suivi IncreasedCharacteristics (celui-ci ne suit que les gains d'un Homme de main, sans objet
    /// pour une perte).</summary>
    private static void ApplyCharacteristicPenalty(Warrior warrior, CharacteristicField field)
    {
        switch (field)
        {
            case CharacteristicField.Movement: warrior.Movement -= 1; break;
            case CharacteristicField.WeaponSkill: warrior.WeaponSkill -= 1; break;
            case CharacteristicField.BallisticSkill: warrior.BallisticSkill -= 1; break;
            case CharacteristicField.Strength: warrior.Strength -= 1; break;
            case CharacteristicField.Toughness: warrior.Toughness -= 1; break;
            case CharacteristicField.Wounds: warrior.Wounds -= 1; break;
            case CharacteristicField.Initiative: warrior.Initiative -= 1; break;
            case CharacteristicField.Attacks: warrior.Attacks -= 1; break;
            case CharacteristicField.Leadership: warrior.Leadership -= 1; break;
        }
    }

    /// <summary>Applique le +1 d'un résultat de Progression "Caractéristique" (voir AdvanceRollEntry.
    /// ResolvedField, déjà validé éligible - maximum racial respecté, et pour un Homme de main jamais
    /// deux fois la même caractéristique - au moment où le joueur l'a résolu dans le wizard) - switch
    /// explicite sur les 9 champs plutôt que de la réflexion, même style que le reste d'EntityMapping/
    /// ce fichier. Pour un Homme de main, la caractéristique rejoint aussi Warrior.
    /// IncreasedCharacteristics (jamais pour un Héros, qui n'a pas cette restriction).</summary>
    private static void ApplyCharacteristicIncrease(Warrior warrior, CharacteristicField field)
    {
        switch (field)
        {
            case CharacteristicField.Movement: warrior.Movement += 1; break;
            case CharacteristicField.WeaponSkill: warrior.WeaponSkill += 1; break;
            case CharacteristicField.BallisticSkill: warrior.BallisticSkill += 1; break;
            case CharacteristicField.Strength: warrior.Strength += 1; break;
            case CharacteristicField.Toughness: warrior.Toughness += 1; break;
            case CharacteristicField.Wounds: warrior.Wounds += 1; break;
            case CharacteristicField.Initiative: warrior.Initiative += 1; break;
            case CharacteristicField.Attacks: warrior.Attacks += 1; break;
            case CharacteristicField.Leadership: warrior.Leadership += 1; break;
        }

        if (!warrior.IsHero && !warrior.IncreasedCharacteristics.Contains(field))
            warrior.IncreasedCharacteristics.Add(field);
    }

    /// <summary>Doit être appelée APRÈS ApplyWarriorOutcomesAsync (voir l'invariant documenté à
    /// l'appel dans EndOfGame ci-dessus) - celle-ci resynchronise warrior.Status depuis row.Status
    /// (Actif/Mort uniquement) pour chaque guerrier, ce qui écraserait silencieusement un statut Sick/
    /// Mort posé avant elle par un mécanisme d'Exploration (bug trouvé le 2026-08-18 pour Sick ; même
    /// principe appliqué à La Fosse). Regroupe tous les statuts posés hors du flux Blessure normal, pas
    /// seulement la Maladie malgré le nom.</summary>
    private async Task ApplySicknessLifecycleAsync(EndOfGameDialogViewModel dialogViewModel, List<WarriorRow> previouslySickWarriors)
    {
        // La partie qu'ils manquaient (voir previouslySickWarriors dans EndOfGame) vient d'être
        // enregistrée par CE wizard - décrémente le compteur de parties restantes (voir
        // Warrior.SickGamesRemaining), ne redevient Actif qu'une fois ce compteur à 0 (Blessure
        // profonde impose D3 parties, pas juste celle-ci).
        foreach (var row in previouslySickWarriors)
        {
            row.Warrior.SickGamesRemaining = Math.Max(0, row.Warrior.SickGamesRemaining - 1);
            if (row.Warrior.SickGamesRemaining == 0)
            {
                row.Warrior.Status = WarriorStatus.Active;
                // La chip temporaire qui explique la Maladie (Blessure au bras/Jambe écrasée légère,
                // Blessure profonde - voir Models.WarriorInjury.IsTemporary) n'a plus lieu d'être une
                // fois le guerrier de nouveau Actif ; les injuries permanentes (bras amputé, etc.) ne
                // sont jamais IsTemporary et restent donc intactes.
                await _warbandService.RemoveTemporaryInjuriesAsync(row.Warrior.Id);
            }
            await _warbandService.SaveWarriorAsync(row.Warrior);
        }

        // Puits en échec (test d'Endurance, voir EndOfGame/ApplyExplorationOutcomeAsync).
        if (dialogViewModel.StatTestSickHero is { } newlySickHero)
        {
            newlySickHero.Warrior.Status = WarriorStatus.Sick;
            newlySickHero.Warrior.SickGamesRemaining = 1;
            await _warbandService.SaveWarriorAsync(newlySickHero.Warrior);
        }

        // La Fosse, sous-jet 1 (Héros envoyé dévoré, voir EndOfGame/ApplyExplorationOutcomeAsync).
        if (dialogViewModel.PitDevouredHero is { } devouredHero)
        {
            devouredHero.Warrior.Status = WarriorStatus.Dead;
            await _warbandService.SaveWarriorAsync(devouredHero.Warrior);
        }
    }
}
