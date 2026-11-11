using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Pipeline de persistance de Fin de Partie (Apply*Async) - statut/blessures/maladie de
/// chaque guerrier. Scindé depuis EndOfGamePageViewModel.Apply.cs (2026-09-23, retour utilisateur -
/// "on a un view model assez énorme est ce qu'on peut le découper") : pur déplacement de
/// méthode(s), aucun changement de comportement. Voir EndOfGamePageViewModel.cs's Finish() pour
/// l'ordre d'appel complet du pipeline.</summary>
public partial class EndOfGamePageViewModel
{
    private async Task ApplyWarriorOutcomesAsync(string language, List<string> sentences)
    {
        if (Warband is null) return;

        // Écritures des blessures (catalogue, effets, capture, combats de gladiateur, morts, suppression d'un
        // groupe vide) : InjuryService (Core, testé). Le wizard garde l'ordre des jets et l'Historique.
        var injuries = new InjuryService(_warbandService, _libraryService, language);
        Task<Injury> GetOrCreateInjuryAsync(int roll, bool isHero, string fallbackText, int? branchSubRoll = null) =>
            injuries.ResolveInjuryAsync(roll, isHero, fallbackText, branchSubRoll);

        foreach (var row in WarriorRows)
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

                var entryInjury = await GetOrCreateInjuryAsync(hasRoll ? roll : -1, warrior.IsHero, entry.ResolvedInjuryText, entryBranchSubRoll);
                entryChanged |= await injuries.RecordInjuryAsync(warrior, entryInjury, entryOutcome);
                // Relance d'un combat de gladiateur perdu : pas une nouvelle mise hors de combat, phrase dédiée.
                sentences.Add(string.Format(Loc[entry.IsPitFightReroll ? "HistorySoldToPitsRerollSentence" : "HistoryInjurySentence"],
                    warrior.Name, entry.ResolvedInjuryText));

                if (entryOutcome?.Kind == SeriousInjuryEffectKind.ForcedRetirement)
                    sentences.Add(string.Format(Loc["HistoryForcedRetirementSentence"], warrior.Name));
                if (hasRoll && roll == 31) alreadyBlindedInOneEye = true;

                if (entry.HasHatredTarget)
                {
                    await injuries.AddHatredAsync(warrior, entry.HatredTargetWarbandArchetypeId, entry.HatredTargetFreeText);
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
                    injuries.WinPitFight(Warband, warrior);
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
                    await injuries.CaptureAsync(Warband, warrior, rerollRansom);
                    sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, rerollRansom));
                }
                else if (reroll.ShowCapturedChoice)
                {
                    await injuries.CaptureAsync(Warband, warrior, null);
                    sentences.Add(string.Format(Loc["HistoryCapturedLostSentence"], warrior.Name));
                }
                else
                {
                    await injuries.LosePitFightAsync(warrior);
                    sentences.Add(string.Format(Loc["HistorySoldToPitsLostSentence"], warrior.Name));
                }

                return true;
            }

            // Un guerrier mort cette bataille (voir WarriorOutcomeRow.IsDead) ne gagne plus rien : XP ou jets de
            // Progression saisis AVANT qu'un retour à l'étape Blessure ne le fasse mourir sont ignorés.
            if (row.ExperienceGained != 0 && !row.IsDead)
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
                var text = AdvanceText(advance);
                sentences.Add(string.Format(Loc["HistoryAdvanceSentence"], target.Name, text));
                await Advancement.ApplyAdvanceAsync(target, ToAdvanceResult(advance));
            }

            // AdvanceRolls (palier franchi par l'XP de bataille normale) + ExplorationAdvanceRolls
            // (palier atteint uniquement grâce à l'XP accordée par la table d'Exploration - voir
            // WarriorOutcomeRow.ExplorationMilestoneCount) : même application pour les deux, aucune
            // distinction nécessaire une fois les jets faits.
            foreach (var advance in row.IsDead ? [] : row.AdvanceRolls.Concat(row.ExplorationAdvanceRolls))
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
                    // XP de la partie, copie de l'équipement du groupe, jet immédiat du Héros, modèle retiré du
                    // groupe et jet du reste du groupe : AdvancementService (Core, couvert par des tests).
                    // XP répartie par l'Exploration et paliers de Héros qu'elle lui fait franchir (ligne du promu,
                    // voir EndOfGamePageViewModel.Promotion) : appliqués ici, le Héros n'existant en base qu'à partir
                    // de maintenant.
                    var remainderRoll = advance.NestedHenchmanRoll;
                    var promotedRow = PromotedHeroRowFor(advance, promoted);
                    await Advancement.PromoteHenchmanAsync(warrior, promoted, ToAdvanceResult(heroRoll),
                        remainderRoll is null ? null : ToAdvanceResult(remainderRoll),
                        promotedRow?.DistributedExplorationExperience ?? 0);
                    sentences.Add(string.Format(Loc["HistoryPromotionSentence"], warrior.Name, promoted.Name));
                    sentences.Add(string.Format(Loc["HistoryAdvanceSentence"], promoted.Name, AdvanceText(heroRoll)));
                    if (remainderRoll is not null)
                        sentences.Add(string.Format(Loc["HistoryAdvanceSentence"], warrior.Name, AdvanceText(remainderRoll)));
                    if (promotedRow is not null && promotedRow.HasExplorationMilestone)
                    {
                        foreach (var explorationAdvance in promotedRow.ExplorationAdvanceRolls.Where(a => !string.IsNullOrWhiteSpace(a.ResultText)))
                            await ApplyResolvedAdvanceAsync(promoted, explorationAdvance);
                        await _warbandService.SaveWarriorAsync(promoted);
                    }
                    changed = true;
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
                var injury = await GetOrCreateInjuryAsync(hasMainRoll ? mainRoll : -1, warrior.IsHero, row.ResolvedInjuryText, branchSubRoll);
                changed |= await injuries.RecordInjuryAsync(warrior, injury, outcome);
                sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, row.ResolvedInjuryText));
                if (outcome?.Kind == SeriousInjuryEffectKind.ForcedRetirement)
                    sentences.Add(string.Format(Loc["HistoryForcedRetirementSentence"], warrior.Name));
                if (hasMainRoll && mainRoll == 31) alreadyBlindedInOneEye = true;
            }

            // Rancune (56) : la cible choisie par le joueur (EndOfGamePageViewModel.Injury) devient une
            // WarriorHatred - voir Models.WarriorHatred pour pourquoi ce n'est pas une Injury de plus (2
            // sortes de cible possibles, pas un simple texte catalogue).
            if (row.HasHatredTarget)
            {
                await injuries.AddHatredAsync(warrior, row.HatredTargetWarbandArchetypeId, row.HatredTargetFreeText);
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
                    await injuries.CaptureAsync(Warband, warrior, ransomAmount);
                    sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, ransomAmount));
                }
                else
                {
                    await injuries.CaptureAsync(Warband, warrior, null);
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
                    injuries.WinPitFight(Warband, warrior);
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
                    changed |= await injuries.RecordInjuryAsync(warrior, rerollInjury, rerollOutcome);
                    sentences.Add(string.Format(Loc["HistoryInjurySentence"], warrior.Name, reroll.InjuryResultText));
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
                        await injuries.CaptureAsync(Warband, warrior, rerollRansom);
                        sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, rerollRansom));
                    }
                    else if (reroll.ShowCapturedChoice)
                    {
                        await injuries.CaptureAsync(Warband, warrior, null);
                        sentences.Add(string.Format(Loc["HistoryCapturedLostSentence"], warrior.Name));
                    }
                    else
                    {
                        await injuries.LosePitFightAsync(warrior);
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
                        await injuries.CaptureAsync(Warband, warrior, subRansomAmount);
                        sentences.Add(string.Format(Loc["HistoryCapturedRansomedSentence"], warrior.Name, subRansomAmount));
                    }
                    else
                    {
                        await injuries.CaptureAsync(Warband, warrior, null);
                        sentences.Add(string.Format(Loc["HistoryCapturedLostSentence"], warrior.Name));
                    }
                    changed = true;
                }
                // Vendu aux Fosses (65) : même principe, pour un sous-jet qui tombe lui-même sur 65 - sa
                // propre étape de combat de gladiateur (EndOfGamePageViewModel.Steps, une étape par
                // occurrence, 2026-09-04 retour utilisateur).
                else if (sub.ShowSoldToThePits)
                {
                    changed |= await ApplyPitFightEntryAsync(sub);
                }
            }

            // Un jet D6 par figurine hors de combat dans ce groupe d'Hommes de main (règle confirmée
            // avec l'utilisateur, 2026-08-17 - pas un seul jet pour tout le groupe, voir
            // EndOfGamePageViewModel.WarriorOutcomeRow.FigureInjuryRolls). Chaque résultat devient sa
            // propre Injury comme pour un Héros ; celles qui tombent sur "Mort" décrémentent HeadCount
            // d'autant plutôt que de faire basculer tout le groupe à WarriorStatus.Dead - le groupe ne
            // passe Mort (via suppression, voir plus bas) que si HeadCount tombe à 0.
            var headCountWiped = false;
            if (row.FigureInjuryRolls.Count > 0)
            {
                var figures = new List<(Injury, bool)>();
                foreach (var figure in row.FigureInjuryRolls)
                {
                    if (string.IsNullOrWhiteSpace(figure.InjuryResultText)) continue;
                    var hasFigureRoll = int.TryParse(figure.ManualRoll, out var figureRoll);
                    figures.Add((await GetOrCreateInjuryAsync(hasFigureRoll ? figureRoll : -1, warrior.IsHero, figure.InjuryResultText), figure.IsDeath));
                }
                var deaths = figures.Count(f => f.Item2);
                headCountWiped = await injuries.RecordHenchmanInjuriesAsync(warrior, figures);

                if (deaths > 0)
                {
                    changed = true;
                    sentences.Add(headCountWiped
                        ? string.Format(Loc["HistoryHenchmanWipedSentence"], warrior.Name)
                        : string.Format(Loc["HistoryHenchmanDeathSentence"], warrior.Name, deaths, warrior.HeadCount));
                }
            }

            // Groupe vide supprimé (morts par figurine, ou dernier membre promu Héros), sinon sauvegardé s'il a changé.
            await injuries.FinishWarriorAsync(warrior, changed);
        }
    }

    /// <summary>Application des progressions en base (Core, testée) - sans état, recréée à la demande.</summary>
    private AdvancementService Advancement => new(_warbandService);

    private static AdvanceResult ToAdvanceResult(AdvanceRollEntry advance) =>
        new(advance.SelectedSkills.ToList(), advance.HasSpellSelected ? advance.SelectedSpell : null, advance.ResolvedField);

    /// <summary>Résultat d'un jet pour l'Historique (compétence, sort, caractéristique +1, sinon texte de la table).</summary>
    private string AdvanceText(AdvanceRollEntry advance) =>
        advance.SelectedSkills.Count > 0 ? string.Format(Loc["EndOfGameAdvanceSkillResultText"], advance.SelectedSkillsText)
        : advance.HasSpellSelected ? string.Format(Loc["EndOfGameAdvanceSpellResultText"], advance.SelectedSpell!.Name)
        : advance.ResolvedField is not null ? $"{advance.ResolvedFieldLabel} +1"
        : advance.ResultText;

    /// <summary>Doit être appelée APRÈS ApplyWarriorOutcomesAsync (voir l'invariant documenté à
    /// l'appel dans EndOfGame ci-dessus) - celle-ci resynchronise warrior.Status depuis row.Status
    /// (Actif/Mort uniquement) pour chaque guerrier, ce qui écraserait silencieusement un statut Sick/
    /// Mort posé avant elle par un mécanisme d'Exploration (bug trouvé le 2026-08-18 pour Sick ; même
    /// principe appliqué à La Fosse). Regroupe tous les statuts posés hors du flux Blessure normal, pas
    /// seulement la Maladie malgré le nom.</summary>
    private async Task ApplySicknessLifecycleAsync(List<WarriorRow> previouslySickWarriors)
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
        if (StatTestSickHero is { } newlySickHero)
        {
            newlySickHero.Warrior.Status = WarriorStatus.Sick;
            newlySickHero.Warrior.SickGamesRemaining = 1;
            await _warbandService.SaveWarriorAsync(newlySickHero.Warrior);
        }

        // La Fosse, sous-jet 1 (Héros envoyé dévoré, voir EndOfGame/ApplyExplorationOutcomeAsync).
        if (PitDevouredHero is { } devouredHero)
        {
            devouredHero.Warrior.Status = WarriorStatus.Dead;
            await _warbandService.SaveWarriorAsync(devouredHero.Warrior);
        }
    }

}
