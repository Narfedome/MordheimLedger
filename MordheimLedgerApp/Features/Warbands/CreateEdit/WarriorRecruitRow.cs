using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Resources.Icons;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Base commune à WarriorNameSlot (un héros) et HenchmanGroupDraft (un sous-groupe d'Hommes de
/// main) - les deux portent le même équipement/compétences/XP en staging mémoire et les mêmes
/// sous-onglets Équipement/Compétences/XP (WarbandEditDialog.xaml, mode Bande existante), donc
/// factorisés ici plutôt que dupliqués deux fois. Ce que chaque sous-classe garde de propre : le nom
/// (Name, pas ici - WarriorNameSlot et HenchmanGroupDraft le typent différemment : personnel vs de groupe) et
/// ce qui n'a de sens que pour l'un des deux (ArchetypeLabel pour les héros, Count/CanSplit pour les
/// groupes).</summary>
public abstract partial class RecruitSlot : ObservableObject
{
    /// <summary>Référence à la ligne parente - nécessaire à l'étape Équipement pour savoir quel
    /// EquipmentListId/WarriorArchetypeId filtrer au picker quand on achète pour CE slot précis.</summary>
    public WarriorRecruitRow Row { get; }

    /// <summary>Contrairement aux Hommes de main (équipement partagé au sein d'un HenchmanGroupDraft - livre
    /// des règles : "Every model in each Henchman group must be armed and armoured in the same way"),
    /// chaque héros peut avoir un équipement différent - cette collection sert donc les deux cas, propre
    /// à CE slot/groupe précis.</summary>
    public ObservableCollection<EquipmentPick> Equipment { get; } = new();

    /// <summary>Repère visuel affiché à l'étape Noms (à côté du champ Name, une fois compté/équipé) pour
    /// distinguer les recrues sans devoir revenir à l'étape Équipement - snapshot au moment du binding,
    /// pas de notification live nécessaire (l'équipement est déjà figé une fois l'étape Noms atteinte).
    /// Commun aux héros (WarriorNameSlot) et groupes d'Hommes de main (HenchmanGroupDraft), tous deux
    /// portant leur propre Equipment - chaîne vide (jamais null) tant que rien n'est acheté.</summary>
    public string EquipmentSummary => string.Join(", ", Equipment.Select(e => e.Name));

    /// <summary>Passe-plat vers Row.Archetype.CanUseEquipment - masque le bouton "+" équipement (les deux
    /// modes, avec ou sans sous-onglets) pour un type qui n'en porte jamais (Zombie, Rat Ogre...).</summary>
    public bool CanUseEquipment => Row.Archetype.CanUseEquipment;

    /// <summary>Compétences déjà apprises - uniquement peuplé/affiché en mode "Bande existante"
    /// (WarbandEditDialogViewModel.IsExistingWarband), pour importer une bande déjà jouée sur papier.
    /// Persisté au Save() via WarbandService.AddWarriorSkillAsync, une fois le Warrior recruté (aucun
    /// WarriorId réel avant, même raison que EquipmentPick).</summary>
    public ObservableCollection<Skill> Skills { get; } = new();

    /// <summary>Sorts déjà appris - même principe que Skills (mode "Bande existante" uniquement), mais
    /// n'a de sens que si Row.Archetype.IsSpellcaster (voir IsSpellcaster/onglet Sorts masqué sinon dans
    /// RecruitSlotTabsView). Persisté au Save() via WarbandService.AddWarriorSpellAsync. Hors mode Bande
    /// existante, alimenté par WarbandEditDialogViewModel.ApplyStartingSpell (tirage 1D6, livre des
    /// règles) plutôt qu'un choix libre - voir HasSpells pour le plafond à un seul sort de départ.</summary>
    public ObservableCollection<Spell> Spells { get; } = new();

    /// <summary>Pilote l'affichage exclusif ligne de saisie du jet / puce du sort tiré, hors mode Bande
    /// existante (WarbandEditDialog.xaml) - un lanceur de sorts fraîchement recruté ne débute qu'avec un
    /// seul sort (livre des règles), retirer la puce (RemoveSpellCommand) permet de relancer.</summary>
    public bool HasSpells => Spells.Count > 0;

    /// <summary>Saisie du jet 1D6 pour le sort de départ - même idiome que AdvanceRollEntry.ManualRoll
    /// (EndOfGameDialog) : le joueur tape le résultat d'un dé physique, ou laisse
    /// WarbandEditDialogViewModel.AutoRollSpell (bouton dé) le remplir au hasard. Rien n'est appliqué à
    /// la frappe - ApplyStartingSpell (bouton coche) résout le sort correspondant et l'ajoute à Spells.</summary>
    [ObservableProperty]
    private string spellRoll = string.Empty;

    /// <summary>Passe-plat vers Row.Archetype.IsSpellcaster - masque le sous-onglet Sorts pour un type qui
    /// n'en lance jamais.</summary>
    public bool IsSpellcaster => Row.Archetype.IsSpellcaster;

    /// <summary>Copie de WarbandEditDialogViewModel.IsExistingWarband, tenue à jour à la création du slot
    /// (WarbandEditDialogViewModel.IncrementWarrior/SplitHenchmanGroupDraft) et rétroactivement si la
    /// case est basculée après coup (OnIsExistingWarbandChanged) - RecruitSlot n'a pas de référence au
    /// dialog lui-même, donc pas moyen de lire cette valeur en direct depuis ici. Pilote quels
    /// sous-onglets de RecruitSlotTabsView ont un sens : Compétences/XP seulement en Bande existante
    /// (import d'un historique déjà déterminé) - Équipement reste pertinent dans les deux modes (voir
    /// ShowTabsView), Sorts aussi pour un lanceur de sorts (IsSpellcaster) mais avec un bouton différent
    /// selon le mode (choix libre vs tirage 1D6, voir RecruitSlotTabsView.xaml).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTabsView))]
    private bool isExistingWarband;

    /// <summary>Pilote la visibilité de RecruitSlotTabsView - visible dès qu'il y a au moins un
    /// sous-onglet pertinent à montrer : Équipement (CanUseEquipment, les deux modes désormais - plus de
    /// bloc "+" hors-onglet séparé), Compétences/XP (Bande existante uniquement) ou Sorts (lanceur de
    /// sorts, les deux modes - livre des règles : sort de départ tiré au 1D6 même en création neuve).
    /// Composant entièrement masqué seulement pour un type qui ne coche aucune de ces trois raisons
    /// (ex. un Zombie hors Bande existante : pas d'équipement, pas de compétences/XP libres, pas de
    /// sorts).</summary>
    public bool ShowTabsView => CanUseEquipment || IsExistingWarband || IsSpellcaster;

    /// <summary>XP de cette recrue/ce groupe - uniquement modifiable en mode "Bande existante" (sinon
    /// reste la StartingExperience de l'archétype, comme WarriorArchetype.ToWarrior l'applique déjà).
    /// Persisté au Save() par une mise à jour explicite du Warrior fraîchement recruté (RecruitWarriorAsync
    /// applique toujours StartingExperience en premier).</summary>
    [ObservableProperty]
    private int experience;

    /// <summary>Sous-onglet actif (0=Équipement, 1=Compétences, 2=Sorts, 3=XP) - même composant
    /// TabToggleButton que WarriorEditDialog's Équipement/Compétences/Blessures, mais local à CE
    /// slot/groupe plutôt qu'à tout le dialog (chacun a le sien). Équipement/Compétences/XP n'ont de sens
    /// qu'en Bande existante (onglets masqués sinon, voir RecruitSlotTabsView) ; Sorts est le seul
    /// pertinent hors Bande existante pour un lanceur de sorts, d'où le défaut différent selon le mode
    /// (voir le constructeur) - sans ça, un sorcier fraîchement recruté démarrerait sur un onglet
    /// Équipement invisible, contenu orphelin sans bouton pour y accéder.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEquipmentSection))]
    [NotifyPropertyChangedFor(nameof(IsSkillsSection))]
    [NotifyPropertyChangedFor(nameof(IsSpellsSection))]
    [NotifyPropertyChangedFor(nameof(IsXpSection))]
    private int selectedSection;

    public bool IsEquipmentSection => SelectedSection == 0;
    public bool IsSkillsSection => SelectedSection == 1;
    public bool IsSpellsSection => SelectedSection == 2;
    public bool IsXpSection => SelectedSection == 3;

    [RelayCommand]
    private void ShowEquipmentSection() => SelectedSection = 0;

    [RelayCommand]
    private void ShowSkillsSection() => SelectedSection = 1;

    [RelayCommand]
    private void ShowSpellsSection() => SelectedSection = 2;

    [RelayCommand]
    private void ShowXpSection() => SelectedSection = 3;

    protected RecruitSlot(WarriorRecruitRow row, bool isExistingWarband)
    {
        Row = row;
        this.isExistingWarband = isExistingWarband;
        experience = row.Archetype.StartingExperience;
        // Hors Bande existante, Équipement/Compétences/XP sont des onglets masqués (voir ShowTabsView) -
        // démarrer dessus laisserait un contenu orphelin sans bouton pour en sortir. Sorts est alors le
        // seul onglet pertinent pour un lanceur de sorts ; pour tout autre type, RecruitSlotTabsView reste
        // entièrement masqué (ShowTabsView) donc SelectedSection n'a aucun effet visible.
        selectedSection = !isExistingWarband && IsSpellcaster ? 2 : 0;
        // EquipmentSummary est calculée (pas [ObservableProperty]) - Equipment.Add/Remove (AddEquipment/
        // RemoveEquipment de WarbandEditDialogViewModel) ne déclenchent donc aucune notification pour elle
        // sans ce relais explicite, laissant le Label de l'étape Noms vide/périmé tant qu'on ne rouvre pas
        // le dialog.
        Equipment.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EquipmentSummary));
        Spells.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSpells));
    }
}

