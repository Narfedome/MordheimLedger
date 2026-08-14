using System.ComponentModel;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Enveloppe générique "ScrollView de dialog qui grandit sans jamais rétrécir" - factorise le
/// mécanisme _maxContentHeight/OnRootContentSizeChanged jusqu'ici dupliqué par dialog (WarbandEditDialog,
/// WarbandArchetypeEditDialog, WarbandArchetypeDetailDialog). Retient le plus grand contenu jamais
/// mesuré (pas de saut de taille en changeant d'onglet ou en chargeant des données différées), plafonné
/// par DialogSizing.MaxContentHeight() pour rester scrollable plutôt que déborder de l'écran. La toute
/// première mesure se fixe sans animation (taille d'ouverture du dialog) ; toute croissance suivante
/// (onglet plus grand que tous les précédents visité pour la première fois) est animée plutôt
/// qu'assignée d'un coup - le saut de taille reste réel (on ne connaît la hauteur d'un onglet qu'une
/// fois affiché) mais devient un mouvement fluide au lieu d'un à-coup.</summary>
public partial class StableHeightScrollView : ContentView
{
    private const uint GrowAnimationLength = 180;

    private ScrollView? _scrollView;
    private double _maxContentHeight;
    private bool _hasMeasured;

    public StableHeightScrollView()
    {
        InitializeComponent();
        PropertyChanged += OnOwnPropertyChanged;
    }

    private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Content) || Content is null) return;
        Content.SizeChanged += OnContentSizeChanged;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _scrollView ??= GetTemplateChild("PartScrollView") as ScrollView;
    }

    private void OnContentSizeChanged(object? sender, EventArgs e)
    {
        if (_scrollView is null || sender is not View content || content.Height <= 0) return;

        var newMax = Math.Max(_maxContentHeight, content.Height);
        if (newMax <= _maxContentHeight) return;

        var previous = _scrollView.HeightRequest;
        _maxContentHeight = newMax;
        var target = Math.Min(_maxContentHeight, DialogSizing.MaxContentHeight());

        if (!_hasMeasured)
        {
            _hasMeasured = true;
            _scrollView.HeightRequest = target;
            return;
        }

        _scrollView.AbortAnimation(nameof(GrowAnimationLength));
        new Animation(v => _scrollView.HeightRequest = v, previous, target)
            .Commit(_scrollView, nameof(GrowAnimationLength), length: GrowAnimationLength, easing: Easing.CubicOut);
    }
}
