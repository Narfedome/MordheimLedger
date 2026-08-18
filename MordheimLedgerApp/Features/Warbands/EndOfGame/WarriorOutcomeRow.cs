using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One row per active Warrior in the End of Game dialog — collects the outcome, the caller
/// (WarbandDetailViewModel) applies it via IWarbandService and builds the History sentence.</summary>
public partial class WarriorOutcomeRow : ObservableObject
{
    private readonly Dictionary<string, WarriorStatus> _statusByLabel = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public Warrior Warrior { get; }
    public string Name => Warrior.Name;

    /// <summary>The archetype's name (e.g. "Zombie", "Capitaine") shown instead of a plain Héros/Homme
    /// de main label on every step of this wizard - same value as WarriorRow.RoleName, passed in by
    /// the caller (WarbandDetailViewModel.EndOfGame already has it resolved for the roster).</summary>
    public string ArchetypeName { get; }

    /// <summary>Copie de Warrior.GainsExperience (donc, en amont, WarriorArchetype.GainsExperience -
    /// false pour les types comme Zombie, voir sa doc). Exclut ce guerrier de l'étape Expérience
    /// (ShowsInExperienceStep) et donc, en cascade, de la Progression (HasMilestone).</summary>
    public bool GainsExperience { get; }

    /// <summary>Un guerrier mort (étape Blessure) ou qui ne gagne jamais d'XP n'a rien à faire à
    /// l'étape Expérience - retiré de la liste plutôt que juste grisé/désactivé.</summary>
    public bool ShowsInExperienceStep => !IsDead && GainsExperience;

    /// <summary>Saisie libre du PX gagné cette partie (scénario + survie + bonus outsider - non calculé,
    /// voir la doc de classe d'EndOfGameDialogViewModel) - texte plutôt qu'int pour que le champ parte
    /// vide au lieu d'afficher "0" par défaut, même motif que ManualRoll plus bas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ExperienceGained))]
    [NotifyPropertyChangedFor(nameof(MilestoneCount))]
    [NotifyPropertyChangedFor(nameof(HasMilestone))]
    private string experienceGainedText = string.Empty;

    partial void OnExperienceGainedTextChanged(string value) => SyncAdvanceRolls();

    public int ExperienceGained => int.TryParse(ExperienceGainedText, out var xp) ? xp : 0;

    public bool IsHero => Warrior.IsHero;
    public int HeadCount => Warrior.HeadCount;

    /// <summary>Nombre de figurines hors de combat à la fin de la partie - toujours 0 ou 1 pour un Héros
    /// (HeadCount vaut 1), mais peut monter jusqu'à HeadCount pour un groupe d'Hommes de main : chaque
    /// figurine hors de combat a son propre jet de Blessure Grave (règle confirmée, voir
    /// SyncFigureInjuryRolls) - pas un seul jet pour tout le groupe. Piloté par le stepper +/- de
    /// l'étape "Hors de combat" (IncrementOutOfAction/DecrementOutOfAction) pour un groupe, par la case
    /// à cocher IsOutOfAction pour un Héros.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutOfAction))]
    [NotifyPropertyChangedFor(nameof(OutOfActionLabel))]
    private int outOfActionCount;

    /// <summary>Wrapper booléen pour la case à cocher d'un Héros (HeadCount toujours 1, donc 0/1
    /// suffit) - un groupe d'Hommes de main utilise directement OutOfActionCount via le stepper, jamais
    /// cette case.</summary>
    public bool IsOutOfAction
    {
        get => OutOfActionCount > 0;
        set => OutOfActionCount = value ? 1 : 0;
    }

    public string OutOfActionLabel => $"{OutOfActionCount}/{HeadCount}";

    /// <summary>OutOfActionCount change (case cochée/décochée pour un Héros, stepper +/- pour un groupe
    /// d'Hommes de main) : ce guerrier n'a plus d'étape Blessure dans le wizard si la valeur retombe à 0
    /// (voir EndOfGameDialogViewModel.Steps), donc plus rien à y montrer. Héros et groupe divergent
    /// ensuite : un Héros efface son unique jet (ManualRoll/InjuryResultText/Blessures multiples/statut
    /// dérivé) ; un groupe resynchronise juste FigureInjuryRolls sur le nouveau compte (peut aussi
    /// grandir, contrairement au cas Héros qui ne vaut jamais plus que 1).</summary>
    partial void OnOutOfActionCountChanged(int value)
    {
        if (Warrior.IsHero)
        {
            if (value > 0) return;

            ManualRoll = string.Empty;
            InjuryResultText = string.Empty;
            MultipleInjuryCountRoll = string.Empty;
            if (MultipleInjuryRolls.Count > 0)
            {
                MultipleInjuryRolls.Clear();
                OnPropertyChanged(nameof(HasMultipleInjuryRolls));
            }
            SelectedStatusLabel = _statusByLabel.First(kv => kv.Value == Warrior.Status).Key;
        }
        else
        {
            SyncFigureInjuryRolls();
        }
    }

