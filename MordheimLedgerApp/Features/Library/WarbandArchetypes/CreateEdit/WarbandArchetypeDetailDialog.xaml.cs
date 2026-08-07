using CommunityToolkit.Maui.Views;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

/// <summary>Pure XAML wrapper bound to WarbandArchetypeDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class WarbandArchetypeDetailDialog : Popup<bool>
{
    public WarbandArchetypeDetailDialog(WarbandArchetypeDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        // HeightRequest (fixe), pas MaximumHeightRequest (juste un plafond) comme les autres dialogs :
        // les 3 onglets (Général/Guerriers/Équipement) ont des hauteurs de contenu très différentes: sans
        // hauteur fixe, la carte du dialog se redimensionne visiblement à chaque changement d'onglet.
        // Un onglet plus court laisse juste de l'espace vide en bas plutôt que de faire "sauter" la carte.
        ContentScroll.HeightRequest = DialogSizing.MaxContentHeight();
        BindingContext = viewModel;
        viewModel.CloseRequested += async result => await CloseAsync(result);
    }
}
