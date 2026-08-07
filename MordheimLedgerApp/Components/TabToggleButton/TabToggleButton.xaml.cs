using System.Windows.Input;

namespace MordheimLedgerApp.Components;

/// <summary>
/// A tappable label + underline bar for the "toggle a few sections within one page" pattern used
/// across the app (WarbandDetailPage's Roster/Historique, WarriorEditDialog's Équipement/Compétences/
/// Blessures, LibraryPage's Bandes/Marché/Compétences/Blessures) - deliberately not a real
/// TabbedPage/Shell tab, see those pages' comments. A plain Button can't show a single-side (bottom
/// only) accent bar via Style Setters, hence this small ContentView instead of a Button subclass.
/// </summary>
public partial class TabToggleButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(TabToggleButton), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(TabToggleButton));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(TabToggleButton), false);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(TabToggleButton), 14.0);

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Survol souris (Desktop uniquement, voir PointerGestureRecognizer côté XAML) - aucun état
    /// PointerOver natif ici puisque ce n'est pas un Button, juste un Label+TapGestureRecognizer (voir
    /// le commentaire de classe). Interne : mise à jour uniquement par OnPointerEntered/Exited.</summary>
    public static readonly BindableProperty IsHoveredProperty =
        BindableProperty.Create(nameof(IsHovered), typeof(bool), typeof(TabToggleButton), false);

    public bool IsHovered
    {
        get => (bool)GetValue(IsHoveredProperty);
        private set => SetValue(IsHoveredProperty, value);
    }

    public TabToggleButton()
    {
        InitializeComponent();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e) => IsHovered = true;

    private void OnPointerExited(object? sender, PointerEventArgs e) => IsHovered = false;
}