/// <summary>Un slot de nom pour une recrue Héros (WarriorRecruitRow.NameSlots) - une classe dédiée plutôt
/// qu'un ObservableCollection&lt;string&gt; nu, parce qu'un Entry lié en TwoWay à un élément de collection
/// ne peut pas réécrire une string immuable en place (voir WarriorRecruitListView.xaml).</summary>
public partial class WarriorNameSlot : RecruitSlot
{
    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>Étiquette affichée à l'étape Équipement (avant que Name ne soit renseigné à l'étape Noms) -
    /// juste le nom d'archétype si ce héros est seul de son type, suffixé d'un numéro (1, 2...) s'il faut
    /// le distinguer d'autres héros du même type. Maintenue par WarbandEditDialogViewModel.
    /// RenumberHeroLabels (appelée après tout Increment/DecrementWarrior) plutôt que calculée ici, ce slot
    /// n'ayant pas de visibilité sur ses frères dans Row.NameSlots.</summary>
    [ObservableProperty]
    private string archetypeLabel = string.Empty;

    public WarriorNameSlot(WarriorRecruitRow row, bool isExistingWarband) : base(row, isExistingWarband)
    {
        archetypeLabel = row.Archetype.Name;
    }
}

/// <summary>Un sous-groupe nommé d'Hommes de main d'un même WarriorRecruitRow, avec son propre
/// équipement partagé (livre des règles : "Every model in each Henchman group must be armed and
/// armoured in the same way" - dans le même groupe, pas forcément dans tout le type recruté). Un type
/// recruté à l'effectif N démarre avec un seul HenchmanGroupDraft (Count = N, nom = celui de l'archétype) -
/// WarbandEditDialogViewModel.SplitHenchmanGroupDraft permet d'en détacher un second si le joueur veut des
/// équipements différents au sein du même type (ex. 3 Verminkin à l'épée + 2 au gourdin = 2 groupes
/// distincts, chacun avec son propre nom - voir "warband roster" dans le livre des règles : chaque
/// groupe d'Hommes de main doit avoir un nom).</summary>
public partial class HenchmanGroupDraft : RecruitSlot
{
    [ObservableProperty]
    private string name;

