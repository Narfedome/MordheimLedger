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
        BindableProperty.Create(nameof(MovementText), typeof(string), typeof(StatRowView), string.Empty);

    public string MovementText
    {
        get => (string)GetValue(MovementTextProperty);
        set => SetValue(MovementTextProperty, value);
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
