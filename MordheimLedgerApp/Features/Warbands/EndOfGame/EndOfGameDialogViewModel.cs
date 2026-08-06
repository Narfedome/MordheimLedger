using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>
/// Wizard jusqu'à 6 étapes : Résultat, Blessures Graves, Expérience, Progression, Trésor,
/// Récapitulatif - dans cet ordre pour suivre la "Séquence d'après-bataille" du livre de règles
/// (Blessures Graves puis Expérience puis Revenus, à faire "devant témoin" juste après la partie ;
/// le reste de la séquence - vente de pierre magique, disponibilité des vétérans, personnages
/// spéciaux, achats... - n'est pas dans ce dialog, soit hors périmètre V1 soit déjà couvert ailleurs
/// dans l'appli, ex. Recruter/Ajouter un objet sur la carte guerrier). Résultat n'est pas une étape
/// du livre à proprement parler, gardé en premier comme contexte léger pour la phrase d'Historique.
/// Progression n'est pas non plus une étape séparée du livre (le jet s'y fait "immédiatement" dès
/// qu'un palier d'XP est franchi, cf. Campagne.md § Expérience) mais mérite son propre écran plutôt
/// que d'être imbriquée dans Expérience : un guerrier peut franchir plusieurs paliers d'un coup, un
/// jet 2D6 par palier (voir WarriorOutcomeRow.AdvanceRolls/ExperienceMilestones). Elle disparaît
/// entièrement du wizard (ActiveSteps) si aucun guerrier n'a franchi de palier cette partie.
/// Même pattern CurrentStep/IsStepN que WarriorArchetypeEditDialogViewModel. Le Statut n'est plus une
/// saisie manuelle du tout - voir WarriorOutcomeRow.ApplyInjuryRoll.
/// </summary>
public partial class EndOfGameDialogViewModel : DialogViewModel<bool>
{
    private readonly ISkillPickerService _skillPicker;
    private readonly int _warbandArchetypeId;

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
    [NotifyPropertyChangedFor(nameof(IsStep5))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int currentStep;

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;

    /// <summary>True si au moins un guerrier franchit un palier d'XP cette partie - évalué à chaque
    /// Next/Back (pas mis en cache) puisque ça dépend des PX saisis à l'étape Expérience juste
    /// avant. Pilote à la fois ActiveSteps (l'étape 3 "Progression" est incluse ou non) et l'affichage
    /// conditionnel de son contenu dans le XAML (IsStep3 seul ne suffit pas : encore faut-il que
    /// CurrentStep puisse valoir 3, ce que ActiveSteps empêche si ce flag est faux).</summary>
    public bool HasAnyMilestone => WarriorRows.Any(r => r.HasMilestone);

    /// <summary>Étape 3 (Progression) incluse seulement si HasAnyMilestone - les indices des autres
    /// étapes restent fixes (0..5) pour que IsStepN reste simple, seule la séquence de navigation et
    /// la numérotation affichée (StepLabel) sautent l'étape 3 quand elle est vide.</summary>
    private List<int> ActiveSteps => HasAnyMilestone ? [0, 1, 2, 3, 4, 5] : [0, 1, 2, 4, 5];

    public bool CanGoBack => ActiveSteps.IndexOf(CurrentStep) > 0;
    public bool IsLastStep => CurrentStep == ActiveSteps[^1];
    public string StepLabel => string.Format(Loc["LibStepLabel"], ActiveSteps.IndexOf(CurrentStep) + 1, ActiveSteps.Count);

    public EndOfGameDialogViewModel(IEnumerable<Warrior> activeWarriors, ISkillPickerService skillPicker, int warbandArchetypeId)
    {
        _skillPicker = skillPicker;
        _warbandArchetypeId = warbandArchetypeId;

        ResultOptions.Add(Loc["EndOfGameResultVictory"]);
        ResultOptions.Add(Loc["EndOfGameResultDefeat"]);
        ResultOptions.Add(Loc["EndOfGameResultDraw"]);
        selectedResult = ResultOptions[0];

        WarriorRows = new ObservableCollection<WarriorOutcomeRow>(activeWarriors.Select(w => new WarriorOutcomeRow(w)));
    }

    [RelayCommand]
    private void Next()
    {
        var steps = ActiveSteps;
        var index = steps.IndexOf(CurrentStep);
        if (index < steps.Count - 1) CurrentStep = steps[index + 1];
    }

    [RelayCommand]
    private void Back()
    {
        var steps = ActiveSteps;
        var index = steps.IndexOf(CurrentStep);
        if (index > 0) CurrentStep = steps[index - 1];
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

    // Un guerrier peut franchir plusieurs paliers d'un coup - chaque AdvanceRollEntry (une par palier,
    // voir WarriorOutcomeRow.SyncAdvanceRolls) est un jet 2D6 indépendant sur la table de progression,
    // même pattern que AutoRoll/ShowInjuryResult mais purement descriptif : aucune stat n'est modifiée
    // automatiquement (les sous-jets 1D6 des résultats 6/8/9 et le choix CC/CT du 7 restent à résoudre
    // par le joueur, cf. HeroAdvanceTable/HenchmanAdvanceTable).
    [RelayCommand]
    private async Task AutoRollAdvance(AdvanceRollEntry entry)
    {
        var (roll, text) = entry.IsHero ? HeroAdvanceTable.Roll() : HenchmanAdvanceTable.Roll();
        entry.ManualRoll = roll.ToString();
        entry.ResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    [RelayCommand]
    private async Task ShowAdvanceResult(AdvanceRollEntry entry)
    {
        if (!int.TryParse(entry.ManualRoll, out var roll))
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidAdvanceRoll"]);
            return;
        }

        string text;
        bool found;
        if (entry.IsHero) found = HeroAdvanceTable.TryGet(roll, out text);
        else found = HenchmanAdvanceTable.TryGet(roll, out text);

        if (!found)
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidAdvanceRoll"]);
            return;
        }

        entry.ResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    // Résultat "Compétence" (voir HeroAdvanceTable.IsSkill) : le joueur choisit directement une
    // compétence existante de la Bibliothèque, comme le "+" Compétences de la carte guerrier -
    // rattachée au guerrier par WarbandDetailViewModel.EndOfGame à l'enregistrement du wizard, pas
    // tout de suite (même logique différée que les autres résultats de cette étape).
    [RelayCommand]
    private async Task PickAdvanceSkill(AdvanceRollEntry entry)
    {
        var row = WarriorRows.First(r => r.AdvanceRolls.Contains(entry));
        var skills = await _skillPicker.PickSkillAsync(_warbandArchetypeId, row.Warrior.WarriorArchetypeId);
        foreach (var skill in skills)
            entry.SelectedSkills.Add(skill);
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
    [NotifyPropertyChangedFor(nameof(MilestoneCount))]
    [NotifyPropertyChangedFor(nameof(HasMilestone))]
    private int experienceGained;

    partial void OnExperienceGainedChanged(int value) => SyncAdvanceRolls();

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

    /// <summary>Un jet 2D6 par palier d'XP franchi cette partie (voir SyncAdvanceRolls) - un guerrier
    /// qui saute plusieurs cases à bord épais d'un coup doit relancer autant de fois sur la table de
    /// progression (Campagne.md § Expérience). Distincte du jet de Blessure Grave (ManualRoll) : les
    /// deux peuvent coexister le même End of Game.</summary>
    public ObservableCollection<AdvanceRollEntry> AdvanceRolls { get; } = new();

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

    /// <summary>Nombre de cases à bord épais franchies par les PX gagnés cette partie (voir
    /// ExperienceMilestones) - peut dépasser 1 si le guerrier cumule assez de PX pour sauter
    /// plusieurs paliers d'un coup, d'où AdvanceRolls plutôt qu'un jet unique.</summary>
    public int MilestoneCount => ExperienceMilestones.MilestonesCrossedCount(Warrior.IsHero, Warrior.Experience, Warrior.Experience + ExperienceGained);
    public bool HasMilestone => MilestoneCount > 0;

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
            foreach (var advance in AdvanceRolls)
            {
                if (advance.SelectedSkills.Count > 0) parts.Add(advance.SelectedSkillsText);
                else if (!string.IsNullOrWhiteSpace(advance.ResultText)) parts.Add(advance.ResultText);
            }

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

    /// <summary>Ajuste AdvanceRolls pour qu'il compte exactement un AdvanceRollEntry par palier
    /// franchi (MilestoneCount), en préservant les jets déjà faits quand le nombre ne diminue pas -
    /// appelé à chaque frappe dans le champ PX de l'étape Expérience (OnExperienceGainedChanged).</summary>
    private void SyncAdvanceRolls()
    {
        while (AdvanceRolls.Count < MilestoneCount)
        {
            var entry = new AdvanceRollEntry(AdvanceRolls.Count + 1, Warrior.IsHero);
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            AdvanceRolls.Add(entry);
        }
        while (AdvanceRolls.Count > MilestoneCount)
            AdvanceRolls.RemoveAt(AdvanceRolls.Count - 1);
    }
}

/// <summary>One 2D6 progression roll for one milestone crossed by a WarriorOutcomeRow - see
/// WarriorOutcomeRow.AdvanceRolls/SyncAdvanceRolls (a warrior can cross several milestones in the
/// same End of Game, each needing its own independent roll).</summary>
public partial class AdvanceRollEntry : ObservableObject
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public int Index { get; }
    public bool IsHero { get; }
    public string Label => string.Format(_loc["EndOfGameMilestoneLabel"], Index);

    /// <summary>Le score 2D6 - saisi à la main (jet physique) ou rempli par AutoRollAdvance.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkillResult))]
    private string manualRoll = string.Empty;

    /// <summary>Texte descriptif du résultat une fois consulté - purement informatif, voir
    /// HeroAdvanceTable/HenchmanAdvanceTable.</summary>
    [ObservableProperty]
    private string resultText = string.Empty;

    /// <summary>Seuls les résultats "Compétence" des Héros (voir HeroAdvanceTable.IsSkill) proposent
    /// de choisir directement une compétence - les résultats de stat/choix (6/7/8/9) et la
    /// promotion Homme de main (10-12, "Ce gars est doué") restent du texte descriptif.</summary>
    public bool IsSkillResult => IsHero && int.TryParse(ManualRoll, out var roll) && HeroAdvanceTable.IsSkill(roll);

    /// <summary>Compétence(s) choisie(s) pour ce jet - rattachée(s) au guerrier par
    /// WarbandDetailViewModel.EndOfGame à l'enregistrement, voir PickAdvanceSkill.</summary>
    public ObservableCollection<Skill> SelectedSkills { get; } = new();
    public string SelectedSkillsText => string.Join(", ", SelectedSkills.Select(s => s.Name));

    /// <summary>Pilote l'affichage exclusif bouton "Choisir une compétence" / nom(s) choisi(s) dans le
    /// XAML - une fois une compétence sélectionnée, son nom remplace le bouton plutôt que de
    /// s'afficher à côté.</summary>
    public bool HasSkillSelected => SelectedSkills.Count > 0;

    public AdvanceRollEntry(int index, bool isHero)
    {
        Index = index;
        IsHero = isHero;
        SelectedSkills.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedSkillsText));
            OnPropertyChanged(nameof(HasSkillSelected));
        };
    }
}
