using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

public partial class WarbandEditDialog : DialogContent<bool>
{
    // Le plus grand contenu jamais mesuré (voir OnRootContentSizeChanged), même mécanisme que
    // WarbandArchetypeDetailDialog pour éviter le saut de taille au changement d'onglet.
    private double _maxContentHeight;

    public WarbandEditDialog(WarbandEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnRootContentSizeChanged(object? sender, EventArgs e)
    {
        if (RootContent.Height <= 0) return;

        var newMax = Math.Max(_maxContentHeight, RootContent.Height);
        if (newMax <= _maxContentHeight) return;

        _maxContentHeight = newMax;
        ContentScroll.HeightRequest = Math.Min(_maxContentHeight, DialogSizing.MaxContentHeight());
    }
}
