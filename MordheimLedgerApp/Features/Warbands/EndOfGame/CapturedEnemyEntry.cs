using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One captured enemy hero's fate, chosen by the player - see EndOfGameDialogViewModel.
/// CapturedEnemies. Ransomed/SoldToSlavers are always offered (any band can do either); KilledForZombie
/// only for an Undead warband, SacrificedForXp only for Cult of the Possessed (see
/// EndOfGameDialogViewModel.IsUndeadWarband/IsPossessedWarband) - this is the reverse side of the
/// simplified Captured (61) mechanic: there, OUR warrior is captured by an unmodeled opponent; here,
/// WE are the captor, so OUR warband's own type genuinely gates what's available, no opponent data
/// needed.</summary>
public enum CapturedEnemyFate
{
    Ransomed,
    SoldToSlavers,
    KilledForZombie,
    SacrificedForXp
}

public partial class CapturedEnemyEntry : ObservableObject
{
    private readonly Dictionary<string, CapturedEnemyFate> _fateByLabel = new();

    public int Index { get; }
    public List<string> FateLabels { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFate))]
    [NotifyPropertyChangedFor(nameof(ShowGoldAmount))]
    private string? selectedFateLabel;

    partial void OnSelectedFateLabelChanged(string? value)
    {
        if (value is not null) FateError = null;
        if (!ShowGoldAmount) GoldAmount = string.Empty;
    }

    /// <summary>Résolu depuis SelectedFateLabel - null tant qu'aucune option n'est choisie. Consommé par
    /// WarbandDetailViewModel.EndOfGame.ApplyCapturedEnemiesAsync pour appliquer le vrai effet (or gagné,
    /// Zombie recruté, +1 XP au chef).</summary>
    public CapturedEnemyFate? SelectedFate =>
        SelectedFateLabel is { } label && _fateByLabel.TryGetValue(label, out var fate) ? fate : null;

    /// <summary>Rançon/Vendu aux esclavagistes gagnent tous les deux de l'or pour NOTRE trésorerie -
    /// Rançon reste un montant négocié à la table (saisie libre, comme pour notre propre guerrier
    /// capturé), Vendu aux esclavagistes a une vraie formule du livre (1D6x5 CO, voir AutoRollSoldToSlavers
    /// sur le ViewModel parent) mais reste un champ modifiable pour un jet physique.</summary>
    public bool ShowGoldAmount => SelectedFate is CapturedEnemyFate.Ransomed or CapturedEnemyFate.SoldToSlavers;

    [ObservableProperty]
    private string goldAmount = string.Empty;

    public bool HasValidGoldAmount => int.TryParse(GoldAmount, out var amount) && amount >= 0;

    partial void OnGoldAmountChanged(string value)
    {
        if (HasValidGoldAmount) FateError = null;
    }

    /// <summary>Même principe que WarriorOutcomeRow.RollError, pour ce prisonnier.</summary>
    [ObservableProperty]
    private string? fateError;

    public CapturedEnemyEntry(int index, bool isUndeadWarband, bool isPossessedWarband, LocalizationService loc)
    {
        Index = index;

        void AddFate(CapturedEnemyFate fate)
        {
            var label = loc[$"CapturedEnemyFate{fate}"];
            _fateByLabel[label] = fate;
            FateLabels.Add(label);
        }

        AddFate(CapturedEnemyFate.Ransomed);
        AddFate(CapturedEnemyFate.SoldToSlavers);
        if (isUndeadWarband) AddFate(CapturedEnemyFate.KilledForZombie);
        if (isPossessedWarband) AddFate(CapturedEnemyFate.SacrificedForXp);
    }
}
