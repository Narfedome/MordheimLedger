using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Récapitulatif" (pas une étape du livre - relecture avant d'Enregistrer) - étoffée le
/// 2026-09-22 (retour utilisateur : "c'est censé être un récap de toutes les étapes qu'on a fait") pour
/// couvrir toutes les décisions prises ailleurs dans ce même wizard, pas seulement Résultat/Exploration/
/// Prisonniers/le résumé par guerrier (déjà dans EndOfGamePage.xaml avant cette passe). Chaque section
/// ci-dessous réutilise TELLES QUELLES des données déjà exposées par l'étape correspondante (aucune
/// nouvelle logique de calcul, sauf ProjectedWarbandRating) - un simple aperçu en lecture seule, jamais
/// une source de vérité pour Terminer.
///
/// Réallouer l'équipement (dernière étape ajoutée) reste HORS de ce récap : c'est la seule qui ne garde
/// aucune trace de ses déplacements (juste une mutation en mémoire, voir EndOfGamePageViewModel.
/// Reallocation.cs), retour utilisateur explicite - "on peut si besoin le faire plus tard".</summary>
public partial class EndOfGamePageViewModel
{
    public bool HasWyrdstoneSaleRecap => ShardsToSell > 0;

    /// <summary>Butin de l'Exploration (recette Android du 2026-11-05 : pierres magiques et objets trouvés
    /// absents du récap) - mêmes sources que ce qu'ApplyExplorationOutcomeAsync/ApplyReserveAsync écrivent
    /// à Terminer : barème des dés + pierre localisée + lignes Trésor Caché/Bande Massacrée, or du résultat
    /// + de ces lignes, objets de PendingExplorationItems. Le scénario a sa propre section.</summary>
    private int ExplorationLootWyrdstone => BaselineWyrdstoneShardsFound
        + (IsExplorationWyrdstone && int.TryParse(ExplorationWyrdstoneAmount, out var located) ? located : 0)
        + IndependentOutcomeWyrdstoneShards;

    private int ExplorationLootGold =>
        (ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold && int.TryParse(ExplorationGoldAmount, out var gold) ? gold : 0)
        + IndependentOutcomeGold;

    public bool HasExplorationLootRecap => ExplorationLootWyrdstone > 0 || ExplorationLootGold != 0 || PendingExplorationItems().Any();

    public IEnumerable<string> ExplorationLootRecapLines
    {
        get
        {
            if (ExplorationLootWyrdstone > 0)
                yield return string.Format(Loc["EndOfGameRecapLootWyrdstoneFormat"], ExplorationLootWyrdstone);
            if (ExplorationLootGold != 0)
                yield return string.Format(Loc["EndOfGameRecapLootGoldFormat"], ExplorationLootGold);
            foreach (var found in PendingExplorationItems())
            {
                var name = found.MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $"{found.Item.Name} ({abbr})" : found.Item.Name;
                yield return string.Format(Loc["EndOfGameRecapHenchmanGroupFormat"], name, found.Quantity);
            }
        }
    }

    /// <summary>La vue d'étape est mise en cache : relue à chaque arrivée sur le récap, les saisies des
    /// autres étapes ont pu changer depuis.</summary>
    private void NotifyRecapChanged()
    {
        foreach (var name in new[]
        {
            nameof(HasExplorationLootRecap), nameof(ExplorationLootRecapLines), nameof(HasWyrdstoneSaleRecap),
            nameof(AvailableVeteransRecapDisplay), nameof(HasRareItemsRecap), nameof(RareItemsRecapLines),
            nameof(HasDramatisPersonaeRecap), nameof(DramatisPersonaeRecapLines), nameof(PairEngagementRecapDisplay),
            nameof(HasHiredSwordsRecap), nameof(HiredSwordsRecapLines), nameof(HasDismissedWarriorsRecap),
            nameof(DismissedWarriorsRecapLines), nameof(HasEquipmentTradingRecap), nameof(EquipmentTradingPurchasedRecapLines),
            nameof(EquipmentTradingSoldRecapLines), nameof(HasRecruitmentRecap), nameof(RecruitedHeroesRecapLines),
            nameof(RecruitedHenchmenRecapLines), nameof(VeteranTopUpsRecapLines), nameof(ExplorationJoinRecapLines),
            nameof(ProjectedWarbandRatingDisplay), nameof(RecapWarriorRows),
        })
            OnPropertyChanged(name);
        foreach (var row in AllOutcomeRows) row.NotifySummaryChanged();
    }

