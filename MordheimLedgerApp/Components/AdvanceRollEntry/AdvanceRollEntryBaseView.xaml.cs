namespace MordheimLedgerApp.Components.AdvanceRollEntry;

/// <summary>Le jet + résultat Compétence/Sort/Caractéristique d'un AdvanceRollEntry, SANS le bloc
/// Promotion (voir AdvanceRollEntryView, qui ajoute ce bloc par-dessus et l'utilise pour ses 2 jets
/// imbriqués NestedHeroRoll/NestedHenchmanRoll). Délibérément un composant SÉPARÉ plutôt qu'un simple
/// contenu partiel d'AdvanceRollEntryView : une ContentView qui s'auto-référence dans son propre XAML
/// (même sous un IsVisible="False") est instanciée en boucle infinie par MAUI - InitializeComponent
/// construit tout l'arbre visuel sans tenir compte des bindings/visibilité, un StackOverflowException
/// garanti à l'exécution (trouvé le 2026-08-24 en testant la Promotion en vrai). AdvanceRollEntryBaseView
/// ne référence jamais AdvanceRollEntryView ni lui-même, donc aucun cycle possible.</summary>
public partial class AdvanceRollEntryBaseView : ContentView
{
    public static readonly BindableProperty EntryProperty =
        BindableProperty.Create(nameof(Entry), typeof(object), typeof(AdvanceRollEntryBaseView));

    public object? Entry
    {
        get => GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    public AdvanceRollEntryBaseView()
    {
        InitializeComponent();
    }
}
