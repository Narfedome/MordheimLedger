using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One 2D6 progression roll for one milestone crossed by a WarriorOutcomeRow - see
/// WarriorOutcomeRow.AdvanceRolls/SyncAdvanceRolls (a warrior can cross several milestones in the
/// same End of Game, each needing its own independent roll).</summary>
public partial class AdvanceRollEntry : ObservableObject
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public int Index { get; }
    public bool IsHero { get; }
    public string Label => string.Format(_loc["EndOfGameMilestoneLabel"], Index);

    /// <summary>Le score 2D6 - saisi à la main (jet physique) ou rempli par AutoRollAdvance. Dès que la
    /// valeur est un jet complet et valide, ResultText se résout tout seul (OnManualRollChanged).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkillResult))]
    private string manualRoll = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.RollError, posé uniquement par
    /// EndOfGameDialogViewModel.Next si ce jet est encore vide/invalide à ce moment-là.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        ResultText = string.Empty;
        if (!int.TryParse(value, out var roll)) return;

        bool found;
        string key;
        found = IsHero ? HeroAdvanceTable.TryGetTextKey(roll, out key) : HenchmanAdvanceTable.TryGetTextKey(roll, out key);
        if (found)
        {
            ResultText = _loc[key];
            RollError = null;
        }
    }

    /// <summary>Texte descriptif du résultat une fois résolu - purement informatif, voir
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