    /// <summary>Le score D66 (Héros) ou D6 (Homme de main) - saisi à la main (jet physique) ou rempli
    /// par AutoRoll. Dès que la valeur est un jet complet et valide, InjuryResultText se résout tout
    /// seul (OnManualRollChanged) - pas de bouton "Voir le résultat" à cliquer après une saisie
    /// physique, décision explicite du 2026-08-17.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMultipleInjuriesSection))]
    private string manualRoll = string.Empty;

    /// <summary>Message affiché sous le champ ManualRoll si le joueur essaie de passer à l'étape
    /// suivante (EndOfGameDialogViewModel.Next) sans jet valide - jamais posé par une simple frappe,
    /// seulement par cette validation ; effacé dès que le jet redevient valide (ci-dessous), pas
    /// seulement au prochain essai de Suivant.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        InjuryResultText = string.Empty;
        if (int.TryParse(value, out var roll)) ResolveInjuryResult(roll);
        if (!string.IsNullOrWhiteSpace(InjuryResultText)) RollError = null;

        // Si le jet principal est refait vers un résultat qui n'est plus "Blessures multiples", les
        // sous-jets précédemment saisis n'ont plus de sens - on les efface plutôt que de les laisser
        // affichés pour un résultat qui ne les déclenche plus.
        if (!Warrior.IsHero || ShowMultipleInjuriesSection) return;
        if (MultipleInjuryCountRoll.Length == 0 && MultipleInjuryRolls.Count == 0) return;

        MultipleInjuryCountRoll = string.Empty;
        MultipleInjuryRolls.Clear();
        OnPropertyChanged(nameof(HasMultipleInjuryRolls));
        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>Le jet/la classification (mort ou non) vivent dans Core.Rules, sans dépendance à la
    /// localisation - cette résolution de clé -> texte affiché reste ici côté tête MAUI (WarriorOutcomeRow
    /// a déjà accès à LocalizationService via _loc, comme pour SelectedStatusLabel).</summary>
    private void ResolveInjuryResult(int roll)
    {
        bool found;
        string key;
        found = Warrior.IsHero ? SeriousInjuryTable.TryGetTextKey(roll, out key) : HenchmanInjuryTable.TryGetTextKey(roll, out key);
        if (!found) return;

        InjuryResultText = _loc[key];
        ApplyInjuryRoll(roll);
    }

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

    /// <summary>True dès que le jet principal (ManualRoll) donne "Blessures multiples" (16, 21,
    /// Héros uniquement) - pilote l'affichage du bloc "combien de sous-jets" (1D6) dans le XAML, avant
    /// même que ce 1D6 ait été tiré.</summary>
    public bool ShowMultipleInjuriesSection => Warrior.IsHero && int.TryParse(ManualRoll, out var roll) && SeriousInjuryTable.IsMultipleInjuries(roll);

    /// <summary>Le score du 1D6 tiré pour savoir combien de sous-jets faire - saisi à la main ou
    /// rempli par AutoRollMultipleInjuryCount. Dès que la valeur est 1 à 6, MultipleInjuryRolls se
    /// peuple tout seul (OnMultipleInjuryCountRollChanged), même principe que ManualRoll ci-dessus.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string multipleInjuryCountRoll = string.Empty;

    /// <summary>Même principe que RollError, pour le champ 1D6 "combien de sous-jets" plutôt que le jet
    /// principal.</summary>
    [ObservableProperty]
    private string? multipleInjuryCountError;

    partial void OnMultipleInjuryCountRollChanged(string value)
    {
        if (int.TryParse(value, out var count) && count is >= 1 and <= 6)
        {
            PopulateMultipleInjuryRolls(count);
            MultipleInjuryCountError = null;
        }
    }

    /// <summary>Un sous-jet D66 par point du 1D6 ci-dessus - peuplée par SetMultipleInjuryCount une
    /// fois ce 1D6 résolu. Vide tant qu'il ne l'est pas, et pour tout guerrier/résultat qui n'est pas
    /// concerné par les Blessures multiples.</summary>
    public ObservableCollection<InjurySubRollEntry> MultipleInjuryRolls { get; } = new();
    public bool HasMultipleInjuryRolls => MultipleInjuryRolls.Count > 0;

    /// <summary>Un jet D6 par figurine hors de combat dans un groupe d'Hommes de main (OutOfActionCount)
    /// - sans objet pour un Héros, qui utilise ManualRoll/InjuryResultText ci-dessus à la place (une
    /// seule figurine, un seul jet). Peuplée/resynchronisée par SyncFigureInjuryRolls à chaque
    /// changement d'OutOfActionCount.</summary>
    public ObservableCollection<InjurySubRollEntry> FigureInjuryRolls { get; } = new();

