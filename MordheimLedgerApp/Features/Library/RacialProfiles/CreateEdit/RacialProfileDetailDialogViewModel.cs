using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.RacialProfiles.CreateEdit;

/// <summary>Read-only recap de RacialProfileEditDialog - même motif que WarriorArchetypeDetailDialog
/// (base ReadOnlyDialogViewModel, StatRowView partagé en lecture seule pour la grille des 9
/// maximums).</summary>
public partial class RacialProfileDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public RacialProfile Item { get; }

    public RacialProfileDetailDialogViewModel(RacialProfile item)
    {
        Item = item;
        Title = item.Name;
    }
}
