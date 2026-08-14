using System.Windows.Input;

namespace MordheimLedgerApp.Components;

/// <summary>Single tappable chip (icône + nom, X optionnel) - le visuel partagé par ChipListView (une
/// liste de chips) et ChipItemView (un item unique obligatoire), extrait ici pour être réutilisable
/// ailleurs (ex. WarriorRecruitListView) sans dupliquer le Border/Grid trois fois.</summary>
public partial class ChipView : ContentView
{
    public static readonly BindableProperty ItemProperty =
        BindableProperty.Create(nameof(Item), typeof(object), typeof(ChipView));

    /// <summary>L'objet dont le Name est affiché - devient le BindingContext du Grid interne (Name)
    /// et le CommandParameter de Command/RemoveCommand.</summary>
    public object? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(ChipView), string.Empty);

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public static readonly BindableProperty IconFontFamilyProperty =
        BindableProperty.Create(nameof(IconFontFamily), typeof(string), typeof(ChipView), "FontSolid");

    public string IconFontFamily
    {
        get => (string)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }

    // Invoquée avec Item en CommandParameter au tap sur le chip (ouvre un dialog détail).
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ChipView));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    // Non renseignée (défaut) : pas de bouton Xmark. Renseignée : petit Xmark en bout de chip,
    // invoqué avec Item en CommandParameter.
    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(ChipView));

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public ChipView()
    {
        InitializeComponent();
    }
}
