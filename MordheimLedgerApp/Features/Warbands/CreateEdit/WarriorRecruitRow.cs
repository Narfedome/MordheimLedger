using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Resources.Icons;
using System.Collections.ObjectModel;

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

    /// <summary>Équipement propre à cette recrue Héros - contrairement aux Hommes de main (voir
    /// WarriorRecruitRow.GroupEquipment), chaque héros peut avoir un équipement différent (livre des
    /// règles : "Every model in each Henchman group must be armed and armoured in the same way", ce qui
    /// ne s'applique pas aux héros).</summary>
    public ObservableCollection<EquipmentPick> Equipment { get; } = new();

    public WarriorNameSlot(WarriorRecruitRow row)
    {
        Row = row;
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

    /// <summary>Équipement partagé par TOUT le groupe d'Hommes de main de ce type (livre des règles :
    /// chaque modèle d'un même groupe de Hommes de main doit être équipé identiquement) - une seule
    /// sélection ici, appliquée à chacune des Count recrues au Save. Vide/non pertinent pour les Héros,
    /// qui utilisent WarriorNameSlot.Equipment à la place (chacun peut différer).</summary>
    public ObservableCollection<EquipmentPick> GroupEquipment { get; } = new();

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
