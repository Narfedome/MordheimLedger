using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One injury roll stacked under a WarriorOutcomeRow, in one of two unrelated situations that
/// happen to share the exact same shape (an indexed D66/D6 roll auto-resolving to text): (1) a Hero's
/// D66 sub-roll from a "Blessures multiples" result (16/21) - see
/// WarriorOutcomeRow.MultipleInjuryRolls/PopulateMultipleInjuryRolls; (2) one D6 roll per Henchman
/// group model marked out of action - see WarriorOutcomeRow.FigureInjuryRolls/SyncFigureInjuryRolls.
/// IsHero picks which table resolves ManualRoll (always Hero/D66 for case 1, always Henchman/D6 for
/// case 2 - never mixed within one collection). Same "accept the result as-is" stance as the main
/// injury roll for a sub-roll landing on Dead/Captured/Multiple Injuries again: the rulebook says to
/// re-roll but the app leaves that to the player rather than enforcing it (see SeriousInjuryTable's doc
/// comment).</summary>
public partial class InjurySubRollEntry : ObservableObject
{
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly string _labelKey;

    public int Index { get; }
    public int Total { get; set; }
    public bool IsHero { get; }
    public string Label => string.Format(_loc[_labelKey], Index, Total);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDeepWoundSubRoll))]
    [NotifyPropertyChangedFor(nameof(ShowCapturedChoice))]
    private string manualRoll = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.RollError, posé uniquement par
    /// EndOfGameDialogViewModel.Next si ce jet est encore vide/invalide à ce moment-là.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        InjuryResultText = string.Empty;
        if (!int.TryParse(value, out var roll))
        {
            if (DeepWoundSubRoll.Length > 0) DeepWoundSubRoll = string.Empty;
            return;
        }

        bool found;
        string key;
        found = IsHero ? SeriousInjuryTable.TryGetTextKey(roll, out key) : HenchmanInjuryTable.TryGetTextKey(roll, out key);
        if (found)
        {
            InjuryResultText = _loc[key];
            RollError = null;
        }

        // Même principe que WarriorOutcomeRow.OnManualRollChanged pour le sous-jet de branche - un
        // nouveau jet qui ne tombe plus sur 35 invalide le sous-jet de Blessure profonde déjà saisi.
        if (!ShowDeepWoundSubRoll && DeepWoundSubRoll.Length > 0)
            DeepWoundSubRoll = string.Empty;

        // Même principe pour le choix de Capturé (61).
        if (!ShowCapturedChoice && SelectedCapturedOutcomeLabel is not null)
            SelectedCapturedOutcomeLabel = null;
    }

    [ObservableProperty]
    private string injuryResultText = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.ShowDeepWoundSubRoll, pour un sous-jet "Blessures
    /// multiples" (16/21) qui tombe lui-même sur 35 - toujours faux pour un jet Homme de main (IsHero
    /// false), qui utilise une table D6 totalement différente sans résultat 35.</summary>
    public bool ShowDeepWoundSubRoll => IsHero && int.TryParse(ManualRoll, out var roll) && roll == 35;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeepWoundConfirmationText))]
    private string deepWoundSubRoll = string.Empty;

    partial void OnDeepWoundSubRollChanged(string value)
    {
        if (HasValidDeepWoundSubRoll) DeepWoundRollError = null;
    }

    public bool HasValidDeepWoundSubRoll => int.TryParse(DeepWoundSubRoll, out var subRoll) && subRoll is >= 1 and <= 3;

    public string DeepWoundConfirmationText =>
        HasValidDeepWoundSubRoll && int.TryParse(DeepWoundSubRoll, out var subRoll)
            ? string.Format(_loc["EndOfGameDeepWoundResultFormat"], subRoll)
            : string.Empty;

    [ObservableProperty]
    private string? deepWoundRollError;

    /// <summary>Même principe que WarriorOutcomeRow.ShowCapturedChoice, pour un sous-jet "Blessures
    /// multiples" qui tombe lui-même sur 61.</summary>
    public bool ShowCapturedChoice => IsHero && int.TryParse(ManualRoll, out var roll) && roll == 61;

    private readonly Dictionary<string, CapturedOutcome> _capturedOutcomeByLabel = new();

    public List<string> CapturedOutcomeLabels { get; } = new();

    [ObservableProperty]
    private string? selectedCapturedOutcomeLabel;

    partial void OnSelectedCapturedOutcomeLabelChanged(string? value)
    {
        if (value is not null) CapturedChoiceError = null;
    }

    public CapturedOutcome? SelectedCapturedOutcome =>
        SelectedCapturedOutcomeLabel is { } label && _capturedOutcomeByLabel.TryGetValue(label, out var outcome) ? outcome : null;

    [ObservableProperty]
    private string? capturedChoiceError;

    /// <summary>True si le jet actuellement saisi est un résultat de mort (Héros 11-15, Homme de main
    /// 1-2) - utilisé par WarbandDetailViewModel.EndOfGame pour compter les figurines perdues dans un
    /// groupe d'Hommes de main (voir WarriorOutcomeRow.FigureInjuryRolls). Sans objet pour les sous-jets
    /// de Blessures multiples d'un Héros (déjà géré au niveau du jet principal, voir ApplyInjuryRoll).</summary>
    public bool IsDeath => int.TryParse(ManualRoll, out var roll) && (IsHero ? SeriousInjuryTable.IsDeath(roll) : HenchmanInjuryTable.IsDeath(roll));

    public InjurySubRollEntry(int index, int total, bool isHero, string labelKey)
    {
        Index = index;
        Total = total;
        IsHero = isHero;
        _labelKey = labelKey;

        foreach (var outcome in Enum.GetValues<CapturedOutcome>())
        {
            var label = _loc[$"CapturedOutcome{outcome}"];
            _capturedOutcomeByLabel[label] = outcome;
            CapturedOutcomeLabels.Add(label);
        }
    }

    /// <summary>Steps.SyncFigureInjuryRolls-style syncs (voir WarriorOutcomeRow.SyncFigureInjuryRolls)
    /// n'ajoutent/ne retirent qu'en bout de liste et préservent les entrées existantes - mais Total (le
    /// nombre total affiché dans Label, ex. "Figurine 2/3") doit rester à jour sur celles-ci quand le
    /// compte global change.</summary>
    public void UpdateTotal(int total)
    {
        if (Total == total) return;
        Total = total;
        OnPropertyChanged(nameof(Label));
    }
}
