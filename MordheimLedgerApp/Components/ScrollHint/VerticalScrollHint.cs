namespace MordheimLedgerApp.Components;

/// <summary>Indice "il reste du contenu plus bas" pour un ScrollView vertical (2026-09-24, retour
/// utilisateur - sur mobile la barre de défilement native est à peine visible) : pilote un badge ⌄ posé
/// par l'appelant en surimpression au bas du ScrollView - visible seulement tant qu'on n'est pas en fin
/// de course, tapable pour descendre d'environ une hauteur visible. Pendant vertical du badge ‹ / › de
/// ScrollableTabStripView. Utilisé par AnimatedHeightScrollView (tous les dialogs) et EndOfGamePage.</summary>
public static class VerticalScrollHint
{
    public static void Attach(ScrollView scrollView, View downHint)
    {
        void Update()
        {
            var maxScrollY = scrollView.ContentSize.Height - scrollView.Height;
            downHint.IsVisible = maxScrollY > 1 && scrollView.ScrollY < maxScrollY - 1;
        }

        scrollView.Scrolled += (_, _) => Update();
        scrollView.SizeChanged += (_, _) => Update();
        // ContentSize change au chargement des données, au changement d'onglet/d'étape...
        scrollView.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScrollView.ContentSize)) Update();
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            var maxScrollY = Math.Max(0, scrollView.ContentSize.Height - scrollView.Height);
            var target = Math.Min(scrollView.ScrollY + scrollView.Height * 0.8, maxScrollY);
            await scrollView.ScrollToAsync(0, target, animated: true);
        };
        downHint.GestureRecognizers.Add(tap);

        Update();
    }
}
