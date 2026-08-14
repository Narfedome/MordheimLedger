using System.Collections;
using System.Windows.Input;

namespace MordheimLedgerApp.Components;

/// <summary>Liste des types de guerriers recrutables pour la bande en cours de création
/// (WarbandEditDialogViewModel, étape Guerriers) : une ligne par WarriorRecruitRow avec son chip
/// (tap = détail) et un compteur 0/MaxCount avec stepper +/- - remplace l'ancien flux ActionSheet
/// "choisir un type puis nommer" par une liste directement manipulable. Le nom de chaque recrue Héros/
/// groupe d'Hommes de main se saisit à part, à l'étape Noms (voir WarbandEditDialog's IsNamesTab), pas
/// ici. La validation (MaxCount/trésorerie/effectif bande) reste côté appelant : Increment/
/// DecrementCommand ne font qu'exposer l'intention, voir WarbandEditDialogViewModel.IncrementWarrior.</summary>
public partial class WarriorRecruitListView : ContentView
{
    public static readonly BindableProperty HeaderTextProperty =
        BindableProperty.Create(nameof(HeaderText), typeof(string), typeof(WarriorRecruitListView), string.Empty);

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(WarriorRecruitListView));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    // Tap sur le chip (icône+nom) d'une ligne - ouvre le détail complet du WarriorArchetype.
    public static readonly BindableProperty DetailCommandProperty =
        BindableProperty.Create(nameof(DetailCommand), typeof(ICommand), typeof(WarriorRecruitListView));

    public ICommand? DetailCommand
    {
        get => (ICommand?)GetValue(DetailCommandProperty);
        set => SetValue(DetailCommandProperty, value);
    }

    public static readonly BindableProperty IncrementCommandProperty =
        BindableProperty.Create(nameof(IncrementCommand), typeof(ICommand), typeof(WarriorRecruitListView));

    public ICommand? IncrementCommand
    {
        get => (ICommand?)GetValue(IncrementCommandProperty);
        set => SetValue(IncrementCommandProperty, value);
    }

    public static readonly BindableProperty DecrementCommandProperty =
        BindableProperty.Create(nameof(DecrementCommand), typeof(ICommand), typeof(WarriorRecruitListView));

    public ICommand? DecrementCommand
    {
        get => (ICommand?)GetValue(DecrementCommandProperty);
        set => SetValue(DecrementCommandProperty, value);
    }

    public WarriorRecruitListView()
    {
        InitializeComponent();
    }
}
