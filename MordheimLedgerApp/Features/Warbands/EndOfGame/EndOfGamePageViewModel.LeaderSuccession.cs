using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape « Nouveau chef » (1.6, livre p. 117 + errata) : juste avant le recrutement, quand la bande n'a plus de
/// chef - mort pendant cette partie (blessures, Fosse, duel...) ou déjà absent. Tous les Héros sont listés (promus de la
/// partie compris) ; l'appli pré-coche celui que désigne le livre (Commandement, puis XP, puis D6 ; ou le successeur
/// imposé du Codex) et le joueur peut en choisir un autre (message ambre, jamais bloquant). Sans successeur possible :
/// avertissement et « Archiver la bande » (fait à Terminer). Les règles viennent de Core.Rules.LeaderSuccessionRules,
/// les écritures de Core.Services.LeaderSuccessionService.</summary>
public partial class EndOfGamePageViewModel
{
    public ObservableCollection<LeaderCandidateRow> LeaderCandidates { get; } = new();

    private SuccessionProposal? _leaderProposal;

    /// <summary>La bande archive à Terminer (choix fait à cette étape, annulable).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArchiveWarbandButtonText), nameof(ArchiveWarbandNote))]
    private bool archiveWarbandAtFinish;

    private IReadOnlyDictionary<int, WarriorArchetype> WarriorArchetypesById =>
        RecruitRows.Select(r => r.Archetype).DistinctBy(a => a.Id).ToDictionary(a => a.Id);

    private WarriorArchetype? ArchetypeOf(Warrior warrior) =>
        warrior.WarriorArchetypeId is { } id ? WarriorArchetypesById.GetValueOrDefault(id) : null;

    private Warrior? LeaderDeadThisBattle =>
        _allActiveWarriorRows.Select(r => r.Warrior).FirstOrDefault(w => w.IsLeader && IsDeadThisBattle(w));

    /// <summary>L'étape s'affiche si la bande a un type Chef mais plus de chef vivant après cette partie.</summary>
    private bool NeedsLeaderSuccession => LeaderSuccessionRules.NeedsSuccession(
        warbandHasLeaderType: RecruitRows.Any(r => r.Archetype.IsLeader),
        hasSurvivingLeader: SurvivingLeader is not null,
        leaderDiedThisGame: LeaderDeadThisBattle is not null,
        rosterHadLeader: _allActiveWarriorRows.Any(r => r.Warrior.IsLeader));

    private bool RequiresDesignatedSuccessor => RecruitRows.Any(r => r.Archetype.IsDesignatedSuccessor);

    /// <summary>Reconstruit les candidats et la proposition à chaque arrivée sur l'étape (une mort ou une promotion a pu
    /// changer entre-temps) ; le choix du joueur est gardé tant que son Héros est toujours candidat.</summary>
    private void BuildLeaderCandidates()
    {
        var previouslySelected = LeaderCandidates.FirstOrDefault(c => c.IsSelected)?.Warrior;
        var previousRolls = LeaderCandidates.Where(c => c.HasTieRoll).ToDictionary(c => c.Warrior, c => c.TieRoll);
        LeaderCandidates.Clear();

        foreach (var row in _allActiveWarriorRows)
        {
            var warrior = row.Warrior;
            if (!LeaderSuccessionRules.IsEligible(warrior) || IsDeadThisBattle(warrior)) continue;
            var outcome = WarriorRows.FirstOrDefault(r => ReferenceEquals(r.Warrior, warrior));
            LeaderCandidates.Add(new LeaderCandidateRow(warrior, row.RoleName, isPromotedThisGame: false,
                ArchetypeOf(warrior)?.IsDesignatedSuccessor == true)
            {
                Experience = warrior.Experience + (outcome is null ? 0 : outcome.ExperienceGained + outcome.ExplorationBonusExperience)
            });
        }
        foreach (var promoted in PromotedHeroRows.Where(r => !r.IsDead))
        {
            LeaderCandidates.Add(new LeaderCandidateRow(promoted.Warrior, promoted.ArchetypeName, isPromotedThisGame: true, isDesignatedSuccessor: false)
            {
                Experience = promoted.Warrior.Experience + promoted.ExperienceGained + promoted.ExplorationBonusExperience
            });
        }

        foreach (var candidate in LeaderCandidates)
            if (previousRolls.TryGetValue(candidate.Warrior, out var roll)) candidate.TieRoll = roll;

        RefreshLeaderProposal(keepSelection: previouslySelected);
    }