    /// <summary>Plus de saisie manuelle : uniquement modifié par ApplyInjuryRoll (résultat "Mort",
    /// jets 11-15) ou remis à l'état d'origine par OnIsOutOfActionChanged. Reste sur Warrior.Status
    /// (donc "Actif") pour tout le reste, y compris les rétablissements et le résultat "Blessures
    /// multiples" (16/21, ambigu tant que les sous-jets ne sont pas résolus).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(IsDead))]
    [NotifyPropertyChangedFor(nameof(ShowsInExperienceStep))]
    private string selectedStatusLabel = string.Empty;

    public WarriorStatus Status => _statusByLabel.GetValueOrDefault(SelectedStatusLabel, Warrior.Status);
    public bool IsDead => Status == WarriorStatus.Dead;

    /// <summary>Nombre de cases à bord épais franchies par les PX gagnés cette partie (voir
    /// ExperienceMilestones) - peut dépasser 1 si le guerrier cumule assez de PX pour sauter
    /// plusieurs paliers d'un coup, d'où AdvanceRolls plutôt qu'un jet unique.</summary>
    public int MilestoneCount => ExperienceMilestones.MilestonesCrossedCount(Warrior.IsHero, Warrior.Experience, Warrior.Experience + ExperienceGained);
    public bool HasMilestone => GainsExperience && MilestoneCount > 0;

    /// <summary>Héros et Hommes de main utilisent deux tables de Blessures Graves totalement
    /// différentes (D66 vs D6, voir SeriousInjuryTable/HenchmanInjuryTable) - ce placeholder garde la
    /// distinction visible sur l'étape Blessure de chaque guerrier (ArchetypeName, affiché à côté,
    /// n'en dit rien explicitement).</summary>
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
            foreach (var sub in MultipleInjuryRolls)
                if (!string.IsNullOrWhiteSpace(sub.InjuryResultText)) parts.Add(sub.InjuryResultText);
            foreach (var figure in FigureInjuryRolls)
                if (!string.IsNullOrWhiteSpace(figure.InjuryResultText)) parts.Add(figure.InjuryResultText);
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

    public WarriorOutcomeRow(Warrior warrior, string archetypeName, bool gainsExperience)
    {
        Warrior = warrior;
        ArchetypeName = archetypeName;
        GainsExperience = gainsExperience;

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
        if (isDeath)
            SelectedStatusLabel = _statusByLabel.First(kv => kv.Value == WarriorStatus.Dead).Key;
    }

    /// <summary>(Re)peuple MultipleInjuryRolls avec exactement <paramref name="count"/> sous-jets (le
    /// résultat du 1D6 tiré pour savoir combien en faire, cf. règle "Blessures multiples") - remplace
    /// entièrement les entrées précédentes, un nouveau tirage du 1D6 recommence à zéro plutôt que de
    /// s'ajouter aux sous-jets déjà saisis. N'écrit pas MultipleInjuryCountRoll lui-même : appelée
    /// depuis OnMultipleInjuryCountRollChanged, qui réagit déjà à ce champ.</summary>
    private void PopulateMultipleInjuryRolls(int count)
    {
        MultipleInjuryRolls.Clear();
        for (var i = 1; i <= count; i++)
        {
            var entry = new InjurySubRollEntry(i, count, isHero: true, labelKey: "EndOfGameMultipleInjuryLabel");
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            MultipleInjuryRolls.Add(entry);
        }

        OnPropertyChanged(nameof(HasMultipleInjuryRolls));
    }

    /// <summary>Ajuste FigureInjuryRolls pour qu'il compte exactement un jet D6 par figurine hors de
    /// combat (OutOfActionCount), en préservant les jets déjà faits quand le nombre ne diminue pas -
    /// même principe que SyncAdvanceRolls/PopulateMultipleInjuryRolls, mais additif comme SyncAdvanceRolls
    /// (pas de reset complet) puisque le joueur peut ajuster le stepper dans un sens ou dans l'autre
    /// avant de lancer les dés. Total est mis à jour sur les entrées déjà là (label "Figurine i/N") au
    /// lieu d'être figé à leur création, contrairement à AdvanceRollEntry/InjurySubRollEntry des
    /// Blessures multiples dont le total ne varie jamais après coup.</summary>
    private void SyncFigureInjuryRolls()
    {
        while (FigureInjuryRolls.Count < OutOfActionCount)
        {
            var entry = new InjurySubRollEntry(FigureInjuryRolls.Count + 1, OutOfActionCount, isHero: false, labelKey: "EndOfGameFigureLabel");
            entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
            FigureInjuryRolls.Add(entry);
        }
        while (FigureInjuryRolls.Count > OutOfActionCount)
            FigureInjuryRolls.RemoveAt(FigureInjuryRolls.Count - 1);

        foreach (var entry in FigureInjuryRolls)
            entry.UpdateTotal(OutOfActionCount);
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
