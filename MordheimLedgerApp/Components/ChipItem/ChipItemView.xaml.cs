using System.Windows.Input;

namespace MordheimLedgerApp.Components;

public partial class ChipItemView : ContentView
{
    public static readonly BindableProperty HeaderTextProperty =
        BindableProperty.Create(
            nameof(HeaderText),
            typeof(string),
            typeof(ChipItemView),
            string.Empty);

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    // Défaut 12 = comportement inchangé pour les usages existants qui ne la précisent pas - voir
    // ChipListView.HeaderFontSize pour le même principe côté liste.
    public static readonly BindableProperty HeaderFontSizeProperty =
        BindableProperty.Create(nameof(HeaderFontSize), typeof(double), typeof(ChipItemView), 12.0);

    public double HeaderFontSize
    {
        get => (double)GetValue(HeaderFontSizeProperty);
        set => SetValue(HeaderFontSizeProperty, value);
    }

    public static readonly BindableProperty IsMandatoryProperty =
        BindableProperty.Create(
            nameof(IsMandatory),
            typeof(bool),
            typeof(ChipItemView),
            false);

    public bool IsMandatory
    {
        get => (bool)GetValue(IsMandatoryProperty);
        set =>
            SetValue(IsMandatoryProperty, value);
    }

    public static readonly BindableProperty ItemProperty =
        BindableProperty.Create(
            nameof(Item),
            typeof(object),
            typeof(ChipItemView),
            null,
            propertyChanged: (bindable, _, _) =>
                ((ChipItemView)bindable).Recompute());

    public object? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly BindableProperty IconFontFamilyProperty =
        BindableProperty.Create(
            nameof(IconFontFamily),
            typeof(string),
            typeof(ChipItemView),
            "FontSolid");

    public string IconFontFamily
    {
        get => (string)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }

    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(
            nameof(IconGlyph),
            typeof(string),
            typeof(ChipItemView),
            string.Empty);

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(
            nameof(Command),
            typeof(ICommand),
            typeof(ChipItemView));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty =
        BindableProperty.Create(
            nameof(AddCommand),
            typeof(ICommand),
            typeof(ChipItemView),
            null,
            propertyChanged: (bindable, _, _) =>
                ((ChipItemView)bindable).Recompute());

    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(
            nameof(RemoveCommand),
            typeof(ICommand),
            typeof(ChipItemView));

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public static readonly BindableProperty EmptyHintTextProperty =
        BindableProperty.Create(
            nameof(EmptyHintText),
            typeof(string),
            typeof(ChipItemView),
            null,
            propertyChanged: (bindable, _, _) =>
                ((ChipItemView)bindable).Recompute());

    public string? EmptyHintText
    {
        get => (string?)GetValue(EmptyHintTextProperty);
        set => SetValue(EmptyHintTextProperty, value);
    }

    public static readonly BindableProperty AlwaysShowSectionProperty =
        BindableProperty.Create(
            nameof(AlwaysShowSection),
            typeof(bool),
            typeof(ChipItemView),
            false,
            propertyChanged: (bindable, _, _) =>
                ((ChipItemView)bindable).Recompute());

    public bool AlwaysShowSection
    {
        get => (bool)GetValue(AlwaysShowSectionProperty);
        set => SetValue(AlwaysShowSectionProperty, value);
    }

    public static readonly BindableProperty HasItemProperty =
        BindableProperty.Create(
            nameof(HasItem),
            typeof(bool),
            typeof(ChipItemView),
            false);

    public bool HasItem
    {
        get => (bool)GetValue(HasItemProperty);
        private set => SetValue(HasItemProperty, value);
    }

    public static readonly BindableProperty ShowSectionProperty =
        BindableProperty.Create(
            nameof(ShowSection),
            typeof(bool),
            typeof(ChipItemView),
            false);

    public bool ShowSection
    {
        get => (bool)GetValue(ShowSectionProperty);
        private set => SetValue(ShowSectionProperty, value);
    }

    public ChipItemView()
    {
        InitializeComponent();
    }

    private void Recompute()
    {
        HasItem = Item != null;

        ShowSection =
            HasItem
            || !string.IsNullOrEmpty(EmptyHintText)
            || AddCommand != null
            || AlwaysShowSection;
    }
}