    private void RefreshLeaderProposal(Warrior? keepSelection)
    {
        // Indices plutôt qu'Id : un Héros promu n'a pas encore d'Id en base.
        _leaderProposal = LeaderSuccessionRules.Propose(
            LeaderCandidates.Select((c, i) => new SuccessionCandidate(i, c.Leadership, c.Experience, c.IsDesignatedSuccessor)).ToList(),
            RequiresDesignatedSuccessor);

        var tieRolls = Enumerable.Range(0, LeaderCandidates.Count)
            .ToDictionary(i => i, i => int.TryParse(LeaderCandidates[i].TieRoll, out var r) ? r : (int?)null);
        var proposedIndex = LeaderSuccessionRules.ProposedAfterTieRolls(_leaderProposal, tieRolls);

        for (var i = 0; i < LeaderCandidates.Count; i++)
            LeaderCandidates[i].IsProposed = i == proposedIndex;

        var selected = keepSelection is not null ? LeaderCandidates.FirstOrDefault(c => ReferenceEquals(c.Warrior, keepSelection)) : null;
        selected ??= proposedIndex is { } p ? LeaderCandidates[p] : null;
        foreach (var candidate in LeaderCandidates) candidate.IsSelected = ReferenceEquals(candidate, selected);

        NotifyLeaderSuccessionChanged();
    }

    private void NotifyLeaderSuccessionChanged()
    {
        OnPropertyChanged(nameof(LeaderSuccessionIntro));
        OnPropertyChanged(nameof(ShowLeaderTie));
        OnPropertyChanged(nameof(LeaderTieText));
        OnPropertyChanged(nameof(LeaderOffProposalText));
        OnPropertyChanged(nameof(HasLeaderOffProposal));
        OnPropertyChanged(nameof(LeaderCollapseText));
        OnPropertyChanged(nameof(HasLeaderCollapse));
        OnPropertyChanged(nameof(LeaderGainsText));
        OnPropertyChanged(nameof(HasSelectedLeader));
        OnPropertyChanged(nameof(LeaderNoHireNote));
    }

    private LeaderCandidateRow? SelectedLeaderCandidate => LeaderCandidates.FirstOrDefault(c => c.IsSelected);

    private LeaderCandidateRow? ProposedLeaderCandidate => LeaderCandidates.FirstOrDefault(c => c.IsProposed);

    public string LeaderSuccessionIntro
    {
        get
        {
            var dead = LeaderDeadThisBattle;
            var opening = dead is not null
                ? string.Format(Loc["LeaderSuccessionDeadFormat"], dead.Name, ArchetypeOf(dead)?.Name ?? string.Empty)
                : Loc["LeaderSuccessionNoLeader"];
            // Aucun Héros candidat : la carte ambre de l'effondrement explique la suite, pas « Choisissez le Héros… ».
            if (LeaderCandidates.Count == 0) return opening;
            var rule = _leaderProposal?.Kind == SuccessionKind.DesignatedSuccessor
                ? Loc["LeaderSuccessionDesignatedRule"]
                : Loc["LeaderSuccessionBookRule"];
            return $"{opening} {rule}";
        }
    }

    public bool ShowLeaderTie => _leaderProposal?.Kind == SuccessionKind.Tie;

    public string LeaderTieText => ShowLeaderTie
        ? string.Format(Loc["LeaderSuccessionTieFormat"], string.Join(", ", _leaderProposal!.TiedWarriorIds.Select(i => LeaderCandidates[i].Name)))
        : string.Empty;

    [RelayCommand]
    private void RollLeaderTie()
    {
        if (_leaderProposal is not { Kind: SuccessionKind.Tie } proposal) return;
        // Relance jusqu'à départager, comme on le ferait à la table.
        Dictionary<int, int> rolls;
        do rolls = proposal.TiedWarriorIds.ToDictionary(i => i, _ => Random.Shared.Next(1, 7));
        while (LeaderSuccessionRules.ResolveTie(rolls) is null);
        foreach (var (index, roll) in rolls) LeaderCandidates[index].TieRoll = roll.ToString();
        RefreshLeaderProposal(keepSelection: null);
    }

