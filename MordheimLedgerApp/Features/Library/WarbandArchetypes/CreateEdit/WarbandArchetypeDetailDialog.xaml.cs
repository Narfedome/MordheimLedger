using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

/// <summary>Pure XAML wrapper bound to WarbandArchetypeDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class WarbandArchetypeDetailDialog : DialogContent<bool>
{
    // Le plus grand contenu jamais mesuré (voir OnRootContentSizeChanged) - pas de valeur fixe arbitraire
    // (ex. moitié écran), qui laissait un grand vide sous l'onglet Général, le plus court des 3.
    private double _maxContentHeight;

    public WarbandArchetypeDetailDialog(WarbandArchetypeDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>RootContent (le seul enfant du ScrollView) se remesure à chaque bascule d'onglet et à
    /// chaque chargement différé (Guerriers/Équipement, vides puis remplis après leur premier passage) -
    /// on ne retient que le maximum jamais vu, plafonné par DialogSizing.MaxContentHeight() pour rester
    /// scrollable plutôt que déborder de l'écran sur une très longue liste. Conséquence acceptée : un
    /// saut de taille peut encore survenir la toute première fois qu'un onglet plus grand que tous les
    /// précédents est visité (sa hauteur réelle n'est connue qu'une fois ses données chargées) - mais
    /// plus aucun saut ensuite, y compris en revenant sur Général.</summary>
    private void OnRootContentSizeChanged(object? sender, EventArgs e)
    {
        if (RootContent.Height <= 0) return;

        var newMax = Math.Max(_maxContentHeight, RootContent.Height);
        if (newMax <= _maxContentHeight) return;

        _maxContentHeight = newMax;
        ContentScroll.HeightRequest = Math.Min(_maxContentHeight, DialogSizing.MaxContentHeight());
    }
}
