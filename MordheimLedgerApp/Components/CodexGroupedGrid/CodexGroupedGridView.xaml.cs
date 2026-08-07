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

    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        // Groupe 0 encore visible en haut : son header inline suffit, pas besoin du pin.
        if (!ShowGroupHeaders || ItemsSource is null || e.FirstVisibleItemIndex <= 0)
        {
            PinnedHeaderBorder.IsVisible = false;
            return;
        }

        if (ItemsSource.Cast<object>().ElementAtOrDefault(e.FirstVisibleItemIndex) is not ICodexGroup group)
        {
            PinnedHeaderBorder.IsVisible = false;
            return;
        }

        PinnedHeaderLabel.Text = group.Name;
        PinnedHeaderBorder.IsVisible = true;
    }
}
