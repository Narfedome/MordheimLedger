namespace Plugin.Maui.Onboarding.Internals;

/// <summary>
/// Keyed registry of on-screen controls that can be spotlighted, populated by
/// <see cref="OnboardingTargetBehavior"/> as pages/components attach and detach. Weakly referenced so
/// registering a target never keeps its page alive after it's been navigated away from.
/// </summary>
/// <remarks>
/// Ajout Resonance : l'original ne gardait qu'un élément par clé (le dernier attaché). Une même clé peut
/// pourtant être portée par plusieurs éléments vivants - une ligne de liste répétée par son gabarit, ou
/// une vue réutilisée par une autre page encore en mémoire (Bibliothèque / sélecteur de pistes). On garde
/// donc tous les candidats et <see cref="TryGet"/> préfère le plus récent qui est visible sur la page
/// actuellement affichée par le Shell.
/// </remarks>
public static class OnboardingTargetRegistry
{
    private static readonly Dictionary<string, List<WeakReference<VisualElement>>> Targets = [];

    public static void Register(string key, VisualElement element)
    {
        if (!Targets.TryGetValue(key, out List<WeakReference<VisualElement>>? list))
            Targets[key] = list = [];

        list.RemoveAll(r => !r.TryGetTarget(out VisualElement? e) || ReferenceEquals(e, element));
        list.Add(new WeakReference<VisualElement>(element));
    }

    public static void Unregister(string key, VisualElement element)
    {
        if (Targets.TryGetValue(key, out List<WeakReference<VisualElement>>? list))
            list.RemoveAll(r => !r.TryGetTarget(out VisualElement? e) || ReferenceEquals(e, element));
    }

    public static bool TryGet(string key, out VisualElement? element)
    {
        element = null;
        if (!Targets.TryGetValue(key, out List<WeakReference<VisualElement>>? list))
            return false;

        list.RemoveAll(r => !r.TryGetTarget(out _));
        if (list.Count == 0)
            return false;

        Page? currentPage = Shell.Current?.CurrentPage;
        VisualElement? fallback = null;
        double topmost = double.MaxValue;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].TryGetTarget(out VisualElement? candidate))
                continue;

            fallback ??= candidate;
            if (!IsShownOn(candidate, currentPage))
                continue;

            // Ajout Mordheim Ledger : parmi les éléments affichés, le plus haut à l'écran plutôt que le
            // dernier attaché - une carte de guerrier répétée par son gabarit doit désigner la première
            // du roster, pas la dernière (souvent hors écran, le plugin ne fait pas défiler). À position
            // inconnue (pas encore mise en page), on garde l'ordre « plus récent d'abord ».
            double y = candidate.GetAbsoluteBounds()?.Y ?? double.MaxValue;
            if (element is null || y < topmost)
            {
                element = candidate;
                topmost = y;
            }
        }

        if (element is not null)
            return true;

        element = fallback;
        return element is not null;
    }

    private static bool IsShownOn(VisualElement element, Page? page)
    {
        Element? current = element;
        while (current is not null)
        {
            if (current is VisualElement { IsVisible: false })
                return false;
            if (current is Page owner)
                return page is null || ReferenceEquals(owner, page);

            current = current.Parent;
        }

        return false;
    }
}