    /// <summary>Combien de guerriers de ce type appartiennent à CE groupe - la somme des Count de tous
    /// les HenchmanGroupDrafts d'une ligne doit toujours égaler WarriorRecruitRow.Count, maintenu par
    /// IncrementWarrior/DecrementWarrior (qui ne touchent jamais que le dernier groupe) et
    /// SplitHenchmanGroupDraft (qui transfère des unités du groupe source vers un nouveau groupe).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSplit))]
    private int count;

    /// <summary>Le bouton Diviser n'a de sens que s'il reste au moins 2 guerriers à répartir entre le
    /// groupe existant et un nouveau.</summary>
    public bool CanSplit => Count > 1 && Row.Archetype.CanUseEquipment;

    public HenchmanGroupDraft(WarriorRecruitRow row, string name, int count, bool isExistingWarband) : base(row, isExistingWarband)
    {
        this.name = name;
        this.count = count;
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

    /// <summary>"CO"/"GC" en toutes lettres, même formule que MutationRow.CostDisplay.</summary>
    public string CostDisplay => $"{Cost} {LocalizationService.Instance["LibGoldCrownsAbbr"]}";
    public bool IsHero => Archetype.IsHero;
    // UserGroup, pas Users - Users est déjà l'icône de la bande elle-même partout ailleurs dans l'appli
    // (Shell, tuile vide WarbandListPage, chip d'archétype de ce même wizard). Même icône que
    // WarriorRoleChipListView côté Bibliothèque, pour rester cohérent.
    public string IconGlyph => IsHero ? SolidFont.Crown : SolidFont.UserGroup;

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
    /// des équipements différents à une partie du type recruté - voir HenchmanGroupDraft.</summary>
    public ObservableCollection<HenchmanGroupDraft> HenchmanGroupDrafts { get; } = new();

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