    /// <summary>Toujours affiché (le jet a lieu à chaque Fin de Partie, que la bande recrute ou non des
    /// vétérans - voir EndOfGamePageViewModel.Recruitment.cs's RemainingVeteranBudget's own doc).</summary>
    public string AvailableVeteransRecapDisplay => string.Format(Loc["EndOfGameRecapVeteransFormat"], VeteranExperienceRoll);

    public bool HasRareItemsRecap => RareItemsWithResults.Count > 0;

    public IEnumerable<string> RareItemsRecapLines => RareItemsWithResults.Select(e => e.IsSearchingForCharacter
        ? string.Format(Loc["EndOfGameRecapRareCharacterFormat"], e.HeroName, e.SelectedCharacterDisplayName)
        : string.Format(Loc["EndOfGameRecapRareItemFormat"], e.HeroName, e.SelectedItem?.Name,
            (e.EffectiveCost ?? 0) - HaggleSavingsAt(RareSpotId(RareItemSearchEntries.IndexOf(e)))));

    public bool HasDramatisPersonaeRecap => LivingDramatisPersonaUpkeepEntries.Any(e => e.WillPay.HasValue);

    public IEnumerable<string> DramatisPersonaeRecapLines => LivingDramatisPersonaUpkeepEntries.Where(e => e.WillPay.HasValue)
        .Select(e => string.Format(e.WillPay == true ? Loc["EndOfGameRecapPaidFormat"] : Loc["EndOfGameRecapDismissedFormat"], e.DisplayName, e.UpkeepCostDisplay));

    /// <summary>Corruption/Rétention/Duel sont mutuellement exclusifs (voir EndOfGamePageViewModel.
    /// PairEngagement.cs/.PairDuel.cs) - au plus une des trois branches produit une ligne.</summary>
    public string? PairEngagementRecapDisplay
    {
        get
        {
            if (WantsDuel) return string.Format(Loc["EndOfGameRecapPairDuelFormat"], WonDuel ? Loc["EndOfGamePairDuelWonLabel"] : Loc["EndOfGameDeadLabel"]);
            if (ShowPairCorruptionOption && WantsToRecordPairCorruption && !string.IsNullOrEmpty(PairCorruptionAmount))
                return string.Format(Loc["EndOfGameRecapPairCorruptionFormat"], PairCorruptionLabel, PairCorruptionAmount);
            if (ShowPairRetentionOption && !string.IsNullOrEmpty(PairRetentionAmount) && PairRetentionAmount != "0")
                return string.Format(Loc["EndOfGameRecapPairRetentionFormat"], PairRetentionLabel, PairRetentionAmount);
            return null;
        }
    }

    public bool HasHiredSwordsRecap => LivingHiredSwordUpkeepEntries.Any(e => e.WillPay.HasValue || e.IsPrepaidFree) || SelectedNewHiredSword is not null
        || FreeHiredSword is not null;

    /// <summary>Franc-Tireur offert par l'Exploration (Une Faveur Rendue) - recruté à Terminer par
    /// ApplyExplorationOutcomeAsync, absent du récap et de la Valeur projetée jusqu'à la recette du 2026-11-11.</summary>
    private HiredSword? FreeHiredSword => ResolvedExplorationOutcome?.GrantsFreeHiredSword == true ? SelectedFreeHiredSword : null;

