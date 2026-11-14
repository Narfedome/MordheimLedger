using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape « Une Poignée d'Or » (StepKind.PairEngagement), juste après les soldes des Dramatis Personae.
/// Corruption (la bande soudoie un personnage qu'elle n'a pas, débité de SA trésorerie) et Rétention (ce qu'elle a payé
/// pour garder le sien après une tentative adverse - « seul le camp qui obtient OU GARDE le contrôle paie »). Pilotée
/// par la règle spéciale « A Fistful of Crowns », pas par FeeKind.Pair (le recrutement combiné d'Ulli et Marquand, sujet
/// distinct). Si la bande ne peut pas payer en or, « Où est l'Argent ? » choisit seul : la réserve couvre la dette →
/// saisie de matériel ; sinon duel avec le meneur (étape PairDuel). Aucun or ne sort dans ces deux cas.
///
/// Le cas où CETTE bande se fait corrompre son personnage n'a pas d'UI ici : le joueur le rend hostile sur sa carte
/// (WarbandDetailViewModel.ToggleHostile) et il part à Terminer, sans paiement de son côté.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Une carte par personnage ou duo portant « Une Poignée d'Or » (2026-11-14, retour utilisateur : plusieurs
    /// personnages peuvent porter la règle, et sans mode multijoueur l'appli ne sait pas lesquels sont chez l'adversaire
    /// - elle les propose tous). Groupes : Core.Rules.FistfulOfCrownsRules ; règlement dans l'ordre des cartes :
    /// Core.Rules.PairDebtRules.SettleInOrder.</summary>
    public List<FistfulOfCrownsEntry> FistfulOfCrownsEntries { get; private set; } = new();

    public IEnumerable<FistfulOfCrownsEntry> ActiveFistfulOfCrownsEntries => FistfulOfCrownsEntries.Where(e => e.IsActive);

    public bool HasFistfulOfCrownsStep => FistfulOfCrownsEntries.Any(e => e.IsActive);

    private bool ValidatePairEngagementStep()
    {
        var valid = true;
        foreach (var entry in ActiveFistfulOfCrownsEntries) valid &= entry.Validate();
        return valid;
    }

    private void BuildFistfulOfCrownsEntries()
    {
        var ruleId = _specialRulesByOfficialId.GetValueOrDefault(OfficialIds.FistfulOfCrownsRule)?.Id;
        FistfulOfCrownsEntries = FistfulOfCrownsRules.Groups(_dramatisPersonaCatalog, ruleId).Select(group =>
        {
            var ownedRows = WarriorRows.Where(r => r.Warrior.IsDramatisPersona && r.Warrior.DramatisPersonaId is { } id && group.Contains(id)).ToList();
            // Un personnage rendu hostile (corrompu cette bataille) part de toute façon : rien à payer pour le garder.
            var retentionRows = ownedRows.Where(r => !r.Warrior.IsHostileThisBattle).ToList();
            var opponents = new List<DuelOpponentDisplay> { new(group.Primary, ResolveDuelOpponentEquipment(group.Primary)) };
            if (group.Partner is { } partner) opponents.Add(new(partner, ResolveDuelOpponentEquipment(partner)));
            return new FistfulOfCrownsEntry(group, ownedRows, retentionRows, opponents, IsBeingRecruitedThisWizard, OnFistfulOfCrownsChanged);
        }).ToList();
    }

    /// <summary>Engagé par une recherche « Personnage spécial » réussie de cette Fin de partie : pas de corruption à
    /// proposer (retour utilisateur : « si on a engagé la paire, on ne doit pas voir la case de corruption »). Une
    /// recherche ratée ne cache rien.</summary>
    private bool IsBeingRecruitedThisWizard(FistfulOfCrownsGroup group) =>
        RareItemSearchEntries.Any(e => e.IsRecruited && e.SelectedCharacter is { } c && group.Contains(c.Id));

    private void OnFistfulOfCrownsChanged()
    {
        if (_settlingFistfulOfCrowns) return;
        NotifyTreasuryChanged();
    }

    /// <summary>Le roster ou les recrutements ont changé : une carte peut apparaître, disparaître ou changer de sens.</summary>
    private void RefreshFistfulOfCrownsEntries()
    {
        foreach (var entry in FistfulOfCrownsEntries) entry.Refresh();
        OnPropertyChanged(nameof(HasFistfulOfCrownsStep));
        OnPropertyChanged(nameof(ActiveFistfulOfCrownsEntries));
    }

    /// <summary>Or réellement payé par les cartes (règlement déjà calculé) - retranché de EndOfGameTreasuryRemaining.</summary>
    private int PairPaymentAmountCommitted => ActiveFistfulOfCrownsEntries.Where(e => e.Payment == PairDebtPayment.Gold).Sum(e => e.AmountDue);

    /// <summary>Solde disponible avant les paiements « Une Poignée d'Or », une fois toutes les autres dépenses du wizard
    /// comptées (« un seul solde de trésorerie qu'on ajuste au fur et à mesure des étapes »).</summary>
    private int DramatisPersonaeBaselineTreasury => TreasuryBeforeFistfulOfCrowns;

    public string DramatisPersonaeBaselineTreasuryDisplay => string.Format(Loc["EndOfGameDramatisPersonaeBaselineTreasuryFormat"], DramatisPersonaeBaselineTreasury);

    private bool _settlingFistfulOfCrowns;

    /// <summary>Recalcule le règlement de chaque carte (or, saisie, duel). Appelée par NotifyTreasuryChanged : le solde
    /// et la réserve en dépendent. Pendant ce calcul, la saisie en cours n'est pas comptée dans la réserve (voir
    /// SeizedReserveSourceIds).</summary>
    private void SettleFistfulOfCrowns()
    {
        if (_settlingFistfulOfCrowns) return;
        _settlingFistfulOfCrowns = true;
        _resolvingSeizure = true;
        try
        {
            var entries = FistfulOfCrownsEntries;
            var amounts = entries.Select(e => e.IsActive ? e.AmountDue : 0).ToList();
            var outcomes = PairDebtRules.SettleInOrder(amounts, DramatisPersonaeBaselineTreasury, _originalReserveSnapshot, line => line.SellValue);
            for (var i = 0; i < entries.Count; i++) entries[i].ApplyOutcome(outcomes[i]);
        }
        finally
        {
            _resolvingSeizure = false;
            _settlingFistfulOfCrowns = false;
        }
        OnPropertyChanged(nameof(WantsDuel));
        OnPropertyChanged(nameof(DuelEntries));
        OnPropertyChanged(nameof(LostAnyDuel));
        OnPropertyChanged(nameof(DramatisPersonaeBaselineTreasuryDisplay));
    }

    /// <summary>Lignes de réserve perdues par toutes les saisies.</summary>
    private IEnumerable<ReserveLine> AllSeizedEquipmentItems => ActiveFistfulOfCrownsEntries.SelectMany(e => e.SeizedEquipmentItems);
}
