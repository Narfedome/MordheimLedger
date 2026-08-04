using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>
/// Wizard 5 étapes : Résultat, Blessures Graves, Expérience, Trésor, Récapitulatif - dans cet ordre
/// pour suivre la "Séquence d'après-bataille" du livre de règles (Blessures Graves puis Expérience
/// puis Revenus, à faire "devant témoin" juste après la partie ; le reste de la séquence - vente de
/// pierre magique, disponibilité des vétérans, personnages spéciaux, achats... - n'est pas dans ce
/// dialog, soit hors périmètre V1 soit déjà couvert ailleurs dans l'appli, ex. Recruter/Ajouter un
/// objet sur la carte guerrier). Résultat n'est pas une étape du livre à proprement parler, gardé en
/// premier comme contexte léger pour la phrase d'Historique.
/// Même pattern CurrentStep/IsStepN que WarriorArchetypeEditDialogViewModel. L'étape Expérience reste
/// une saisie libre pour l'instant (pas de calcul assisté ni de jets d'Advance - ce sera une passe
/// séparée) ; le Statut, lui, n'est plus une saisie manuelle du tout - voir WarriorOutcomeRow.
/// ApplyInjuryRoll.
/// </summary>
public partial class EndOfGameDialogViewModel : DialogViewModel<bool>
{
    private const int StepCount = 5;

    protected override bool CancelResult => false;

    public ObservableCollection<string> ResultOptions { get; } = new();
    public ObservableCollection<WarriorOutcomeRow> WarriorRows { get; }

    [ObservableProperty]
    private string selectedResult;

    [ObservableProperty]
    private int treasuryFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsStep4))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int currentStep;

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool CanGoBack => CurrentStep > 0;
    public bool IsLastStep => CurrentStep == StepCount - 1;
    public string StepLabel => string.Format(Loc["LibStepLabel"], CurrentStep + 1, StepCount);

    public EndOfGameDialogViewModel(IEnumerable<Warrior> activeWarriors)
    {
        ResultOptions.Add(Loc["EndOfGameResultVictory"]);
        ResultOptions.Add(Loc["EndOfGameResultDefeat"]);
        ResultOptions.Add(Loc["EndOfGameResultDraw"]);
        selectedResult = ResultOptions[0];

        WarriorRows = new ObservableCollection<WarriorOutcomeRow>(activeWarriors.Select(w => new WarriorOutcomeRow(w)));
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep < StepCount - 1) CurrentStep++;
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 0) CurrentStep--;
    }

    // Lance les dés à la place du joueur (D66 pour un Héros, D6 pour un Homme de main - deux tables
    // totalement différentes, voir SeriousInjuryTable/HenchmanInjuryTable) et affiche tout de suite le
    // résultat complet dans une popup - le champ ManualRoll reste modifiable ensuite si le joueur
    // préfère un jet physique.
    [RelayCommand]
    private async Task AutoRoll(WarriorOutcomeRow row)
    {
        var (roll, text) = row.Warrior.IsHero ? SeriousInjuryTable.Roll() : HenchmanInjuryTable.Roll();
        row.ManualRoll = roll.ToString();
        row.InjuryResultText = text;
        row.ApplyInjuryRoll(roll);
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    // Pour un jet fait à la table (dés physiques) : le joueur saisit son score dans ManualRoll, ce
    // bouton affiche juste le résultat correspondant sans en tirer un nouveau.
    [RelayCommand]
    private async Task ShowInjuryResult(WarriorOutcomeRow row)
    {
        if (!int.TryParse(row.ManualRoll, out var roll))
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidRoll"]);
            return;
        }

        // out var across a ternary's branches isn't definite-assignment-friendly - explicit if/else.
        string text;
        bool found;
        if (row.Warrior.IsHero) found = SeriousInjuryTable.TryGet(roll, out text);
        else found = HenchmanInjuryTable.TryGet(roll, out text);

        if (!found)
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidRoll"]);
            return;
        }

        row.InjuryResultText = text;
        row.ApplyInjuryRoll(roll);
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    [RelayCommand]
    private void Save() => Close(true);
}

