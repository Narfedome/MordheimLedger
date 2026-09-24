using System.ComponentModel;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Enveloppe générique "ScrollView de dialog qui suit la hauteur de son contenu en l'animant",
/// plafonnée par DialogSizing.MaxContentHeight() pour rester scrollable plutôt que déborder de l'écran.
/// La toute première mesure se fixe sans animation (taille d'ouverture du dialog) ; tout changement
/// suivant (onglet plus grand OU plus petit, données chargées en différé, section repliée...) est animé
/// dans les deux sens plutôt qu'assigné d'un coup. Remplace StableHeightScrollView (2026-09-24, retour
/// utilisateur), qui retenait la plus grande hauteur jamais mesurée et ne faisait donc que grandir -
/// un dialog restait plus grand que son contenu après être passé par un onglet plus haut.</summary>
public partial class AnimatedHeightScrollView : ContentView
{
    private const string ResizeAnimationName = "DialogResize";
    private const uint ResizeAnimationLength = 180;

    private ScrollView? _scrollView;
    private double _targetHeight;
    private bool _hasMeasured;
    private bool _updatePending;

    public AnimatedHeightScrollView()
    {
        InitializeComponent();
        PropertyChanged += OnOwnPropertyChanged;
    }

    private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Content) || Content is null) return;
        // SizeChanged seul ne suffit pas pour RÉTRÉCIR : un ScrollView étire son contenu au moins à sa
        // propre hauteur visible, donc une fois le dialog agrandi, la hauteur arrangée du contenu
        // (content.Height) ne redescend plus et SizeChanged ne se redéclenche même pas.
        // MeasureInvalidated remonte depuis n'importe quel descendant (texte, IsVisible, onglet...).
        Content.SizeChanged += OnContentChanged;
        Content.MeasureInvalidated += OnContentChanged;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _scrollView ??= GetTemplateChild("PartScrollView") as ScrollView;
    }

    /// <summary>Regroupe les rafales d'invalidations d'un même changement en une seule mise à jour,
    /// exécutée après la passe courante.</summary>
    private void OnContentChanged(object? sender, EventArgs e)
    {
        if (_updatePending) return;
        _updatePending = true;
        Dispatcher.Dispatch(() =>
        {
            _updatePending = false;
            UpdateHeight();
        });
    }

    private void UpdateHeight()
    {
        if (_scrollView is null || Content is not View content || content.Width <= 0) return;

        // Hauteur DÉSIRÉE (mesure sans contrainte verticale, comme le ScrollView lui-même), pas la
        // hauteur arrangée - voir OnOwnPropertyChanged.
        var desired = content.Measure(content.Width, double.PositiveInfinity).Height;
        if (desired <= 0) return;

        var target = Math.Min(desired, DialogSizing.MaxContentHeight());
        // Sous le demi-pixel : arrondis de mesure, pas un vrai changement - évite de relancer une
        // animation pour rien.
        if (_hasMeasured && Math.Abs(target - _targetHeight) < 0.5) return;
        _targetHeight = target;

        if (!_hasMeasured)
        {
            _hasMeasured = true;
            _scrollView.HeightRequest = target;
            return;
        }

        // Repart de la hauteur actuelle (éventuellement en cours d'animation) - un contenu qui change
        // encore pendant l'animation enchaîne en douceur au lieu de sauter.
        var from = _scrollView.HeightRequest;
        _scrollView.AbortAnimation(ResizeAnimationName);
        new Animation(v => _scrollView.HeightRequest = v, from, target)
            .Commit(_scrollView, ResizeAnimationName, length: ResizeAnimationLength, easing: Easing.CubicOut);
    }
}
