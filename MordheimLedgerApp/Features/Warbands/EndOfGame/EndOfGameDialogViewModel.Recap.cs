using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Récapitulatif" (pas une étape du livre - relecture avant d'Enregistrer) - étoffée le
/// 2026-09-22 (retour utilisateur : "c'est censé être un récap de toutes les étapes qu'on a fait") pour
/// couvrir toutes les décisions prises ailleurs dans ce même wizard, pas seulement Résultat/Exploration/
/// Prisonniers/le résumé par guerrier (déjà dans EndOfGameDialog.xaml avant cette passe). Chaque section
/// ci-dessous réutilise TELLES QUELLES des données déjà exposées par l'étape correspondante (aucune
/// nouvelle logique de calcul, sauf ProjectedWarbandRating) - un simple aperçu en lecture seule, jamais
/// une source de vérité pour Terminer.
///
/// Réallouer l'équipement (dernière étape ajoutée) reste HORS de ce récap : c'est la seule qui ne garde
/// aucune trace de ses déplacements (juste une mutation en mémoire, voir EndOfGameDialogViewModel.
/// Reallocation.cs), retour utilisateur explicite - "on peut si besoin le faire plus tard".</summary>
public partial class EndOfGameDialogViewModel
{
    public bool HasWyrdstoneSaleRecap => ShardsToSell > 0;

    /// <summary>Toujours affiché (le jet a lieu à chaque Fin de Partie, que la bande recrute ou non des
    /// vétérans - voir EndOfGameDialogViewModel.Recruitment.cs's RemainingVeteranBudget's own doc).</summary>
    public string AvailableVeteransRecapDisplay => string.Format(Loc["EndOfGameRecapVeteransFormat"], VeteranExperienceRoll);

    public bool HasRareItemsRecap => RareItemsWithResults.Count > 0;

    public IEnumerable<string> RareItemsRecapLines => RareItemsWithResults.Select(e => e.IsSearchingForCharacter
        ? string.Format(Loc["EndOfGameRecapRareCharacterFormat"], e.HeroName, e.SelectedCharacterDisplayName)
        : string.Format(Loc["EndOfGameRecapRareItemFormat"], e.HeroName, e.SelectedItem?.Name, e.EffectiveCost ?? 0));

    public bool HasDramatisPersonaeRecap => DramatisPersonaUpkeepEntries.Any(e => e.WillPay.HasValue);

    public IEnumerable<string> DramatisPersonaeRecapLines => DramatisPersonaUpkeepEntries.Where(e => e.WillPay.HasValue)
        .Select(e => string.Format(e.WillPay == true ? Loc["EndOfGameRecapPaidFormat"] : Loc["EndOfGameRecapDismissedFormat"], e.DisplayName, e.UpkeepCostDisplay));

    /// <summary>Corruption/Rétention/Duel sont mutuellement exclusifs (voir EndOfGameDialogViewModel.
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

    public bool HasHiredSwordsRecap => HiredSwordUpkeepEntries.Any(e => e.WillPay.HasValue || e.IsPrepaidFree) || SelectedNewHiredSword is not null;

    public IEnumerable<string> HiredSwordsRecapLines
    {
        get
        {
            foreach (var entry in HiredSwordUpkeepEntries.Where(e => e.WillPay.HasValue || e.IsPrepaidFree))
                yield return entry.IsPrepaidFree
                    ? string.Format(Loc["EndOfGameRecapHiredSwordPrepaidFormat"], entry.DisplayName)
                    : string.Format(entry.WillPay == true ? Loc["EndOfGameRecapPaidFormat"] : Loc["EndOfGameRecapDismissedFormat"], entry.DisplayName, entry.UpkeepCost);
            if (SelectedNewHiredSword is { } hiredSword)
                yield return string.Format(Loc["EndOfGameRecapHiredSwordHiredFormat"], hiredSword.Name, hiredSword.HireCost);
        }
    }

    public bool HasDismissedWarriorsRecap => WarriorRows.Any(r => r.DismissCount > 0);

    public IEnumerable<string> DismissedWarriorsRecapLines => WarriorRows.Where(r => r.DismissCount > 0)
        .Select(r => string.Format(Loc["EndOfGameRecapDismissedWarriorFormat"], r.Name, r.DismissLabel));

