using System.Collections;
using System.Linq;

namespace MordheimLedgerApp.Components;

public partial class CodexGroupedGridView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(CodexGroupedGridView),
            propertyChanged: (bindable, _, _) => ((CodexGroupedGridView)bindable).RefreshPinnedHeader(0));

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
        BindableProperty.Create(nameof(ShowGroupHeaders), typeof(bool), typeof(CodexGroupedGridView), true,
            propertyChanged: (bindable, _, _) => ((CodexGroupedGridView)bindable).RefreshPinnedHeader(0));

    public bool ShowGroupHeaders
    {
        get => (bool)GetValue(ShowGroupHeadersProperty);
        set => SetValue(ShowGroupHeadersProperty, value);
    }

    public CodexGroupedGridView()
    {
        InitializeComponent();
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e) => RefreshPinnedHeader(e.ScrollY);

    // Épinglé en permanence (y compris au tout en haut, ScrollY=0) plutôt que masqué tant qu'on n'a pas
    // dépassé un seuil : un pin qui apparaît en cours de route "saute" visuellement (signalé par
    // l'utilisateur - le bandeau surgit d'un coup). En restant toujours visible, il coïncide
    // exactement avec le header inline du groupe actuellement en haut plutôt que de le dupliquer.
    private void RefreshPinnedHeader(double scrollY)
    {
        if (!ShowGroupHeaders)
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
            // Pas encore mesuré (juste après un changement d'ItemsSource, avant le premier passage de
            // layout) : Bounds.Y vaut 0 pour TOUS les enfants, donc "Y <= scrollY" ne s'arrête jamais et
            // retiendrait le DERNIER groupe au lieu du premier - on abandonne au profit du repli
            // ci-dessous dès qu'un enfant non mesuré est rencontré.
            if (element.Bounds.Height <= 0)
            {
                current = null;
                break;
            }
            if (element.Bounds.Y <= scrollY)
                current = group;
            else
                break;
        }

        // Au tout premier rendu (ItemsSource vient d'être posé), GroupsStack.Children n'a pas encore de
        // Bounds mesurés - repli sur le premier élément d'ItemsSource directement.
        current ??= ItemsSource?.Cast<object>().OfType<ICodexGroup>().FirstOrDefault();

        if (current is null)
        {
            PinnedHeaderBorder.IsVisible = false;
            return;
        }

        PinnedHeaderLabel.Text = current.Name;
        PinnedHeaderBorder.IsVisible = true;
    }
}