    public IEnumerable<string> HiredSwordsRecapLines
    {
        get
        {
            foreach (var entry in LivingHiredSwordUpkeepEntries.Where(e => e.WillPay.HasValue || e.IsPrepaidFree))
                yield return entry.IsPrepaidFree
                    ? string.Format(Loc["EndOfGameRecapHiredSwordPrepaidFormat"], entry.DisplayName)
                    : string.Format(entry.WillPay == true ? Loc["EndOfGameRecapPaidFormat"] : Loc["EndOfGameRecapDismissedFormat"], entry.DisplayName, entry.UpkeepCost);
            if (SelectedNewHiredSword is { } hiredSword)
                yield return string.Format(Loc["EndOfGameRecapHiredSwordHiredFormat"], hiredSword.Name, hiredSword.HireCost);
            if (FreeHiredSword is { } freeHiredSword)
                yield return string.Format(Loc["EndOfGameRecapHiredSwordFreeFormat"], freeHiredSword.Name);
        }
    }

    public bool HasDismissedWarriorsRecap => WarriorRows.Any(r => r.EffectiveDismissCount > 0);

    public IEnumerable<string> DismissedWarriorsRecapLines => WarriorRows.Where(r => r.EffectiveDismissCount > 0)
        .Select(r => string.Format(Loc["EndOfGameRecapDismissedWarriorFormat"], r.Name, r.DismissLabel));

    public bool HasEquipmentTradingRecap => PurchasedReserveItems.Count > 0 || PendingSales.Count > 0;

    public IEnumerable<string> EquipmentTradingPurchasedRecapLines => PurchasedReserveItems.Select(p => p.Name);

    public IEnumerable<string> EquipmentTradingSoldRecapLines => PendingSales.Select(c => c.SoldLabel);

    public bool HasRecruitmentRecap => RecruitedHeroRows.Any() || RecruitedHenchmanRows.Any() || LivingHenchmanTopUps.Any(t => t.AddCount > 0)
        || ExplorationJoinedHeadCount > 0;

    /// <summary>Hommes de main gagnés à l'Exploration (Zombies offerts, prisonnier équipé) - absents du récap
    /// jusqu'au 2026-10-24 alors qu'ApplyExplorationAsync les ajoute bien à l'enregistrement.</summary>
    public IEnumerable<string> ExplorationJoinRecapLines
    {
        get
        {
            if (ExplorationFreeHenchmanCount > 0)
                yield return string.Format(Loc["EndOfGameRecapExplorationJoinFormat"], ResolvedFreeHenchman!.Name, ExplorationFreeHenchmanCount);
            if (ExplorationEquippedHenchmanGroup is { } group)
                yield return string.Format(Loc["EndOfGameRecapExplorationJoinFormat"], group.Name, 1);
        }
    }

    public IEnumerable<string> RecruitedHeroesRecapLines => RecruitedHeroRows.SelectMany(r => r.NameSlots).Select(s => s.DisplayLabel);

    public IEnumerable<string> RecruitedHenchmenRecapLines => RecruitedHenchmanRows.SelectMany(r => r.HenchmanGroupDrafts)
        .Select(g => string.Format(Loc["EndOfGameRecapHenchmanGroupFormat"], g.Name, g.Count));

    public IEnumerable<string> VeteranTopUpsRecapLines => LivingHenchmanTopUps.Where(t => t.AddCount > 0)
        .Select(t => string.Format(Loc["EndOfGameRecapVeteranTopUpFormat"], t.Name, t.AddCount));

