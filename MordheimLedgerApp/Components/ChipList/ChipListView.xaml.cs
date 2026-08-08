using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;

namespace MordheimLedgerApp.Components;

public partial class ChipListView : ContentView
{
    public static readonly BindableProperty HeaderTextProperty =
        BindableProperty.Create(nameof(HeaderText), typeof(string), typeof(ChipListView), string.Empty);

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ChipListView),
            propertyChanged: (bindable, oldValue, newValue) => ((ChipListView)bindable).OnItemsSourceChanged((IEnumerable?)oldValue, (IEnumerable?)newValue));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    // ItemsSource reste la même instance d'ObservableCollection tout au long d'une session d'édition
    // (AddWarrior/RemoveWarrior etc. mutent en place, ils ne réassignent jamais la propriété) - sans ce
    // second abonnement, HasItems/ShowSection ne se recalculaient qu'au tout premier binding, restant
    // figés à "vide" (donc FlexLayout cachée) pour le reste de la session dès qu'on partait d'une liste
    // vide, même si BindableLayout affichait correctement les nouveaux items en dessous.
    private void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldIncc) oldIncc.CollectionChanged -= OnItemsCollectionChanged;
        if (newValue is INotifyCollectionChanged newIncc) newIncc.CollectionChanged += OnItemsCollectionChanged;
        Recompute();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Recompute();

    // "FontSolid" = Font Awesome 7 Free Solid (voir MauiProgram.ConfigureFonts) - la police de la
    // grande majorité des chips existants ; les icônes RpgAwesome (écoles de magie, animaux...)
    // passent leur propre IconFontFamily="RpgAwesome" à l'usage.
    public static readonly BindableProperty IconFontFamilyProperty =
        BindableProperty.Create(nameof(IconFontFamily), typeof(string), typeof(ChipListView), "FontSolid");

    public string IconFontFamily
    {
        get => (string)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }

    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(ChipListView), string.Empty);

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    // Invoquée avec l'élément tapé en CommandParameter - chaque page appelante fournit sa propre
    // commande ShowXDetailCommand (ouvre ChipDetailDialog ou un dialog récap dédié selon le type).
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ChipListView));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    // Non renseignée (défaut, usages en lecture seule) : pas de bouton "+" à côté de HeaderText.
    // Renseignée (dialogs éditables, ex. WarbandArchetypeEditDialog) : affiche un petit
    // FaIconButtonView GhostIconButtonStyle "+" invoquant cette commande - remplace le pattern
    // Grid+FaIconButtonView dupliqué manuellement par chaque dialog Edit.
    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(nameof(AddCommand), typeof(ICommand), typeof(ChipListView),
            propertyChanged: (bindable, _, _) => ((ChipListView)bindable).Recompute());

    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    // Non renseignée (défaut) : pas de bouton Xmark sur les chips (usages en lecture seule).
    // Renseignée : chaque chip affiche un petit Xmark supplémentaire, invoqué avec l'item du chip en
    // CommandParameter (même idée que Command, un second point d'entrée pour "retirer" plutôt que
    // "voir le détail/éditer").
    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(ChipListView));

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    // Non renseigné (défaut) : la section entière (header inclus) se masque si ItemsSource est vide.
    // Renseigné : affiché à la place du FlexLayout quand ItemsSource est vide (ex. "Réservé à ces
    // bandes : vide = commun à toutes les bandes" sur EquipmentItemDetailDialog).
    public static readonly BindableProperty EmptyHintTextProperty =
        BindableProperty.Create(nameof(EmptyHintText), typeof(string), typeof(ChipListView), null,
            propertyChanged: (bindable, _, _) => ((ChipListView)bindable).Recompute());

    public string? EmptyHintText
    {
        get => (string?)GetValue(EmptyHintTextProperty);
        set => SetValue(EmptyHintTextProperty, value);
    }

    // Calculées à partir d'ItemsSource/EmptyHintText (voir Recompute) - pas destinées à être posées
    // depuis l'extérieur, juste bindées en interne par ChipListView.xaml.
    public static readonly BindableProperty HasItemsProperty =
        BindableProperty.Create(nameof(HasItems), typeof(bool), typeof(ChipListView), false);

    public bool HasItems
    {
        get => (bool)GetValue(HasItemsProperty);
        private set => SetValue(HasItemsProperty, value);
    }

    public static readonly BindableProperty ShowSectionProperty =
        BindableProperty.Create(nameof(ShowSection), typeof(bool), typeof(ChipListView), false);

    public bool ShowSection
    {
        get => (bool)GetValue(ShowSectionProperty);
        private set => SetValue(ShowSectionProperty, value);
    }

    public ChipListView()
    {
        InitializeComponent();
    }

    private void Recompute()
    {
        HasItems = ItemsSource?.Cast<object>().Any() ?? false;
        // AddCommand renseignée : la section (header + bouton "+") reste visible même liste vide,
        // pour pouvoir ajouter le tout premier élément.
        ShowSection = HasItems || !string.IsNullOrEmpty(EmptyHintText) || AddCommand != null;
    }
}