    [RelayCommand]
    private void SelectLeaderCandidate(LeaderCandidateRow candidate)
    {
        foreach (var c in LeaderCandidates) c.IsSelected = ReferenceEquals(c, candidate);
        NotifyLeaderSuccessionChanged();
    }

    public string LeaderOffProposalText
    {
        get
        {
            if (SelectedLeaderCandidate is not { IsProposed: false } chosen || ProposedLeaderCandidate is not { } proposed) return string.Empty;
            return proposed.IsDesignatedSuccessor
                ? string.Format(Loc["LeaderSuccessionOffDesignatedFormat"], proposed.Name, chosen.Name)
                : string.Format(Loc["LeaderSuccessionOffProposalFormat"], proposed.Name, proposed.Leadership, proposed.Experience, chosen.Name);
        }
    }

    public bool HasLeaderOffProposal => !string.IsNullOrEmpty(LeaderOffProposalText);

    public string LeaderCollapseText => _leaderProposal?.Kind switch
    {
        SuccessionKind.NoDesignatedSuccessor => string.Format(Loc["LeaderSuccessionCollapseFormat"],
            RecruitRows.Select(r => r.Archetype).FirstOrDefault(a => a.IsDesignatedSuccessor)?.Name ?? string.Empty),
        SuccessionKind.NoCandidate => Loc["LeaderSuccessionNoCandidate"],
        _ => string.Empty
    };

    public bool HasLeaderCollapse => !string.IsNullOrEmpty(LeaderCollapseText);

    public string ArchiveWarbandButtonText => Loc[ArchiveWarbandAtFinish ? "LeaderSuccessionKeepWarband" : "BtnArchiveWarband"];

    public string ArchiveWarbandNote => ArchiveWarbandAtFinish ? Loc["LeaderSuccessionArchiveNote"] : string.Empty;

    [RelayCommand]
    private void ToggleArchiveWarband() => ArchiveWarbandAtFinish = !ArchiveWarbandAtFinish;

    public bool HasSelectedLeader => SelectedLeaderCandidate is not null;

    /// <summary>« Ilse Vogt reçoit : Chef, Prières de Sigmar. » + la prière au choix à la première progression.</summary>
    public string LeaderGainsText
    {
        get
        {
            if (SelectedLeaderCandidate is not { } chosen) return string.Empty;
            var gains = new List<string> { Loc["LeaderRuleName"] };
            if (SuccessorLearnsMagicFor(chosen.Warrior))
            {
                gains.AddRange(_recruitableWarbandArchetype.MagicSchools.Select(s => s.Name));
                return string.Format(Loc["LeaderSuccessionGainsMagicFormat"], chosen.Name, string.Join(", ", gains));
            }
            return string.Format(Loc["LeaderSuccessionGainsFormat"], chosen.Name, string.Join(", ", gains));
        }
    }

    public string LeaderNoHireNote
    {
        get
        {
            var leaderType = RecruitRows.Select(r => r.Archetype).FirstOrDefault(a => a.IsLeader);
            if (leaderType is null) return string.Empty;
            return leaderType.CanBeRehiredAfterDeath
                ? string.Format(Loc["LeaderSuccessionRehireNoteFormat"], leaderType.Name)
                : string.Format(Loc["LeaderSuccessionNoHireNoteFormat"], leaderType.Name);
        }
    }

    private bool SuccessorLearnsMagicFor(Warrior warrior) => LeaderSuccessionRules.SuccessorLearnsMagic(
        _recruitableWarbandArchetype.SuccessorLearnsMagic, _recruitableWarbandArchetype.MagicSchools.Count > 0,
        ArchetypeOf(warrior)?.IsSpellcaster == true, warrior.CastsAsSuccessor);

