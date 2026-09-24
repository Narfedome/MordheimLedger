namespace MordheimLedgerApp.Components;

/// <summary>Charge la page suivante d'une CollectionView dont l'ItemsSource est un
/// IIncrementalCollection (voir PagedGroupCollection) quand on approche du bas - porté de DmTools
/// (LibraryTrackView.xaml.cs), avec les mêmes contournements :
/// - RemainingItemsThresholdReachedCommand est peu fiable (notamment sous un ControlTemplate comme
///   WatermarkedLayout) : fin de liste détectée à la main via Scrolled.
/// - Sur Windows, Scrolled ne se déclenche jamais pour une CollectionView (le handler MAUI ne retrouve
///   pas le ScrollViewer natif, dotnet/maui#12725/#15914) : hook natif séparé sur ce ScrollViewer.
/// - Si la page chargée ne remplit pas l'écran (grande fenêtre, filtre qui laisse peu de tuiles), il
///   n'y a rien à faire défiler pour déclencher la suite : pages chargées tant que le contenu ne
///   dépasse pas la zone visible (Windows, là où une fenêtre peut être bien plus haute qu'une page).
/// </summary>
public class LoadMoreOnScrollBehavior : Behavior<CollectionView>
{
    /// <summary>Nombre de tuiles restantes sous la dernière visible en dessous duquel on charge la suite.</summary>
    private const int RemainingItemsThreshold = 8;

    private CollectionView? _collectionView;

    private IIncrementalCollection? Source => _collectionView?.ItemsSource as IIncrementalCollection;

    protected override void OnAttachedTo(CollectionView bindable)
    {
        base.OnAttachedTo(bindable);
        _collectionView = bindable;
        bindable.Scrolled += OnScrolled;
        bindable.PropertyChanged += OnCollectionViewPropertyChanged;
#if WINDOWS
        bindable.Loaded += OnLoadedWindows;
        bindable.SizeChanged += OnSizeChangedWindows;
#endif
    }

    protected override void OnDetachingFrom(CollectionView bindable)
    {
        bindable.Scrolled -= OnScrolled;
        bindable.PropertyChanged -= OnCollectionViewPropertyChanged;
#if WINDOWS
        bindable.Loaded -= OnLoadedWindows;
        bindable.SizeChanged -= OnSizeChangedWindows;
        if (_nativeScrollViewer is not null)
        {
            _nativeScrollViewer.ViewChanged -= OnNativeViewChanged;
            _nativeScrollViewer.SizeChanged -= OnNativeSizeChanged;
        }
        _nativeScrollViewer = null;
#endif
        _collectionView = null;
        base.OnDetachingFrom(bindable);
    }

    private void LoadMoreIfPossible()
    {
        if (Source is { HasMore: true } source) source.LoadMore();
    }

    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        // LastVisibleItemIndex compte les tuiles tous groupes confondus (pas d'index par groupe dans
        // cette version de MAUI) - comparé donc au total de tuiles affichées, comme DmTools.
        if (e.LastVisibleItemIndex < 0 || Source is not { HasMore: true } source) return;
        if (e.LastVisibleItemIndex >= source.LoadedItemCount - RemainingItemsThreshold)
            source.LoadMore();
    }

    /// <summary>Nouvelle source (changement de filtre, rechargement) : la première page peut ne pas
    /// remplir l'écran - revérifie une fois le layout posé.</summary>
    private void OnCollectionViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CollectionView.ItemsSource)) return;
#if WINDOWS
        // Relance aussi le hook natif : un onglet du Codex masqué au chargement n'a pas encore de
        // ScrollViewer natif réalisé (gabarit non appliqué tant que masqué) - ses données, elles, ne sont
        // chargées qu'à sa première visite, donc une fois visible.
        _hookAttempts = 0;
        ScheduleHookAttempt();
#endif
    }

#if WINDOWS
    private Microsoft.UI.Xaml.Controls.ScrollViewer? _nativeScrollViewer;
    private int _hookAttempts;

    private void OnLoadedWindows(object? sender, EventArgs e)
    {
        _hookAttempts = 0;
        ScheduleHookAttempt();
    }

    // Bascule plein écran / redimensionnement : la page chargée peut ne plus remplir la zone agrandie.
    private void OnSizeChangedWindows(object? sender, EventArgs e) => ScheduleViewportFillCheck();

    private void ScheduleHookAttempt()
    {
        if (TryHookNativeScrollViewer())
        {
            ScheduleViewportFillCheck();
            return;
        }

        // Le template natif n'est pas forcément réalisé au moment de Loaded (constaté : plusieurs
        // centaines de ms sur Windows dans DmTools) - quelques tentatives espacées.
        if (++_hookAttempts >= 20 || _collectionView is null) return;
        _collectionView.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), ScheduleHookAttempt);
    }

    private void ScheduleViewportFillCheck() =>
        _collectionView?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () => FillViewport(attemptsLeft: 10));

    /// <summary>Tant qu'il n'y a rien à faire défiler (ScrollableHeight == 0) et qu'il reste des pages,
    /// en charge une de plus - borné pour ne jamais boucler si ScrollableHeight ne bouge pas.</summary>
    private void FillViewport(int attemptsLeft)
    {
        if (_nativeScrollViewer is null || _collectionView is null || Source is not { HasMore: true }) return;
        // Onglet du Codex masqué (IsVisible sur un ancêtre, voir LibraryPage) : zone visible nulle, donc
        // "rien à défiler" - sans ce garde, toutes ses pages se chargeraient en arrière-plan. Revérifié
        // au réaffichage (SizeChanged).
        if (!IsVisibleInTree(_collectionView) || _nativeScrollViewer.ViewportHeight <= 0) return;
        if (_nativeScrollViewer.ScrollableHeight > 0) return;

        LoadMoreIfPossible();
        if (attemptsLeft > 0)
            _collectionView.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => FillViewport(attemptsLeft - 1));
    }

    private bool TryHookNativeScrollViewer()
    {
        if (_nativeScrollViewer is not null) return true;
        if (_collectionView?.Handler?.PlatformView is not Microsoft.UI.Xaml.DependencyObject platformView) return false;

        _nativeScrollViewer = FindDescendant<Microsoft.UI.Xaml.Controls.ScrollViewer>(platformView);
        if (_nativeScrollViewer is null) return false;

        _nativeScrollViewer.ViewChanged += OnNativeViewChanged;
        // Réaffichage d'un onglet masqué / redimensionnement : la zone visible native change de taille.
        _nativeScrollViewer.SizeChanged += OnNativeSizeChanged;
        return true;
    }

    private void OnNativeSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e) => ScheduleViewportFillCheck();

    private void OnNativeViewChanged(object? sender, Microsoft.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.Controls.ScrollViewer scrollViewer) return;

        const double thresholdPixels = 400;
        if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - thresholdPixels)
            LoadMoreIfPossible();
    }

    private static bool IsVisibleInTree(Element? element)
    {
        for (; element is not null; element = element.Parent)
            if (element is VisualElement { IsVisible: false }) return false;
        return true;
    }

    private static T? FindDescendant<T>(Microsoft.UI.Xaml.DependencyObject root) where T : Microsoft.UI.Xaml.DependencyObject
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T target) return target;
            if (FindDescendant<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
#endif
}