    public bool HasEquipmentTradingRecap => PurchasedReserveItems.Count > 0 || PendingSales.Count > 0;

    public IEnumerable<string> EquipmentTradingPurchasedRecapLines => PurchasedReserveItems.Select(p => p.Name);

    public IEnumerable<string> EquipmentTradingSoldRecapLines => PendingSales.Select(c => c.SoldLabel);

    public bool HasRecruitmentRecap => RecruitedHeroRows.Any() || RecruitedHenchmanRows.Any() || ExistingHenchmanTopUps.Any(t => t.AddCount > 0);

    public IEnumerable<string> RecruitedHeroesRecapLines => RecruitedHeroRows.SelectMany(r => r.NameSlots).Select(s => s.DisplayLabel);

    public IEnumerable<string> RecruitedHenchmenRecapLines => RecruitedHenchmanRows.SelectMany(r => r.HenchmanGroupDrafts)
        .Select(g => string.Format(Loc["EndOfGameRecapHenchmanGroupFormat"], g.Name, g.Count));

    public IEnumerable<string> VeteranTopUpsRecapLines => ExistingHenchmanTopUps.Where(t => t.AddCount > 0)
        .Select(t => string.Format(Loc["EndOfGameRecapVeteranTopUpFormat"], t.Name, t.AddCount));

    /// <summary>Aperçu de la Valeur de Bande APRÈS cette Fin de Partie (livre, étape 10 - "Update your
    /// warband rating") - mirroring Core.Rules.WarbandRatingRules.WarriorContribution (déjà utilisé par
    /// WarbandDetailViewModel.Rating/WarbandService.GetWarbandRatingAsync, aucune nouvelle règle), calculé
    /// côté wizard AVANT Terminer pour que le joueur voie le chiffre avant de valider - la vraie Valeur
    /// (exacte) reste celle recalculée par WarbandDetailViewModel.LoadAsync après Enregistrer, jamais
    /// remplacée par cet aperçu.
    ///
    /// Limite acceptée, non résolue ici : ne recompte pas les figurines mortes de CETTE bataille pour un
    /// groupe d'Hommes de main - HeadCount reste l'effectif d'AVANT ce tour tant que Terminer n'a pas
    /// tourné (même limitation "figé jusqu'à Terminer" déjà acceptée partout ailleurs dans ce
    /// wizard).</summary>
    public int ProjectedWarbandRating
    {
        get
        {
            var total = 0;
            foreach (var row in WarriorRows.Where(r => !r.IsDead && !r.Warrior.IsHostileThisBattle))
            {
                var headCount = row.HeadCount - row.DismissCount;
                if (headCount <= 0) continue;
                var experience = row.Warrior.Experience + row.ExperienceGained + row.SeriousInjuryBonusExperience + row.ExplorationBonusExperience;
                total += WarbandRatingRules.WarriorContribution(row.Warrior.IsLargeCreature, experience, headCount, row.Warrior.HiredSwordBaseRating, row.Warrior.DramatisPersonaRatingBonus);
            }
            foreach (var topUp in ExistingHenchmanTopUps.Where(t => t.AddCount > 0))
                total += WarbandRatingRules.WarriorContribution(false, topUp.GroupExperience, topUp.AddCount, null);
            foreach (var slot in RecruitedHeroRows.SelectMany(r => r.NameSlots))
                total += WarbandRatingRules.WarriorContribution(slot.Row.Archetype.IsLargeCreature, slot.Row.Archetype.StartingExperience, 1, null);
            foreach (var group in RecruitedHenchmanRows.SelectMany(r => r.HenchmanGroupDrafts))
                total += WarbandRatingRules.WarriorContribution(group.Row.Archetype.IsLargeCreature, group.Row.Archetype.StartingExperience, group.Count, null);
            if (SelectedNewHiredSword is { } hiredSword)
                total += WarbandRatingRules.WarriorContribution(false, 0, 1, hiredSword.BaseRating);
            foreach (var entry in RareItemsWithResults.Where(e => e.IsRecruited))
                total += WarbandRatingRules.WarriorContribution(false, 0, 1, null, entry.SelectedCharacter!.RatingBonus);
            return total;
        }
    }

    public string ProjectedWarbandRatingDisplay => string.Format(Loc["EndOfGameRecapProjectedRatingFormat"], ProjectedWarbandRating);
}
