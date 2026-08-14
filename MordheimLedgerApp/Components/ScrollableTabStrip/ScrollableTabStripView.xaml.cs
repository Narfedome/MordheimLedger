namespace MordheimLedgerApp.Components;

/// <summary>Enveloppe générique "bande d'onglets qui scrolle horizontalement" - contrôle avec
/// TabToggleButton qui déborde d'un dialog étroit (ex. les 5 onglets de WarbandArchetypeEditDialog dans
/// ses 340px). Ne connaît rien du concept "wizard"/StepLabel : c'est purement un ScrollView horizontal
/// (scrollbar masquée, drag-to-scroll à la souris) autour d'un contenu unique fourni par l'appelant
/// (même idiome ContentPresenter que PickerSelectorLayout) - réutilisable pour n'importe quelle bande
/// d'onglets, pas seulement les dialogs de création/édition qui alternent StepLabel/onglets.</summary>
public partial class ScrollableTabStripView : ContentView
{
    private ScrollView? _tabScrollView;
    private double _panStartScrollX;

    public ScrollableTabStripView()
    {
        InitializeComponent();
    }

    // Le ScrollView vit dans le ControlTemplate, pas dans le contenu direct de cette ContentView - il
    // faut passer par GetTemplateChild (pas de champ x:Name auto-généré pour un élément de template)
    // pour pouvoir y attacher le PanGestureRecognizer et faire défiler à la souris (drag), la scrollbar
    // étant masquée (HorizontalScrollBarVisibility="Never" côté XAML).
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_tabScrollView is not null) return;
        _tabScrollView = GetTemplateChild("TabScrollView") as ScrollView;
        if (_tabScrollView is null) return;

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        _tabScrollView.GestureRecognizers.Add(pan);
    }

    private async void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_tabScrollView is null) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartScrollX = _tabScrollView.ScrollX;
                break;
            case GestureStatus.Running:
                // TotalX = déplacement cumulé depuis le début du drag (pas incrémental) - glisser vers la
                // droite doit faire défiler le contenu vers la droite aussi (paradigme "on tire la page"),
                // donc on soustrait plutôt qu'on additionne.
                await _tabScrollView.ScrollToAsync(_panStartScrollX - e.TotalX, 0, animated: false);
                break;
        }
    }
}
