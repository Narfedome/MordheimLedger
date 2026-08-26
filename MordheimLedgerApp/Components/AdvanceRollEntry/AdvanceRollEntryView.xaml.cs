namespace MordheimLedgerApp.Components.AdvanceRollEntry;

/// <summary>Rendu réutilisable d'un seul AdvanceRollEntry (jet + résultat Compétence/Sort/
/// Caractéristique/Promotion) - même idiome que StatRowView (une seule BindableProperty Item, weakly
/// typed). Utilisé 3 fois par EndOfGameDialog.xaml pour la même carte : la liste CurrentAdvanceRolls
/// (BindableLayout), et les 2 jets imbriqués d'une Promotion (Entry.NestedHeroRoll/NestedHenchmanRoll,
/// bindés directement, hors BindableLayout) - évite de tripler ~90 lignes de XAML.</summary>
public partial class AdvanceRollEntryView : ContentView
{
    public static readonly BindableProperty EntryProperty =
        BindableProperty.Create(nameof(Entry), typeof(object), typeof(AdvanceRollEntryView));

    public object? Entry
    {
        get => GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    public AdvanceRollEntryView()
    {
        InitializeComponent();
    }
}
