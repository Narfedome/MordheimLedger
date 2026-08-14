using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Components;

/// <summary>Regroupe un ItemsSource de WarriorArchetype en deux sections Héros/Hommes de main (chacune
/// son propre ChipListView interne, icône Crown/Shield) plutôt qu'une seule liste plate où rien ne les
/// distingue - composé par-dessus ChipListView existant (pas de duplication de la logique chip/
/// FlexLayout). Un seul bouton "+" en tête (AddCommand) : le type choisi au picker détermine lui-même
/// s'il rejoint Héros ou Hommes de main, pas besoin de deux points d'ajout.</summary>
public partial class WarriorRoleChipListView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<WarriorArchetype>), typeof(WarriorRoleChipListView),
            propertyChanged: (bindable, oldValue, newValue) =>
                ((WarriorRoleChipListView)bindable).OnItemsSourceChanged((IEnumerable<WarriorArchetype>?)oldValue, (IEnumerable<WarriorArchetype>?)newValue));

    public IEnumerable<WarriorArchetype>? ItemsSource
    {
        get => (IEnumerable<WarriorArchetype>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(WarriorRoleChipListView));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(nameof(AddCommand), typeof(ICommand), typeof(WarriorRoleChipListView));

    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(WarriorRoleChipListView));

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    // Alimentent les deux ChipListView internes - pas exposées comme BindableProperty publiques,
    // recalculées uniquement en interne (voir Recompute).
    public ObservableCollection<WarriorArchetype> HeroesItems { get; } = new();
    public ObservableCollection<WarriorArchetype> HenchmenItems { get; } = new();

    public WarriorRoleChipListView()
    {
        InitializeComponent();
    }

    // Même idiome que ChipListView.xaml.cs : ItemsSource reste la même instance d'ObservableCollection
    // tout au long d'une session d'édition (AddWarrior/RemoveWarrior mutent en place), donc un simple
    // binding initial ne suffit pas - il faut s'abonner à CollectionChanged pour recalculer les deux
    // sous-listes à chaque ajout/retrait/remplacement.
    private void OnItemsSourceChanged(IEnumerable<WarriorArchetype>? oldValue, IEnumerable<WarriorArchetype>? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldIncc) oldIncc.CollectionChanged -= OnItemsCollectionChanged;
        if (newValue is INotifyCollectionChanged newIncc) newIncc.CollectionChanged += OnItemsCollectionChanged;
        Recompute();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Recompute();

    private void Recompute()
    {
        var source = ItemsSource ?? Enumerable.Empty<WarriorArchetype>();

        HeroesItems.Clear();
        foreach (var warrior in source.Where(w => w.IsHero)) HeroesItems.Add(warrior);

        HenchmenItems.Clear();
        foreach (var warrior in source.Where(w => !w.IsHero)) HenchmenItems.Add(warrior);
    }
}
