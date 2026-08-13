using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Resources.Icons;
using System.Collections.ObjectModel;
using System.Linq;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Un slot de nom pour une recrue Héros (WarriorRecruitRow.NameSlots) - une classe dédiée plutôt
/// qu'un ObservableCollection&lt;string&gt; nu, parce qu'un Entry lié en TwoWay à un élément de collection
/// ne peut pas réécrire une string immuable en place (voir WarriorRecruitListView.xaml).</summary>
public partial class WarriorNameSlot : ObservableObject
{
    /// <summary>Référence à la ligne parente - nécessaire à l'étape Équipement pour savoir quel
    /// EquipmentListId/WarriorArchetypeId filtrer au picker quand on achète pour CE héros précis.</summary>
    public WarriorRecruitRow Row { get; }

    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>Étiquette affichée à l'étape Équipement (avant que Name ne soit renseigné à l'étape Noms) -
    /// juste le nom d'archétype si ce héros est seul de son type, suffixé d'un numéro (1, 2...) s'il faut
    /// le distinguer d'autres héros du même type. Maintenue par WarbandEditDialogViewModel.
    /// RenumberHeroLabels (appelée après tout Increment/DecrementWarrior) plutôt que calculée ici, ce slot
    /// n'ayant pas de visibilité sur ses frères dans Row.NameSlots.</summary>
    [ObservableProperty]
    private string archetypeLabel = string.Empty;

    /// <summary>Équipement propre à cette recrue Héros - contrairement aux Hommes de main (voir
    /// HenchmanGroup.Equipment), chaque héros peut avoir un équipement différent (livre des règles :
    /// "Every model in each Henchman group must be armed and armoured in the same way", ce qui ne
    /// s'applique pas aux héros).</summary>
    public ObservableCollection<EquipmentPick> Equipment { get; } = new();

    /// <summary>Repère visuel affiché à l'étape Noms (à côté du champ Name, une fois compté/équipé) pour
    /// distinguer les recrues sans devoir revenir à l'étape Équipement - snapshot au moment du binding,
    /// pas de notification live nécessaire (l'équipement est déjà figé une fois l'étape Noms atteinte).</summary>
    public string EquipmentSummary => string.Join(", ", Equipment.Select(e => e.Name));

    /// <summary>Compétences déjà apprises - uniquement peuplé/affiché en mode "Bande existante"
    /// (WarbandEditDialogViewModel.IsExistingWarband), pour importer une bande déjà jouée sur papier.
    /// Persisté au Save() via WarbandService.AddWarriorSkillAsync, une fois le Warrior recruté (aucun
    /// WarriorId réel avant, même raison que EquipmentPick).</summary>
    public ObservableCollection<Skill> Skills { get; } = new();

    /// <summary>XP de cette recrue - uniquement modifiable en mode "Bande existante" (sinon reste la
    /// StartingExperience de l'archétype, comme WarriorArchetype.ToWarrior l'applique déjà). Persisté au
    /// Save() par une mise à jour explicite du Warrior fraîchement recruté (RecruitWarriorAsync applique
    /// toujours StartingExperience en premier).</summary>
    [ObservableProperty]
    private int experience;

    /// <summary>Sous-onglet actif en mode Bande existante (0=Équipement, 1=Compétences, 2=XP) - même
    /// composant TabToggleButton que WarriorEditDialog's Équipement/Compétences/Blessures, mais local à
    /// CE slot plutôt qu'à tout le dialog (chaque héros a le sien). Sans effet hors mode Bande existante,
    /// où seul le panneau Équipement est affiché sans onglets.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEquipmentSection))]
    [NotifyPropertyChangedFor(nameof(IsSkillsSection))]
    [NotifyPropertyChangedFor(nameof(IsXpSection))]
    private int selectedSection;

    public bool IsEquipmentSection => SelectedSection == 0;
    public bool IsSkillsSection => SelectedSection == 1;
    public bool IsXpSection => SelectedSection == 2;

    [RelayCommand]
    private void ShowEquipmentSection() => SelectedSection = 0;

    [RelayCommand]
    private void ShowSkillsSection() => SelectedSection = 1;

    [RelayCommand]
    private void ShowXpSection() => SelectedSection = 2;

    public WarriorNameSlot(WarriorRecruitRow row)
    {
        Row = row;
        experience = row.Archetype.StartingExperience;
        archetypeLabel = row.Archetype.Name;
    }
}

/// <summary>Un sous-groupe nommé d'Hommes de main d'un même WarriorRecruitRow, avec son propre
/// équipement partagé (livre des règles : "Every model in each Henchman group must be armed and
/// armoured in the same way" - dans le même groupe, pas forcément dans tout le type recruté). Un type
/// recruté à l'effectif N démarre avec un seul HenchmanGroup (Count = N, nom = celui de l'archétype) -
/// WarbandEditDialogViewModel.SplitHenchmanGroup permet d'en détacher un second si le joueur veut des
/// équipements différents au sein du même type (ex. 3 Verminkin à l'épée + 2 au gourdin = 2 groupes
/// distincts, chacun avec son propre nom - voir "warband roster" dans le livre des règles : chaque
/// groupe d'Hommes de main doit avoir un nom).</summary>
public partial class HenchmanGroup : ObservableObject
{
    /// <summary>Référence à la ligne parente - même rôle que WarriorNameSlot.Row.</summary>
    public WarriorRecruitRow Row { get; }

