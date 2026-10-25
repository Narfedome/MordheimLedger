using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Photo de la figurine en grand, ouverte d'un tap sur la miniature de la carte guerrier
/// (WarbandDetailViewModel.ShowWarriorPhoto) - lecture seule, un tap n'importe où la referme.</summary>
public partial class WarriorPhotoDialogViewModel : ReadOnlyDialogViewModel
{
    public string PhotoSource { get; }

    public WarriorPhotoDialogViewModel(string name, string photoSource)
    {
        Title = name;
        PhotoSource = photoSource;
    }
}
