using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Ligne d'ajout de recrues à un groupe d'Hommes de main DÉJÀ dans le roster (livre des règles,
/// "New Recruits and Existing Henchman Groups") - une par groupe encore actif ce tour
/// (EndOfGameDialogViewModel.WarriorRows, Homme de main). AddCount recrues supplémentaires, plafonnées
/// par le budget vétérans déjà tiré à l'étape Disponibilité des Vétérans (VeteranExperienceRoll) - "you
/// can hire as many warriors as you wish, as long as their combined Experience does not exceed your dice
/// roll" : chaque recrue "coûte" GroupExperience points de ce budget (le groupe entier partage la même
/// Expérience, voir Warrior.Experience). Équipées comme le reste du groupe (CurrentEquipment, jamais un
/// choix libre - "New Henchmen must be armed and equipped in the same way as existing members of the
/// group") : le prix payé pour cet équipement vient en priorité de la réserve de la bande plutôt qu'un
/// achat à neuf (retour utilisateur 2026-09-05 - voir EndOfGameDialogViewModel.GetTopUpBreakdown), sinon
/// le prix plein s'ajoute au coût de recrutement normal du type + la surtaxe "2 CO par point d'Expérience
/// en trop". C'est le SEUL endroit de tout le recrutement où l'équipement se calcule automatiquement
/// (retour utilisateur explicite) - un nouveau groupe (HenchmanRecruitRows) part vide, équipé plus tard
/// via l'étape Achat d'équipement dédiée.</summary>
public partial class ExistingHenchmanTopUp : ObservableObject
{
    public WarriorOutcomeRow Row { get; }
    private readonly WarriorRecruitRow _archetypeRow;

    public string Name => Row.Warrior.Name;

    /// <summary>Expérience du groupe ENTIER (partagée par tous ses membres, livre des règles) - c'est ce
    /// chiffre, PAS le nombre de recrues, que le budget vétérans limite. Warrior.Experience seul seraît la
    /// valeur AVANT cette même Fin de Partie (la mutation réelle n'arrive qu'à Terminer, comme HeadCount -
    /// voir GetTopUpBreakdown) : il faut donc y ajouter l'XP gagnée plus tôt dans CE wizard (étape
    /// Expérience/ExperienceGained, blessure grave/SeriousInjuryBonusExperience, Exploration/
    /// ExplorationBonusExperience) pour refléter l'Expérience RÉELLE du groupe au moment du recrutement de
    /// vétérans - bug signalé 2026-09-21 : un groupe gagnant 5 PX à l'étape Expérience ne voyait jamais
    /// cette surtaxe appliquée ici, restée calculée sur l'ancienne valeur.</summary>
    public int GroupExperience => Row.Warrior.Experience + Row.ExperienceGained + Row.SeriousInjuryBonusExperience + Row.ExplorationBonusExperience;

    /// <summary>Coût de recrutement normal du type (WarriorArchetype.Cost) - résolu via la même
    /// WarriorRecruitRow que la section Nouveau groupe ci-dessous, retrouvée par WarriorArchetypeId à la
    /// construction de cette ligne plutôt que dupliquée ici.</summary>
    public int ArchetypeCost => _archetypeRow.Cost;

    /// <summary>Id du WarriorArchetype de ce groupe - permet à EndOfGameDialogViewModel.
    /// IncrementHenchmanTopUp de retrouver l'effectif déjà recruté de CE type (ExistingCountForArchetype)
    /// pour appliquer WarriorArchetype.MaxCount, retour utilisateur 2026-09-22 - "selon le type de
    /// henchmen... on a une limite de personnage max parfois" (jusque-là seul le budget XP bloquait
    /// l'incrément, jamais MaxCount ni MaxWarriors).</summary>
    public int WarriorArchetypeId => _archetypeRow.Archetype.Id;

    /// <summary>Plafond de ce type (WarriorArchetype.MaxCount, null = pas de plafond) - voir
    /// WarriorArchetypeId's own doc.</summary>
    public int? MaxCountForType => _archetypeRow.Archetype.MaxCount;

    /// <summary>Effectif de ce type ailleurs dans la bande (poussé une fois par
    /// EndOfGameDialogViewModel.BuildExistingHenchmanTopUps juste après la construction, même idiome que
    /// WarriorRecruitRow.ExternalHeadCount - ne bouge jamais pendant cette étape, seul AddCount change) -
    /// permet CountDisplay ("X/Y", même format que WarriorRecruitRow.CountDisplay), retour utilisateur
    /// 2026-09-22 - "ça serait top si l'affichage on avait la même chose qu'au recrutement des
    /// recrues".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountDisplay))]
    private int existingCountForType;

    public string CountDisplay => $"{ExistingCountForType + AddCount}/{(MaxCountForType?.ToString() ?? "∞")}";

    public IReadOnlyList<WarriorEquipment> CurrentEquipment => Row.Warrior.Equipment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddCount))]
    [NotifyPropertyChangedFor(nameof(CountDisplay))]
    private int addCount;

    /// <summary>Pilote l'affichage du détail de coût (AddCount == 0 -&gt; rien à montrer) - IsNotNullConverter
    /// ne convient pas ici (AddCount est un int, jamais null).</summary>
    public bool HasAddCount => AddCount > 0;

    /// <summary>Détail de coût "poussé" par EndOfGameDialogViewModel.RefreshHenchmanTopUpBreakdowns
    /// (appelée après tout Increment/Decrement/à la construction) plutôt que calculé ici à la demande - ce
    /// calcul dépend de TOUS les ExistingHenchmanTopUps à la fois (réserve partagée, consommée dans
    /// l'ordre, voir EndOfGameDialogViewModel.GetTopUpBreakdown), pas seulement de cette ligne, donc n'a
    /// pas sa place comme simple propriété calculée ici. Même idiome "push" que WarriorRecruitRow.
    /// CanIncrement (UpdateRecruitRowsEligibility). Vide par défaut (avant tout Increment).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StashItemsUsedDisplay))]
    private HenchmanTopUpCostBreakdown breakdown = new(0, Array.Empty<HenchmanEquipmentCostLine>(), 0, 0);

    public string StashItemsUsedDisplay => Breakdown.HasStashSavings
        ? string.Format(LocalizationService.Instance["EndOfGameRecruitStashUsedFormat"], Breakdown.StashItemsUsed)
        : string.Empty;

    public ExistingHenchmanTopUp(WarriorOutcomeRow row, WarriorRecruitRow archetypeRow)
    {
        Row = row;
        _archetypeRow = archetypeRow;
    }
}