    [ObservableProperty]
    private string name;

    /// <summary>Combien de guerriers de ce type appartiennent à CE groupe - la somme des Count de tous
    /// les HenchmanGroups d'une ligne doit toujours égaler WarriorRecruitRow.Count, maintenu par
    /// IncrementWarrior/DecrementWarrior (qui ne touchent jamais que le dernier groupe) et
    /// SplitHenchmanGroup (qui transfère des unités du groupe source vers un nouveau groupe).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSplit))]
    private int count;

    /// <summary>Le bouton Diviser n'a de sens que s'il reste au moins 2 guerriers à répartir entre le
    /// groupe existant et un nouveau.</summary>
    public bool CanSplit => Count > 1;

    public ObservableCollection<EquipmentPick> Equipment { get; } = new();

    /// <summary>Voir WarriorNameSlot.Skills - même staging mémoire, appliqué à tout le sous-groupe
    /// (comme Equipment).</summary>
    public ObservableCollection<Skill> Skills { get; } = new();

    /// <summary>Voir WarriorNameSlot.Experience - appliquée à tout le sous-groupe (comme Equipment/Skills).</summary>
    [ObservableProperty]
    private int experience;

    /// <summary>Voir WarriorNameSlot.SelectedSection - même sous-onglets, un état par groupe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEquipmentSection))]
    [NotifyPropertyChangedFor(nameof(IsSkillsSection))]
    [NotifyPropertyChangedFor(nameof(IsXpSection))]
    private int selectedSection;

    public bool IsEquipmentSection => SelectedSection == 0;
    public bool IsSkillsSection => SelectedSection == 1;
    public bool IsXpSection => SelectedSection == 2;

    [RelayCommand]
    private void ShowEquipmentSection() => SelectedSection = 0;

    [RelayCommand]
    private void ShowSkillsSection() => SelectedSection = 1;

    [RelayCommand]
    private void ShowXpSection() => SelectedSection = 2;

    public HenchmanGroup(WarriorRecruitRow row, string name, int count)
    {
        Row = row;
        this.name = name;
        this.count = count;
        experience = row.Archetype.StartingExperience;
    }
}

/// <summary>Une ligne du nouvel écran de recrutement (WarriorRecruitListView) : un WarriorArchetype
/// recrutable pour la bande en cours de création, avec le nombre déjà choisi et un slot de nom par
/// recrue Héros. Purement de la présentation - WarbandEditDialogViewModel.Save() aplati ces lignes en
/// vrais Warrior (via WarriorArchetype.ToWarrior) au moment de persister, rien avant.</summary>
public partial class WarriorRecruitRow : ObservableObject
{
    public WarriorArchetype Archetype { get; }

    /// <summary>Lu par ChipView (Grid.BindingContext = Item = cette ligne) pour afficher le nom.</summary>
    public string Name => Archetype.Name;
    public int Cost => Archetype.Cost;
    public bool IsHero => Archetype.IsHero;
    public string IconGlyph => IsHero ? SolidFont.Crown : SolidFont.Users;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountDisplay))]
    [NotifyPropertyChangedFor(nameof(CanDecrement))]
    private int count;

    /// <summary>Ne dépend que de cette ligne (contrairement à CanIncrement) - pas besoin d'être
    /// recalculée par UpdateRecruitability, NotifyPropertyChangedFor sur Count suffit.</summary>
    public bool CanDecrement => Count > 0;

    /// <summary>Un slot par recrue Héros déjà comptée (Count) - une Entry par slot dans
    /// WarriorRecruitListView. Vide pour les Hommes de main (anonymes, pas de nom individuel).</summary>
    public ObservableCollection<WarriorNameSlot> NameSlots { get; } = new();

    /// <summary>Sous-groupes d'Hommes de main de ce type - vide pour les Héros, qui utilisent NameSlots à
    /// la place. Un seul groupe par défaut (tout le Count), plusieurs si le joueur a divisé pour donner
    /// des équipements différents à une partie du type recruté - voir HenchmanGroup.</summary>
    public ObservableCollection<HenchmanGroup> HenchmanGroups { get; } = new();

    public string CountDisplay => $"{Count}/{(Archetype.MaxCount?.ToString() ?? "∞")}";

    /// <summary>Recalculée par WarbandEditDialogViewModel après chaque changement (budget/effectif
    /// bande dépendent des AUTRES lignes, pas seulement de celle-ci) - voir UpdateRecruitability.</summary>
    [ObservableProperty]
    private bool canIncrement = true;

    public WarriorRecruitRow(WarriorArchetype archetype)
    {
        Archetype = archetype;
    }
}
