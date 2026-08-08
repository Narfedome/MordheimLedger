using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

public partial class WarbandArchetypeEditDialog : Popup<bool>
{
    // Le plus grand contenu jamais mesuré (voir OnRootContentSizeChanged), même mécanisme que
    // WarbandArchetypeDetailDialog pour éviter le saut de taille au changement d'onglet.
    private double _maxContentHeight;

    public WarbandArchetypeEditDialog(WarbandArchetypeEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
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
