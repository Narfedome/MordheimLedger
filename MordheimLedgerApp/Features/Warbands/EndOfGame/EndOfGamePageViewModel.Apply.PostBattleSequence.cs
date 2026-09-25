using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Pipeline de persistance de Fin de Partie (Apply*Async) - séquence post-bataille
/// (étapes 1-7 du livre : Prisonniers, Vente de pierre magique, Objets rares, Francs-Tireurs/
/// Wanderers, Dramatis Personae, Une Poignée d'Or, Duel). Scindé depuis EndOfGamePageViewModel.
/// Apply.cs (2026-09-23, retour utilisateur - "on a un view model assez énorme est ce qu'on peut
/// le découper") : pur déplacement de méthode(s), aucun changement de comportement. Voir
/// EndOfGamePageViewModel.cs's Finish() pour l'ordre d'appel complet du pipeline.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Étape "Prisonniers ennemis" (EndOfGamePageViewModel.IsCaptivesStep) - une seule fois
    /// pour toute la bande, pas par guerrier (contrairement à ApplyWarriorOutcomesAsync ci-dessus).
    /// Contrepartie de Capturé (61, un DE NOS guerriers capturé PAR l'adversaire, simplifié en rançon/
    /// mort faute de vrai capteur modélisé, voir SERIOUS_INJURIES_STATUS.md) : ici NOUS sommes le
    /// capteur, donc notre propre type de bande (EndOfGamePageViewModel.IsUndeadWarband/
    /// IsPossessedWarband) est une donnée bien réelle - les 4 issues du livre restent toutes
    /// distinguées plutôt que simplifiées.</summary>
    private async Task ApplyCapturedEnemiesAsync(
        IReadOnlyDictionary<string, WarriorArchetype> warriorArchetypesByEnglishName, List<string> sentences)
    {
        if (Warband is null || !HasCapturedEnemies) return;

        foreach (var entry in CapturedEnemies)
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
                    var leader = _allActiveWarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);
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

    /// <summary>Étape "Vente de pierre magique" (livre, étape 4 - EndOfGamePageViewModel.
    /// IsWyrdstoneSaleStep) : absente du wizard si le stock était vide à l'ouverture (voir Steps), donc
    /// ShardsToSell reste à 0 par défaut dans ce cas - rien à faire, pas besoin de re-vérifier ici.</summary>
    private async Task ApplyWyrdstoneSaleAsync(List<string> sentences)
    {
        if (Warband is null || ShardsToSell <= 0) return;

        var gold = WyrdstoneSaleValue;
        Warband.WyrdstoneShards -= ShardsToSell;
        Warband.Treasury += gold;
        await _warbandService.SaveWarbandAsync(Warband);
        sentences.Add(string.Format(Loc["HistoryWyrdstoneSaleSentence"], ShardsToSell, gold));
    }

    /// <summary>Étape "Disponibilité des Vétérans" (livre, étape 5 - EndOfGamePageViewModel.
    /// IsAvailableVeteransStep) : toujours présente dans le wizard, donc VeteranExperienceRoll est
    /// toujours renseigné ici (bloqué par ValidateAvailableVeteransStep sinon). Contrairement à un
    /// premier essai de ce chantier, ce pool n'est PAS persisté sur Warband - retour utilisateur
    /// 2026-08-28, texte du livre à l'appui ("Nouvelles recrues et groupes d'Hommes de main existants",
    /// p.144) : ce jet ne sert qu'à recruter DURANT CETTE séquence (étape 8, pas encore construite), les
    /// points excédentaires sont perdus, rien ne se cumule d'une Fin de Partie à l'autre. Juste une
    /// entrée d'Historique pour trace du jet, aucun état durable.</summary>
    private void ApplyAvailableVeterans(List<string> sentences)
    {
        if (!int.TryParse(VeteranExperienceRoll, out var pool)) return;

        sentences.Add(string.Format(Loc["HistoryAvailableVeteransSentence"], pool));
    }

    /// <summary>Étapes "Objets rares" + "Achat" (livre, étape 6, scindée en 2 - EndOfGamePageViewModel.
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
    /// <summary>Étape Achat/Recrutement (EndOfGamePageViewModel.IsRareItemPurchaseStep) - traite les
    /// deux modes (RareItemSearchEntry.IsSearchingForCharacter) : objets achetés (IsPurchased, inchangé)
    /// ET personnages recrutés (IsRecruited). Un personnage à frais en or (RareItemSearchEntry.
    /// HasHireCost - Johann/Veskit/Marianna, PAS Bertha/None ni Nicodemus/Wyrdstone ni Ulli & Marquand/
    /// Pair) prélève désormais réellement son HireCost sur la trésorerie (2026-09-01, user request),
    /// SAUF s'il a un objet de paiement alternatif ET que le joueur a coché cette option
    /// (IsPayingWithAlternativeItem, ex. Johann/Ombre Cramoisie) - dans ce cas un exemplaire de l'objet
    /// est retiré de l'inventaire de bande à la place (retrait total de la pile, voir DramatisPersona.
    /// AlternativePaymentItemId's own doc - pas de mécanisme de pile partielle dans cette app). Un
    /// personnage sans frais (Bertha) reste inséré gratuitement comme avant.</summary>
    private async Task ApplyRareItemSearchAsync(List<EquipmentItem> localizedEquipment, List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var entry in RareItemSearchEntries.Where(e => e.IsPurchased))
        {
            // L'objet entre dans la réserve via ReserveGains (voir BuildReserve), écrite par ApplyReserveAsync.
            var item = entry.SelectedItem!;
            Warband.Treasury -= entry.EffectiveCost!.Value;
            await _warbandService.SaveWarbandAsync(Warband);
            var displayName = entry.SelectedMaterial is { } material ? $"{item.Name} ({material.Abbreviation})" : item.Name;
            sentences.Add(string.Format(Loc["HistoryRareItemFoundSentence"], entry.HeroName, displayName));
        }

        // Même sélection que la réserve (BuildReserve retire déjà ces lignes) - ici seulement l'historique.
        var alternativePaymentLines = AlternativePaymentLines();

        foreach (var entry in RareItemSearchEntries.Where(e => e.IsRecruited))
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
            else if (alternativePaymentLines.TryGetValue(entry, out var paymentStash))
            {
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
    /// Scope volontairement WarriorRows (le roster Actif figé à L'OUVERTURE de ce wizard)
    /// plutôt que le roster courant : un Personnage spécial fraîchement recruté PENDANT cette même Fin de
    /// Partie (juste au-dessus, ApplyRareItemSearchAsync) n'apparaît jamais dans WarriorRows - il reste
    /// donc pour au moins la prochaine bataille, comme voulu ("A request for Bertha to aid the warband
    /// must be made for EACH battle" implique qu'une fois recrutée elle participe à celle-ci, puis repart
    /// à LA FIN de celle-ci, pas immédiatement). Mort/Retraité exclus : déjà des états terminaux gérés
    /// ailleurs, rien à faire de plus pour eux ici.</summary>
    private async Task ApplyWandererDeparturesAsync(List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var row in WarriorRows)
        {
            var warrior = row.Warrior;
            if (!warrior.IsDramatisPersona || warrior.Status is WarriorStatus.Dead or WarriorStatus.Retired) continue;

            var persona = _dramatisPersonaCatalog.FirstOrDefault(p => p.Id == warrior.DramatisPersonaId);
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
            // EndOfGamePageViewModel.DramatisPersonae.cs's own doc - seul le camp qui gagne le contrôle
            // paie, jamais celui qui les a recrutés).
            sentences.Add(string.Format(warrior.IsHostileThisBattle ? Loc["HistoryDramatisPersonaCorruptedSentence"] : Loc["HistoryDramatisPersonaDepartedSentence"], warrior.Name));
        }
    }

    /// <summary>Étape "Dramatis Personae" (EndOfGamePageViewModel.IsDramatisPersonaeStep) - règle la
    /// solde de chaque Dramatis Persona à frais récurrents déjà engagé : en or (Johann/Veskit/Marianna)
    /// OU en pierre magique (Nicodemus, DramatisPersonaUpkeepEntry.IsWyrdstoneFee - 2026-09-01, "on a
    /// qu'a brancher la wyrstone à son paiement"). Même Payer/Renvoyer que ApplyHiredSwordUpkeepAsync
    /// ci-dessous, mais sans équivalent d'IsPrepaidFree (rien comme "Une Faveur Rendue" n'existe pour un
    /// Dramatis Persona) et sans recrutement combiné (un Dramatis Persona se recrute via
    /// ApplyRareItemSearchAsync, pas ici). Solde refusée/impayée = il quitte la bande pour de bon - même
    /// traitement que le refus d'un Franc-Tireur (retrait complet, équipement/compétences avec) : une
    /// future recherche recréera une fiche neuve. Traite aussi, en fin de méthode, l'éventuel paiement
    /// "Une Poignée d'Or" (Corruption ou Rétention de Marquand &amp; Ulli - voir EndOfGamePageViewModel.
    /// WantsToRecordPairCorruption/PairRetentionAmount, saisis sur l'étape "Une Poignée d'Or" dédiée
    /// depuis le 2026-09-04, voir EndOfGamePageViewModel.PairEngagement.cs), appliqué ici avec le reste
    /// de la comptabilité Dramatis Personae, sujet indépendant de l'étape qui l'affiche.</summary>
    private async Task ApplyDramatisPersonaUpkeepAsync(List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var entry in DramatisPersonaUpkeepEntries)
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
        // Marquand (voir EndOfGamePageViewModel.ShowPairCorruptionOption) - elle vient de gagner leur
        // contrôle par corruption réussie, donc paie SA PROPRE trésorerie (jamais celle qui les a
        // recrutés à l'origine, voir la SpecialRule "A Fistful of Crowns"). Ne recrute PAS réellement la
        // paire ici (aucune ligne Warrior créée) - purement le paiement, cohérent avec "sur un autre
        // appareil pas celle présente en local" (retour utilisateur) : cette bande n'a pas forcément
        // accès aux fiches Marquand/Ulli elles-mêmes.
        //
        // !IsPairCorruptionUnaffordable (2026-09-04, retour utilisateur - vrai bug trouvé en creusant un
        // signalement sur les valeurs de la réserve) : payer en or ne se fait QUE si la bande peut réellement
        // payer - "instead" dans "if the controlling warband can't pay a successful bribe... the pair
        // INSTEAD seizes an equal value of equipment" est une lecture stricte confirmée par l'utilisateur
        // (aucun or ne sort du tout dans le cas impayable, Céder du matériel/Duel couvre l'INTÉGRALITÉ du
        // montant) - sans ce garde-fou, la trésorerie était débitée du montant COMPLET ICI, EN PLUS de la
        // saisie d'équipement/du duel appliqués séparément (ApplyPairEquipmentSeizureIfNeededAsync/
        // ApplyPairDuelIfNeededAsync juste en dessous) : double paiement (or perdu ET objets/meneur
        // perdus pour la même dette).
        if (WantsToRecordPairCorruption && !IsPairCorruptionUnaffordable
            && int.TryParse(PairCorruptionAmount, out var corruptionAmount))
        {
            Warband.Treasury -= corruptionAmount;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryPairCorruptionPaidSentence"], corruptionAmount));
        }

        // "C'est l'heure de payer !" (2026-09-01) - cas symétrique pour une bande qui possède DÉJÀ Ulli &
        // Marquand (EndOfGamePageViewModel.ShowPairRetentionOption) : ce qu'elle a dû payer pour les
        // GARDER après une tentative adverse ("seul le camp qui obtient OU GARDE le contrôle paie", voir
        // la SpecialRule "A Fistful of Crowns"). 0/vide (pas de tentative) ne fait rien - PairRetentionAmount
        // reste alors vide, int.TryParse échoue, ce bloc est un no-op. Appliqué en tout dernier (après la
        // boucle Payer/Renvoyer ci-dessus) - "à la fin de tous les décomptes", retour utilisateur. Même
        // garde-fou !IsPairRetentionUnaffordable que la Corruption ci-dessus (2026-09-04) - double
        // paiement sinon.
        if (!IsPairRetentionUnaffordable
            && int.TryParse(PairRetentionAmount, out var retentionAmount) && retentionAmount > 0)
        {
            Warband.Treasury -= retentionAmount;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryPairRetentionPaidSentence"], PairRetentionLabel, retentionAmount));
        }
    }

    /// <summary>"Où est l'Argent ?" - "Céder du matériel" (2026-09-01, retour utilisateur - remplace
    /// l'ancien simple rappel textuel) : les objets sélectionnés automatiquement par
    /// EndOfGamePageViewModel.SeizedEquipmentItems (voir sa propre doc) sont retirés de la réserve finale
    /// par BuildReserve (SeizedReserveSourceIds - une ligne = une pile entière) et donc de la base par
    /// ApplyReserveAsync ; ici, seulement la phrase d'historique.</summary>
    private Task ApplyPairEquipmentSeizureIfNeededAsync(List<string> sentences)
    {
        var isUnaffordable = IsPairCorruptionUnaffordable || IsPairRetentionUnaffordable;
        if (Warband is null || !isUnaffordable || !WantsEquipmentSeizure || SeizedEquipmentItems.Count == 0)
            return Task.CompletedTask;

        var pairLabel = IsPairCorruptionUnaffordable ? PairCorruptionLabel : PairRetentionLabel;
        sentences.Add(string.Format(Loc["HistoryPairEquipmentSeizedSentence"], pairLabel, SeizedEquipmentTotalValue));
        return Task.CompletedTask;
    }

    /// <summary>"Où est l'Argent ?" (2026-09-01) - repli si le montant à payer dépasserait le solde
    /// prévisionnel de la bande, dans l'un ou l'autre des deux cas mutuellement exclusifs (une bande ne
    /// possède jamais Ulli &amp; Marquand ET tente de les débaucher à la fois) : IsPairCorruptionUnaffordable
    /// (ne les possède pas) ou IsPairRetentionUnaffordable (les possède déjà) - toutes deux dans
    /// EndOfGamePageViewModel.PairEngagement.cs. Voir ApplyPairEquipmentSeizureIfNeededAsync juste
    /// au-dessus pour l'autre choix possible
    /// ("Céder du matériel"). Victoire = rien de plus ; défaite = mort automatique du meneur (2026-09-03,
    /// retour utilisateur - "en cas de défaite, le chef de bande est forcément mort"), pas de jet sur la
    /// table des Blessures Graves contrairement à une première version qui s'inspirait par erreur de
    /// Vendu aux Fosses (WonPitFight/SoldToPitsRerollRoll) - absent du texte de cette règle-ci.</summary>
    private async Task ApplyPairDuelIfNeededAsync(List<string> sentences)
    {
        var isUnaffordable = IsPairCorruptionUnaffordable || IsPairRetentionUnaffordable;
        if (Warband is null || !isUnaffordable || !WantsDuel) return;

        var leaderRow = WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);
        if (leaderRow is null) return;
        var warrior = leaderRow.Warrior;

        if (WonDuel)
        {
            sentences.Add(string.Format(Loc["HistoryPairDuelWonSentence"], warrior.Name));
            return;
        }

        warrior.Status = WarriorStatus.Dead;
        sentences.Add(string.Format(Loc["HistoryPairDuelLostSentence"], warrior.Name));
        await _warbandService.SaveWarriorAsync(warrior);
    }

    /// <summary>Étape "Francs-Tireurs" (EndOfGamePageViewModel.IsHiredSwordsStep) - règle la solde de
    /// chaque Franc-Tireur déjà engagé (payée/refusée/déjà prépayée par "Une Faveur Rendue") ET recrute
    /// le nouveau éventuellement choisi à la même étape. Un Franc-Tireur déjà Mort/Retraité (décompté
    /// plus haut par ApplyWarriorOutcomesAsync) n'a plus d'entrée d'upkeep ici (WarriorRows filtre déjà
    /// les guerriers Actifs uniquement) - rien à facturer pour lui.</summary>
    private async Task ApplyHiredSwordUpkeepAsync(List<string> sentences)
    {
        if (Warband is null) return;

        foreach (var entry in HiredSwordUpkeepEntries)
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

        if (SelectedNewHiredSword is { } hiredSword)
        {
            var localizedEquipment = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);
            var startingEquipment = localizedEquipment.Where(e => hiredSword.StartingEquipmentIds.Contains(e.Id)).ToList();
            var name = NewHiredSwordName.Trim();

            await _warbandService.RecruitHiredSwordAsync(Warband.Id, hiredSword, name, startingEquipment);
            Warband.Treasury -= hiredSword.HireCost;
            await _warbandService.SaveWarbandAsync(Warband);
            sentences.Add(string.Format(Loc["HistoryHiredSwordHiredSentence"], name, hiredSword.Name));
        }
    }

}
