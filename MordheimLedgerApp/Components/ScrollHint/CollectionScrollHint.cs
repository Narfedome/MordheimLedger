using System.Collections.Specialized;

namespace MordheimLedgerApp.Components;

/// <summary>Pendant de VerticalScrollHint pour une CollectionView - le MAUI ne donne ni offset maximal ni
/// "peut encore défiler", donc la question est posée au contrôle natif : RecyclerView.
/// CanScrollVertically(1) sur Android, le ScrollViewer natif sur Windows (même contournement que
/// LoadMoreOnScrollBehavior - Scrolled ne s'y déclenche jamais pour une CollectionView). Autres
/// plateformes : pas d'indice (badge jamais affiché), comportement inchangé.</summary>
public static class CollectionScrollHint
{
    public static void Attach(CollectionView collectionView, View downHint)
    {
        void Update() => downHint.IsVisible = CanScrollDown(collectionView);

        // Le contenu natif n'est à jour qu'après le layout - revérification différée à chaque déclencheur.
        void ScheduleUpdate() => collectionView.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), Update);

#if WINDOWS
        var hooked = false;
        void TryHookWindows()
        {
            if (hooked) return;
            HookWindowsScrollViewer(collectionView, Update, attemptsLeft: 20, onHooked: () => hooked = true);
        }
#endif

        INotifyCollectionChanged? observedSource = null;
        void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleUpdate();

        void ObserveItemsSource()
        {
            if (observedSource is not null) observedSource.CollectionChanged -= OnItemsChanged;
            observedSource = collectionView.ItemsSource as INotifyCollectionChanged;
            if (observedSource is not null) observedSource.CollectionChanged += OnItemsChanged;
            ScheduleUpdate();
#if WINDOWS
            // Onglet du Codex masqué au chargement : pas encore de ScrollViewer natif (gabarit non réalisé) -
            // ses données n'arrivent qu'à sa première visite, une fois visible.
            TryHookWindows();
#endif
        }

        collectionView.Scrolled += (_, _) => Update();
        collectionView.SizeChanged += (_, _) => ScheduleUpdate();
        collectionView.Loaded += (_, _) => ScheduleUpdate();
        collectionView.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CollectionView.ItemsSource)) ObserveItemsSource();
        };
        ObserveItemsSource();
#if WINDOWS
        collectionView.Loaded += (_, _) => TryHookWindows();
#endif

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => ScrollDownOnePage(collectionView);
        downHint.GestureRecognizers.Add(tap);
    }

    private static bool CanScrollDown(CollectionView collectionView)
    {
#if ANDROID
        return collectionView.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recyclerView
               && recyclerView.CanScrollVertically(1);
#elif WINDOWS
        return FindScrollViewer(collectionView) is { } scrollViewer
               && scrollViewer.ScrollableHeight > 1
               && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - 1;
#else
        return false;
#endif
    }

    private static void ScrollDownOnePage(CollectionView collectionView)
    {
#if ANDROID
        if (collectionView.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recyclerView)
            recyclerView.SmoothScrollBy(0, (int)(recyclerView.Height * 0.8));
#elif WINDOWS
        if (FindScrollViewer(collectionView) is { } scrollViewer)
            scrollViewer.ChangeView(null, scrollViewer.VerticalOffset + scrollViewer.ViewportHeight * 0.8, null);
#endif
    }

#if WINDOWS
    private static Microsoft.UI.Xaml.Controls.ScrollViewer? FindScrollViewer(CollectionView collectionView) =>
        collectionView.Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject root
            ? FindDescendant<Microsoft.UI.Xaml.Controls.ScrollViewer>(root)
            : null;

    /// <summary>Le ScrollViewer natif n'existe qu'une fois le gabarit réalisé (plusieurs centaines de ms
    /// après Loaded, voir LoadMoreOnScrollBehavior) - quelques tentatives espacées, puis abonnement à ses
    /// propres événements (défilement, redimensionnement).</summary>
    private static void HookWindowsScrollViewer(CollectionView collectionView, Action update, int attemptsLeft, Action onHooked)
    {
        if (FindScrollViewer(collectionView) is { } scrollViewer)
        {
            onHooked();
            scrollViewer.ViewChanged += (_, _) => update();
            scrollViewer.SizeChanged += (_, _) => update();
            update();
            return;
        }
        if (attemptsLeft > 0)
            collectionView.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () => HookWindowsScrollViewer(collectionView, update, attemptsLeft - 1, onHooked));
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
