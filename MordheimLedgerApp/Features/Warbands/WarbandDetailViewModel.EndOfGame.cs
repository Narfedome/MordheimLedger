using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
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

        var dialogViewModel = new EndOfGameDialogViewModel(activeWarriorRows, _skillPicker, _detailDialogs, Warband.WarbandArchetypeId, explorationResults, equipmentItemsByEnglishName, specialRulesByEnglishName, skillIdsByEnglishName);
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

        // Find-or-create par nom (résolu dans la langue courante, comme le catalogue lui-même) dans le
        // catalogue Injury - la table Blessures Graves a un texte fixe par jet, donc pas de risque de
        // quasi-doublons.
        async Task<Injury> GetOrCreateInjuryAsync(string name)
        {
            injuryCatalog ??= await _libraryService.GetInjuriesAsync(language);
            var injury = injuryCatalog.FirstOrDefault(i => i.Name == name);
            if (injury is null)
            {
                injury = new Injury { Name = name, Source = ContentSource.Official };
                await _libraryService.SaveInjuryAsync(injury, language);
                injuryCatalog.Add(injury);
            }
            return injury;
        }

        foreach (var row in dialogViewModel.WarriorRows)
        {
            var warrior = row.Warrior;
            var changed = false;

            if (row.ExperienceGained != 0)
            {
                warrior.Experience += row.ExperienceGained;
                sentences.Add(string.Format(Loc["HistoryXpSentence"], warrior.Name, row.ExperienceGained));
                changed = true;
            }

            foreach (var advance in row.AdvanceRolls)
            {
                if (string.IsNullOrWhiteSpace(advance.ResultText)) continue;

                // Aucun résultat d'Advance (compétence ou stat) ne touche Injuries - ça prêterait à
                // confusion avec une vraie blessure. La vraie compétence choisie est rattachée au
                // guerrier ; les résultats de stat/choix (pas d'équivalent structuré dans le modèle,
                // "no rules engine V1") ne vivent que dans l'Historique de la bande, à appliquer à la
                // main via l'édition du guerrier.
                var text = advance.SelectedSkills.Count > 0
                    ? string.Format(Loc["EndOfGameAdvanceSkillResultText"], advance.SelectedSkillsText)
                    : advance.ResultText;
                sentences.Add(string.Format(Loc["HistoryAdvanceSentence"], warrior.Name, text));

                foreach (var skill in advance.SelectedSkills)
                    await _warbandService.AddWarriorSkillAsync(warrior.Id, skill);
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
                var injury = await GetOrCreateInjuryAsync(row.InjuryResultText);
                await _warbandService.AddWarriorInjuryAsync(warrior.Id, injury);
                sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, row.InjuryResultText));
            }

            // "Blessures multiples" (16/21) : jusqu'à 6 sous-jets supplémentaires sur la table, chacun
            // devient sa propre Injury en plus du texte "Blessures multiples" ci-dessus.
            foreach (var sub in row.MultipleInjuryRolls)
            {
                if (string.IsNullOrWhiteSpace(sub.InjuryResultText)) continue;

                var subInjury = await GetOrCreateInjuryAsync(sub.InjuryResultText);
                await _warbandService.AddWarriorInjuryAsync(warrior.Id, subInjury);
                sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, sub.InjuryResultText));
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

                    var figureInjury = await GetOrCreateInjuryAsync(figure.InjuryResultText);
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

            if (headCountWiped)
                await _warbandService.DeleteWarriorAsync(warrior.Id);
            else if (changed)
                await _warbandService.SaveWarriorAsync(warrior);
        }
    }

    /// <summary>Doit être appelée APRÈS ApplyWarriorOutcomesAsync (voir l'invariant documenté à
    /// l'appel dans EndOfGame ci-dessus) - celle-ci resynchronise warrior.Status depuis row.Status
    /// (Actif/Mort uniquement) pour chaque guerrier, ce qui écraserait silencieusement un statut Sick
    /// posé avant elle (bug trouvé le 2026-08-18).</summary>
    private async Task ApplySicknessLifecycleAsync(EndOfGameDialogViewModel dialogViewModel, List<WarriorRow> previouslySickWarriors)
    {
        // La partie qu'ils manquaient (voir previouslySickWarriors dans EndOfGame) vient d'être
        // enregistrée par CE wizard - ils redeviennent Actifs pour la PROCHAINE fin de partie, jamais
        // celle-ci.
        foreach (var row in previouslySickWarriors)
        {
            row.Warrior.Status = WarriorStatus.Active;
            await _warbandService.SaveWarriorAsync(row.Warrior);
        }

        // Puits en échec (test d'Endurance, voir EndOfGame/ApplyExplorationOutcomeAsync).
        if (dialogViewModel.StatTestSickHero is { } newlySickHero)
        {
            newlySickHero.Warrior.Status = WarriorStatus.Sick;
            await _warbandService.SaveWarriorAsync(newlySickHero.Warrior);
        }
    }
}