/// <summary>Une ligne du détail de coût affiché sous chaque ExistingHenchmanTopUp (retour utilisateur
/// 2026-09-05 - "il faut détailler le calcul au recrutement de vétéran") : un type d'équipement du
/// groupe, combien il en faut au total pour les recrues ajoutées, combien viennent de la réserve (donc
/// gratuits) et le coût réel de ce qui reste à acheter. Voir EndOfGameDialogViewModel.GetTopUpBreakdown.</summary>
public sealed record HenchmanEquipmentCostLine(string Name, int Quantity, int Cost, int StashUsed);

/// <summary>Détail complet du coût d'UN ExistingHenchmanTopUp - recrutement (ArchetypeCost × AddCount),
/// une ligne par type d'équipement du groupe, et la surtaxe d'Expérience (livre des règles, "2 gold
/// crowns... for each extra Experience point"). Total additionne tout.</summary>
public sealed record HenchmanTopUpCostBreakdown(int RecruitmentCost, IReadOnlyList<HenchmanEquipmentCostLine> EquipmentLines, int SurchargeCost, int StashItemsUsed)
{
    public int Total => RecruitmentCost + EquipmentLines.Sum(l => l.Cost) + SurchargeCost;
    public bool HasStashSavings => StashItemsUsed > 0;
}