    /// <summary>Aperçu de la Valeur de Bande APRÈS cette Fin de Partie (livre, étape 10 - "Update your
    /// warband rating") - mirroring Core.Rules.WarbandRatingRules.WarriorContribution (déjà utilisé par
    /// WarbandDetailViewModel.Rating/WarbandService.GetWarbandRatingAsync, aucune nouvelle règle), calculé
    /// côté wizard AVANT Terminer pour que le joueur voie le chiffre avant de valider - la vraie Valeur
    /// (exacte) reste celle recalculée par WarbandDetailViewModel.LoadAsync après Enregistrer, jamais
    /// remplacée par cet aperçu. Compte l'effectif survivant de cette bataille (SurvivingHeadCount).</summary>
    public int ProjectedWarbandRating
    {
        get
        {
            var total = 0;
            foreach (var row in WarriorRows.Where(r => !r.IsDead && !r.Warrior.IsHostileThisBattle))
            {
                var headCount = row.SurvivingHeadCount - row.EffectiveDismissCount;
                if (headCount <= 0) continue;
                var experience = row.Warrior.Experience + row.ExperienceGained + row.SeriousInjuryBonusExperience + row.ExplorationBonusExperience;
                total += WarbandRatingRules.WarriorContribution(row.Warrior.IsLargeCreature, experience, headCount, row.Warrior.HiredSwordBaseRating, row.Warrior.DramatisPersonaRatingBonus);
            }
            foreach (var topUp in LivingHenchmanTopUps.Where(t => t.AddCount > 0))
                total += WarbandRatingRules.WarriorContribution(false, topUp.GroupExperience, topUp.AddCount, null);
            foreach (var slot in RecruitedHeroRows.SelectMany(r => r.NameSlots))
                total += WarbandRatingRules.WarriorContribution(slot.Row.Archetype.IsLargeCreature, slot.Row.Archetype.StartingExperience, 1, null);
            foreach (var group in RecruitedHenchmanRows.SelectMany(r => r.HenchmanGroupDrafts))
                total += WarbandRatingRules.WarriorContribution(group.Row.Archetype.IsLargeCreature, group.Row.Archetype.StartingExperience, group.Count, null);
            if (SelectedNewHiredSword is { } hiredSword)
                total += WarbandRatingRules.WarriorContribution(false, 0, 1, hiredSword.BaseRating);
            if (FreeHiredSword is { } freeHiredSword)
                total += WarbandRatingRules.WarriorContribution(false, 0, 1, freeHiredSword.BaseRating);
            foreach (var entry in RareItemsWithResults.Where(e => e.IsRecruited))
                total += WarbandRatingRules.WarriorContribution(false, 0, 1, null, entry.SelectedCharacter!.RatingBonus);
            // Exploration : un Zombie offert rejoint un groupe existant du même type s'il y en a un (et en prend
            // l'expérience, voir ApplyExplorationAsync), sinon démarre à l'XP de départ de son archétype ; le
            // prisonnier équipé prend celle du groupe qu'il rejoint.
            if (ExplorationFreeHenchmanCount > 0 && ResolvedFreeHenchman is { } freeHenchman)
            {
                var joinedGroup = LivingWarriorRows.FirstOrDefault(r => !r.Warrior.IsHero && r.Warrior.WarriorArchetypeId == freeHenchman.Id);
                var experience = joinedGroup is null ? freeHenchman.StartingExperience : GroupExperienceAfterBattle(joinedGroup);
                total += WarbandRatingRules.WarriorContribution(freeHenchman.IsLargeCreature, experience, ExplorationFreeHenchmanCount, null);
            }
            if (ExplorationEquippedHenchmanGroup is { } prisonerGroup)
                total += WarbandRatingRules.WarriorContribution(prisonerGroup.Warrior.IsLargeCreature, GroupExperienceAfterBattle(prisonerGroup), 1, null);
            return total;
        }
    }

    private static int GroupExperienceAfterBattle(WarriorOutcomeRow row) =>
        row.Warrior.Experience + row.ExperienceGained + row.SeriousInjuryBonusExperience + row.ExplorationBonusExperience;

    public string ProjectedWarbandRatingDisplay => string.Format(Loc["EndOfGameRecapProjectedRatingFormat"], ProjectedWarbandRating);
}
