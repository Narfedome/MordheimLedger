using System.Collections;
using System.Linq;

namespace MordheimLedgerApp.Components;

public partial class CodexGroupedGridView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(CodexGroupedGridView));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    // Template d'une tuile (fourni par la page appelante) - passé tel quel au BindableLayout interne.
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(CodexGroupedGridView));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly BindableProperty ShowGroupHeadersProperty =
        BindableProperty.Create(nameof(ShowGroupHeaders), typeof(bool), typeof(CodexGroupedGridView), true);

    public bool ShowGroupHeaders
    {
        get => (bool)GetValue(ShowGroupHeadersProperty);
        set => SetValue(ShowGroupHeadersProperty, value);
    }

    public CodexGroupedGridView()
    {
        InitializeComponent();
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        // Tout en haut (avant tout scroll) : le header inline du 1er groupe est déjà visible, pas
        // besoin du pin. Petit seuil plutôt que "> 0" pour absorber le bruit de scroll natif.
        if (!ShowGroupHeaders || e.ScrollY < 10)
        {
            PinnedHeaderBorder.IsVisible = false;
            return;
        }

        // Le groupe "courant" est le dernier dont le header a défilé au-dessus du haut visible -
        // recalculé à chaque scroll plutôt que mis en cache (pas de risque de désynchronisation si
        // ItemsSource change ou si une tuile change de taille).
        ICodexGroup? current = null;
        foreach (var child in GroupsStack.Children)
        {
            if (child is not VisualElement { BindingContext: ICodexGroup group } element)
                continue;
            if (element.Bounds.Y <= e.ScrollY)
                current = group;
            else
                break;
        }

        if (current is null)
        {
            PinnedHeaderBorder.IsVisible = false;
            return;
        }

        PinnedHeaderLabel.Text = current.Name;
        PinnedHeaderBorder.IsVisible = true;
    }
}
