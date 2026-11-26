using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Un Héros qui peut prendre la tête de la bande, à l'étape « Nouveau chef » (1.6). Voir
/// EndOfGamePageViewModel.LeaderSuccession.</summary>
public partial class LeaderCandidateRow : ObservableObject
{
    public LeaderCandidateRow(Warrior warrior, string roleName, bool isPromotedThisGame, bool isDesignatedSuccessor)
    {
        Warrior = warrior;
        RoleName = roleName;
        IsPromotedThisGame = isPromotedThisGame;
        IsDesignatedSuccessor = isDesignatedSuccessor;
    }

    public Warrior Warrior { get; }
    public string Name => Warrior.Name;
    public string RoleName { get; }
    public bool IsPromotedThisGame { get; }
    public bool IsDesignatedSuccessor { get; }
    public int Leadership => Warrior.Leadership;

    /// <summary>XP à la fin de cette partie (celle de la bataille comprise) - c'est elle qui départage.</summary>
    [ObservableProperty]
    private int experience;

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Proposé par l'appli (Commandement, puis XP, puis D6 ; ou successeur imposé).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private bool isProposed;

    /// <summary>Jet de D6 de départage, vide tant qu'il n'a pas été lancé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTieRoll))]
    private string tieRoll = string.Empty;

    public bool HasTieRoll => !string.IsNullOrEmpty(TieRoll);

    public string LeadershipText => string.Format(Services.LocalizationService.Instance["LeaderCandidateLeadershipFormat"], Leadership);

    public string ExperienceText => string.Format(Services.LocalizationService.Instance["LeaderCandidateExperienceFormat"], Experience);

    partial void OnExperienceChanged(int value) => OnPropertyChanged(nameof(ExperienceText));

    public string SubtitleText
    {
        get
        {
            var loc = Services.LocalizationService.Instance;
            var parts = new List<string> { RoleName };
            if (IsPromotedThisGame) parts.Add(loc["LeaderCandidatePromoted"]);
            if (IsDesignatedSuccessor) parts.Add(loc["LeaderCandidateDesignated"]);
            else if (IsProposed) parts.Add(loc["LeaderCandidateProposed"]);
            return string.Join(" · ", parts);
        }
    }
}
