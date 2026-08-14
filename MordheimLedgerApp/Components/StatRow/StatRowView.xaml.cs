namespace MordheimLedgerApp.Components;

public partial class StatRowView : ContentView
{
    // WarriorArchetype/Animal/Warrior - la seule contrainte est d'exposer WeaponSkill/BallisticSkill/
    // Strength/Toughness/Wounds/Initiative/Attacks/Leadership sous ces noms (déjà le cas des 3).
    public static readonly BindableProperty ItemProperty =
        BindableProperty.Create(nameof(Item), typeof(object), typeof(StatRowView));

    public object? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    // À fournir explicitement par l'appelant (ex. Item.MovementDisplay pour WarriorArchetype/Warrior,
    // Item.Movement pour Animal) - pas de nom de propriété uniforme à binder faiblement ici, voir le
    // commentaire dans StatRowView.xaml.
    public static readonly BindableProperty MovementTextProperty =
        BindableProperty.Create(nameof(MovementText), typeof(string), typeof(StatRowView), string.Empty, BindingMode.TwoWay);

    public string MovementText
    {
        get => (string)GetValue(MovementTextProperty);
        set => SetValue(MovementTextProperty, value);
    }

    // Bascule Label (lecture seule, tous les appelants existants - WarbandDetailPage/AnimalDetailDialog/
    // WarriorArchetypeDetailDialog) / Entry (édition - AnimalEditDialog/WarriorArchetypeEditDialog) par
    // colonne, même Grid/mêmes tailles de police dans les deux cas - un seul composant plutôt qu'une
    // grille dupliquée par dialog d'édition.
    public static readonly BindableProperty IsEditableProperty =
        BindableProperty.Create(nameof(IsEditable), typeof(bool), typeof(StatRowView), false);

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    // Défauts calés sur les dialogs récap (Animal/WarriorArchetypeDetailDialog) - WarbandDetailPage
    // (une ligne par guerrier dans un roster qui peut être long) les resserre à 10/13 pour rester dense.
    public static readonly BindableProperty AbbrFontSizeProperty =
        BindableProperty.Create(nameof(AbbrFontSize), typeof(double), typeof(StatRowView), 12.0);

    public double AbbrFontSize
    {
        get => (double)GetValue(AbbrFontSizeProperty);
        set => SetValue(AbbrFontSizeProperty, value);
    }

    public static readonly BindableProperty ValueFontSizeProperty =
        BindableProperty.Create(nameof(ValueFontSize), typeof(double), typeof(StatRowView), 14.0);

    public double ValueFontSize
    {
        get => (double)GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    public StatRowView()
    {
        InitializeComponent();
    }
}
