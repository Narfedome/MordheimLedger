using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Un personnage ou un duo portant « Une Poignée d'Or » dans l'étape du même nom (2026-11-14 : une carte par
/// groupe au lieu d'un seul personnage par bande). Corruption si la bande ne l'a pas (ou l'a perdu cette partie),
/// Rétention si elle l'a encore, rien sinon. Le règlement (or, saisie, duel) est calculé par le ViewModel pour tous
/// les groupes à la fois (<see cref="PairDebtRules.SettleInOrder"/>) puis poussé ici par <see cref="ApplyOutcome"/>.</summary>
public partial class FistfulOfCrownsEntry : ObservableObject
{
    private readonly IReadOnlyList<WarriorOutcomeRow> _ownedRows;
    private readonly IReadOnlyList<WarriorOutcomeRow> _retentionRows;
    private readonly Func<FistfulOfCrownsGroup, bool> _isBeingRecruited;
    private readonly Action _changed;

    public FistfulOfCrownsEntry(FistfulOfCrownsGroup group, IReadOnlyList<WarriorOutcomeRow> ownedRows,
        IReadOnlyList<WarriorOutcomeRow> retentionRows, IReadOnlyList<DuelOpponentDisplay> duelOpponents,
        Func<FistfulOfCrownsGroup, bool> isBeingRecruited, Action changed)
    {
        Group = group;
        _ownedRows = ownedRows;
        _retentionRows = retentionRows;
        DuelOpponents = duelOpponents;
        _isBeingRecruited = isBeingRecruited;
        _changed = changed;
    }

    private static LocalizationService Loc => LocalizationService.Instance;

    public FistfulOfCrownsGroup Group { get; }

    public string Name => Group.DisplayName;

    public IReadOnlyList<DuelOpponentDisplay> DuelOpponents { get; }

    /// <summary>Proposée tant que la bande n'a plus personne de ce groupe en vie (jamais recruté, ou mort cette
    /// partie) et ne l'engage pas dans cette même Fin de partie.</summary>
    public bool ShowCorruption => !_ownedRows.Any(r => !r.IsDead) && !_isBeingRecruited(Group);

    /// <summary>La bande l'a encore et il n'a pas été rendu hostile (corrompu cette bataille, il part de toute façon).</summary>
    public bool ShowRetention => _retentionRows.Any(r => !r.IsDead);

    public bool IsActive => ShowCorruption || ShowRetention;

    public string CorruptionLabel => string.Format(Loc["EndOfGamePairCorruptionOptionFormat"], Name);

    [ObservableProperty]
    private bool wantsToRecordCorruption;

    [ObservableProperty]
    private string corruptionAmount = string.Empty;

    [ObservableProperty]
    private string? corruptionError;

    [ObservableProperty]
    private string retentionAmount = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDuelLost))]
    private bool wonDuel;

    public bool IsDuelLost => !WonDuel;

    partial void OnWantsToRecordCorruptionChanged(bool value)
    {
        if (!value)
        {
            CorruptionAmount = string.Empty;
            CorruptionError = null;
        }
        _changed();
    }

    partial void OnCorruptionAmountChanged(string value)
    {
        CorruptionError = null;
        _changed();
    }

    partial void OnRetentionAmountChanged(string value) => _changed();

    partial void OnWonDuelChanged(bool value) => _changed();

    /// <summary>Montant en jeu pour ce groupe : la corruption cochée, ou la rétention saisie ; 0 sinon.</summary>
    public int AmountDue =>
        ShowCorruption && WantsToRecordCorruption && int.TryParse(CorruptionAmount, out var corruption) && corruption > 0 ? corruption
        : ShowRetention && int.TryParse(RetentionAmount, out var retention) && retention > 0 ? retention
        : 0;

    public bool IsCorruptionPaid => ShowCorruption && AmountDue > 0;

    public bool Validate()
    {
        if (!ShowCorruption || !WantsToRecordCorruption) return true;
        if (int.TryParse(CorruptionAmount, out var amount) && amount > 0) return true;
        CorruptionError = Loc["EndOfGameAmountRequired"];
        return false;
    }

    // --- Règlement (« Où est l'Argent ? ») --------------------------------------------------------------

    public PairDebtPayment Payment { get; private set; }

    public List<ReserveLine> SeizedEquipmentItems { get; private set; } = new();

    /// <summary>La bande ne paie pas en or : saisie ou duel.</summary>
    public bool IsUnaffordable => Payment is PairDebtPayment.SeizeEquipment or PairDebtPayment.Duel;

    public bool WantsEquipmentSeizure => Payment == PairDebtPayment.SeizeEquipment;

    public bool WantsDuel => Payment == PairDebtPayment.Duel;

    public int SeizedEquipmentTotalValue => SeizedEquipmentItems.Sum(l => l.SellValue);

    public string SeizedEquipmentTotalDisplay => string.Format(Loc["EndOfGameSeizedEquipmentTotalFormat"], SeizedEquipmentTotalValue, AmountDue);

    public void ApplyOutcome(PairDebtOutcome<ReserveLine> outcome)
    {
        Payment = outcome.Payment;
        SeizedEquipmentItems = outcome.SeizedLines;
        if (!WantsDuel) WonDuel = false;
        OnPropertyChanged(nameof(Payment));
        OnPropertyChanged(nameof(IsUnaffordable));
        OnPropertyChanged(nameof(WantsEquipmentSeizure));
        OnPropertyChanged(nameof(WantsDuel));
        OnPropertyChanged(nameof(SeizedEquipmentItems));
        OnPropertyChanged(nameof(SeizedEquipmentTotalValue));
        OnPropertyChanged(nameof(SeizedEquipmentTotalDisplay));
    }

    /// <summary>Le roster ou la recherche de personnages a changé : la carte peut passer de Corruption à Rétention.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(ShowCorruption));
        OnPropertyChanged(nameof(ShowRetention));
        OnPropertyChanged(nameof(IsActive));
    }
}
