using CommunityToolkit.Mvvm.Input;
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
        var previouslySickWarriors = Heroes.Concat(Henchmen).Where(r => r.Warrior.Status == WarriorStatus.Sick).ToList();

        var activeWarriorRows = Heroes.Concat(Henchmen)
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

        var dialogViewModel = new EndOfGameDialogViewModel(activeWarriorRows, _skillPicker, _detailDialogs, _libraryService, Warband.WarbandArchetypeId,
            warbandArchetypeName, Warband.PendingExplorationBonusDie, Warband.HasCatacombReroll, Warband.Treasury, explorationResults, equipmentItemsByEnglishName, specialRulesByEnglishName,
            warriorArchetypesByEnglishName, skillIdsByEnglishName, injuryCatalog);
        if (await ShowDialogAsync(new EndOfGameDialog(dialogViewModel)) != true) return;

        await Loading.RunAsync(async () =>
        {
            var sentences = new List<string> { string.Format(Loc["HistoryResultSentence"], dialogViewModel.SelectedResult) };

            await ApplyExplorationOutcomeAsync(dialogViewModel, englishEquipment, equipmentItemsByEnglishName, englishSpecialRules, sentences);
            await ApplyWarriorOutcomesAsync(dialogViewModel, language, sentences);
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
                var leader = Heroes.Concat(Henchmen).FirstOrDefault(r => r.Warrior.IsLeader);
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
        // sur l'entrée générique (BranchRange vide) si elle existe, sinon une entrée arbitraire (cas
        // hors-périmètre : un sous-jet "Blessures multiples" tombant sur 23/25 n'a pas de sous-jet de
        // branche imbriqué dans ce wizard).
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

            // "Blessures multiples" (16/21) : jusqu'à 6 sous-jets supplémentaires sur la table, chacun
            // devient sa propre Injury en plus du texte "Blessures multiples" ci-dessus.
            foreach (var sub in row.MultipleInjuryRolls)
            {
                if (string.IsNullOrWhiteSpace(sub.InjuryResultText)) continue;

                var hasSubRoll = int.TryParse(sub.ManualRoll, out var subRoll);

                // Même table Palier 1 que le jet principal ci-dessus (un sous-jet "Blessures multiples"
                // est un jet D66 complet sur cette même table) - à l'exception de la branche 23/25, qui
                // n'a pas de sous-jet dédié ici (pas de second niveau de jet imbriqué dans ce wizard,
                // décision de portée) : reste texte de référence pur pour ce cas précis, comme avant
                // cette passe (GetOrCreateInjuryAsync retombe alors sur l'entrée catalogue générique).
                var subOutcome = hasSubRoll && SeriousInjuryEffectTable.TryGetOutcome(subRoll, alreadyBlindedInOneEye, out var o) ? o : null;

                // Même principe que pour le jet principal ci-dessus : le nombre de parties manquées est
                // le 1D3 saisi par le joueur (sub.DeepWoundSubRoll), pas un jet invisible.
                if (subOutcome?.Kind == SeriousInjuryEffectKind.MissGamesRollD3)
                    subOutcome = subOutcome with { Value = int.TryParse(sub.DeepWoundSubRoll, out var subD3) ? subD3 : SeriousInjuryEffectTable.RollD3() };

                var subIsTemporary = subOutcome?.Kind is SeriousInjuryEffectKind.MissNextGame or SeriousInjuryEffectKind.MissGamesRollD3;

                var subInjury = await GetOrCreateInjuryAsync(hasSubRoll ? subRoll : -1, warrior.IsHero, sub.InjuryResultText);
                await _warbandService.AddWarriorInjuryAsync(warrior.Id, subInjury, subIsTemporary);
                sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, sub.InjuryResultText));

                if (subOutcome is not null)
                {
                    changed |= await ApplySeriousInjuryEffectAsync(warrior, subOutcome);
                    if (subOutcome.Kind == SeriousInjuryEffectKind.ForcedRetirement)
                        sentences.Add(string.Format(Loc["HistoryForcedRetirementSentence"], warrior.Name));
                }
                if (hasSubRoll && subRoll == 31) alreadyBlindedInOneEye = true;
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