    /// <summary>Message ambre de l'étape Recrutement des Héros quand un type Chef est recruté : le livre l'interdit,
    /// sauf un type « Rachetable après sa mort » à partir de la partie qui suit sa mort.</summary>
    public string LeaderRecruitmentNoticeText
    {
        get
        {
            var row = RecruitedHeroRows.FirstOrDefault(r => r.Archetype.IsLeader);
            if (row is null) return string.Empty;
            return LeaderSuccessionRules.RecruitmentNotice(true, row.Archetype.CanBeRehiredAfterDeath, LeaderDeadThisBattle is not null) switch
            {
                LeaderRecruitmentNotice.NewLeaderNotAllowed => string.Format(Loc["RecruitLeaderNotAllowedFormat"], row.Archetype.Name),
                LeaderRecruitmentNotice.RehireOnlyAfterNextGame => string.Format(Loc["RecruitLeaderRehireNextGameFormat"], row.Archetype.Name),
                _ => string.Empty
            };
        }
    }

    public bool HasLeaderRecruitmentNotice => !string.IsNullOrEmpty(LeaderRecruitmentNoticeText);

    /// <summary>Info (pas un avertissement) quand un chef rachetable reprend la tête à son successeur.</summary>
    public string LeaderRehireInfoText
    {
        get
        {
            var row = RecruitedHeroRows.FirstOrDefault(r => r.Archetype.IsLeader && r.Archetype.CanBeRehiredAfterDeath);
            if (row is null || LeaderDeadThisBattle is not null) return string.Empty;
            var acting = SurvivingLeader?.Warrior.Name;
            return acting is null ? string.Empty : string.Format(Loc["RecruitLeaderReplacesActingFormat"], row.Archetype.Name, acting);
        }
    }

    public bool HasLeaderRehireInfo => !string.IsNullOrEmpty(LeaderRehireInfoText);

    private void NotifyLeaderRecruitmentChanged()
    {
        OnPropertyChanged(nameof(LeaderRecruitmentNoticeText));
        OnPropertyChanged(nameof(HasLeaderRecruitmentNotice));
        OnPropertyChanged(nameof(LeaderRehireInfoText));
        OnPropertyChanged(nameof(HasLeaderRehireInfo));
    }

    // --- Terminer ---------------------------------------------------------------------------------------

    private LeaderSuccessionService LeaderSuccession => new(_warbandService);

    /// <summary>Applique le choix de l'étape : le Héros choisi gagne la règle Chef (et la magie de la bande si elle le
    /// prévoit). Après la promotion (un promu a alors son Id) et toutes les morts de la partie.</summary>
    private async Task ApplyLeaderSuccessionAsync(List<string> sentences)
    {
        if (!NeedsLeaderSuccession || SelectedLeaderCandidate is not { } chosen || chosen.Warrior.Id == 0) return;

        var learnsMagic = SuccessorLearnsMagicFor(chosen.Warrior);
        var previousLeaders = _allActiveWarriorRows.Select(r => r.Warrior).Where(w => w.IsLeader).ToList();
        await LeaderSuccession.ApplySuccessionAsync(previousLeaders, chosen.Warrior, learnsMagic, alreadySpellcaster: !learnsMagic);
        sentences.Add(string.Format(Loc["HistoryNewLeaderSentence"], chosen.Name));
    }

    /// <summary>Un chef recruté pendant cette Fin de partie (ex. Vampire racheté) reprend la tête : le successeur en
    /// place perd la règle Chef. Relu en base, une fois toutes les autres écritures faites.</summary>
    private async Task ApplyRehiredLeaderAsync(string language, List<string> sentences)
    {
        if (Warband is null || !RecruitedHeroRows.Any(r => r.Archetype.IsLeader)) return;
        var warriors = await _warbandService.GetWarriorsAsync(Warband.Id, language);
        var leaderArchetypeIds = RecruitRows.Select(r => r.Archetype).Where(a => a.IsLeader).Select(a => a.Id).ToHashSet();
        if (LeaderSuccessionRules.RehiredLeaderToKeep(warriors, leaderArchetypeIds) is not { } newest) return;
        await LeaderSuccession.ApplyRehiredLeaderAsync(warriors.Where(w => w.IsLeader).ToList(), newest);
        sentences.Add(string.Format(Loc["HistoryNewLeaderSentence"], newest.Name));
    }

    private async Task ArchiveWarbandIfChosenAsync(List<string> sentences)
    {
        if (Warband is null || !ArchiveWarbandAtFinish || !NeedsLeaderSuccession) return;
        await LeaderSuccession.ArchiveAsync(Warband, DateTime.Now);
        sentences.Add(Loc["HistoryWarbandArchivedSentence"]);
    }
}
