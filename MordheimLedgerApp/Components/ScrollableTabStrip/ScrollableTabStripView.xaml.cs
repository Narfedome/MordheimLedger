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

        _leftHint = GetTemplateChild("LeftHint") as View;
        _rightHint = GetTemplateChild("RightHint") as View;
        AddHintTap(_leftHint, direction: -1);
        AddHintTap(_rightHint, direction: 1);

        _tabScrollView.Scrolled += (_, _) => UpdateScrollHints();
        _tabScrollView.SizeChanged += (_, _) =>
        {
            UpdateScrollHints();
            _ = RevealActiveTabAsync();
        };
        // ContentSize change quand un onglet apparaît/disparaît (IsVisible conditionnels des appelants).
        _tabScrollView.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(ScrollView.ContentSize)) return;
            UpdateScrollHints();
            _ = RevealActiveTabAsync();
        };
        HookTabs();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Content)) HookTabs();
    }

    private View? _leftHint;
    private View? _rightHint;

    // --- Onglet actif toujours visible (2026-09-28, retour utilisateur - sur téléphone, des bandes du même
    // écran s'affichaient défilées différemment, parfois l'onglet actif hors champ, le badge ‹ rognant le
    // premier onglet). Comportement explicite plutôt que celui, variable, de la plateforme : la bande part
    // calée à gauche, et ne défile que pour révéler entièrement l'onglet actif (TabToggleButton.IsActive) -
    // à l'ouverture, quand il change, ou quand des onglets apparaissent/disparaissent. Jamais contre un
    // défilement fait à la main (_userScrolled).

    private readonly HashSet<TabToggleButton> _hookedTabs = new();
    private bool _userScrolled;

    private IEnumerable<TabToggleButton> Tabs() =>
        Content?.GetVisualTreeDescendants().OfType<TabToggleButton>() ?? Enumerable.Empty<TabToggleButton>();

    private void HookTabs()
    {
        foreach (var tab in Tabs())
        {
            if (!_hookedTabs.Add(tab)) continue;
            tab.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(TabToggleButton.IsActive) && tab.IsActive
                    || args.PropertyName == nameof(IsVisible))
                    _ = RevealActiveTabAsync();
            };
        }
    }

    private async Task RevealActiveTabAsync()
    {
        if (_tabScrollView is null || _tabScrollView.Width <= 0) return;
        var viewport = _tabScrollView.Width;
        var scrollX = _tabScrollView.ScrollX;

        var active = Tabs().FirstOrDefault(t => t.IsActive && IsShown(t));
        double target;
        if (active is null || OffsetInContent(active) is not { } left)
        {
            if (_userScrolled) return;
            target = 0;
        }
        else
        {
            var right = left + active.Width;
            if (!_userScrolled && right <= viewport) target = 0;
            else if (left < scrollX) target = left;
            else if (right > scrollX + viewport) target = right - viewport;
            else return;
        }

        var maxScrollX = Math.Max(0, _tabScrollView.ContentSize.Width - viewport);
        target = Math.Clamp(target, 0, maxScrollX);
        if (Math.Abs(target - scrollX) > 1)
            await _tabScrollView.ScrollToAsync(target, 0, animated: _userScrolled);
    }

    /// <summary>Visible, lui et tous ses parents jusqu'à la bande (un onglet peut être enveloppé dans un
    /// ContentView porteur d'une seconde condition IsVisible, voir RecruitSlotTabsView).</summary>
    private bool IsShown(VisualElement tab)
    {
        for (Element? e = tab; e is not null && e != _tabScrollView; e = e.Parent)
            if (e is VisualElement { IsVisible: false }) return false;
        return true;
    }

    /// <summary>Abscisse de l'onglet dans le contenu défilant (somme des X de ses parents jusqu'au
    /// ScrollView) - null tant qu'il n'est pas encore mesuré.</summary>
    private double? OffsetInContent(VisualElement tab)
    {
        if (tab.Width <= 0) return null;
        double x = 0;
        for (Element? e = tab; e is VisualElement v && e != _tabScrollView; e = e.Parent)
        {
            if (v.Parent == _tabScrollView) break;
            x += v.X;
        }
        return x;
    }

    /// <summary>Badges ‹ / › (voir le XAML) : visibles seulement quand il reste des onglets hors champ de
    /// ce côté - rien quand toute la bande tient dans la largeur disponible.</summary>
    private void UpdateScrollHints()
    {
        if (_tabScrollView is null) return;
        const double tolerance = 1;
        var maxScrollX = _tabScrollView.ContentSize.Width - _tabScrollView.Width;
        if (_leftHint is not null) _leftHint.IsVisible = maxScrollX > tolerance && _tabScrollView.ScrollX > tolerance;
        if (_rightHint is not null) _rightHint.IsVisible = maxScrollX > tolerance && _tabScrollView.ScrollX < maxScrollX - tolerance;
    }

    private void AddHintTap(View? hint, int direction)
    {
        if (hint is null) return;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (_tabScrollView is null) return;
            _userScrolled = true;
            // Défile d'environ une largeur visible (un peu moins, pour garder un repère), borné au contenu.
            var maxScrollX = Math.Max(0, _tabScrollView.ContentSize.Width - _tabScrollView.Width);
            var target = Math.Clamp(_tabScrollView.ScrollX + direction * _tabScrollView.Width * 0.8, 0, maxScrollX);
            await _tabScrollView.ScrollToAsync(target, 0, animated: true);
        };
        hint.GestureRecognizers.Add(tap);
    }

    private async void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_tabScrollView is null) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _userScrolled = true;
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