/// <summary>One row per active Warrior in the End of Game dialog — collects the outcome, the caller
/// (WarbandDetailViewModel) applies it via IWarbandService and builds the History sentence.</summary>
public partial class WarriorOutcomeRow : ObservableObject
{
    private readonly Dictionary<string, WarriorStatus> _statusByLabel = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public Warrior Warrior { get; }
    public string Name => Warrior.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int experienceGained;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInjuryTools))]
    private bool isOutOfAction;

    /// <summary>Le score D66 - saisi à la main (jet physique) ou rempli par AutoRoll.</summary>
    [ObservableProperty]
    private string manualRoll = string.Empty;

    /// <summary>Texte complet du résultat une fois consulté (via AutoRoll ou ShowInjuryResult) -
    /// c'est ce texte qui alimente la note du guerrier et la phrase d'Historique à la sauvegarde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string injuryResultText = string.Empty;

    /// <summary>Plus de saisie manuelle : uniquement modifié par ApplyInjuryRoll (résultat "Mort",
    /// jets 11-15). Reste sur Warrior.Status (donc "Actif") pour tout le reste, y compris les
    /// rétablissements et le résultat "Blessures multiples" (16, ambigu - deux jets de plus requis).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(IsDead))]
    private string selectedStatusLabel = string.Empty;

    public bool ShowInjuryTools => IsOutOfAction;
    public WarriorStatus Status => _statusByLabel.GetValueOrDefault(SelectedStatusLabel, Warrior.Status);
    public bool IsDead => Status == WarriorStatus.Dead;

    /// <summary>Héros et Hommes de main utilisent deux tables de Blessures Graves totalement
    /// différentes (D66 vs D6, voir SeriousInjuryTable/HenchmanInjuryTable) - ce label et ce
    /// placeholder gardent la distinction visible sur chaque carte du wizard.</summary>
    public string RoleLabel => _loc[Warrior.IsHero ? "WarriorRoleHeroSingular" : "WarriorRoleHenchmanSingular"];
    public string RollPlaceholder => _loc[Warrior.IsHero ? "EndOfGameRollPh" : "EndOfGameHenchmanRollPh"];

    /// <summary>Ligne affichée à l'étape Récapitulatif - ne liste que ce qui a réellement changé.</summary>
    public string SummaryText
    {
        get
        {
            var parts = new List<string>();
            if (ExperienceGained != 0) parts.Add($"+{ExperienceGained} PX");
            if (Status != Warrior.Status) parts.Add(SelectedStatusLabel);
            if (!string.IsNullOrWhiteSpace(InjuryResultText)) parts.Add(InjuryResultText);

            return parts.Count > 0
                ? $"{Name} : {string.Join(", ", parts)}"
                : string.Format(_loc["EndOfGameNoChange"], Name);
        }
    }

    public WarriorOutcomeRow(Warrior warrior)
    {
        Warrior = warrior;

        foreach (var status in new[] { WarriorStatus.Active, WarriorStatus.Dead })
            _statusByLabel[_loc[$"WarriorStatus{status}"]] = status;

        selectedStatusLabel = _statusByLabel.First(kv => kv.Value == warrior.Status).Key;
    }

    /// <summary>Appelé après un jet de Blessure Grave - passe le guerrier à Mort pour les résultats
    /// de mort sans équivoque (Héros : 11-15 sur la table D66 ; Homme de main : 1-2 sur la table D6,
    /// mécaniques totalement différentes). Le reste (rétablissements, blessures permanentes,
    /// "Blessures multiples"...) ne touche pas le Statut.</summary>
    public void ApplyInjuryRoll(int roll)
    {
        var isDeath = Warrior.IsHero ? SeriousInjuryTable.IsDeath(roll) : HenchmanInjuryTable.IsDeath(roll);
        if (!isDeath) return;

        SelectedStatusLabel = _statusByLabel.First(kv => kv.Value == WarriorStatus.Dead).Key;
    }
}
