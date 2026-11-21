using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Rappel « Soutenir le projet » : Soutenir ouvre la page de dons (comme la carte des Paramètres), Plus tard
/// (ou ✕/retour) redemande plus loin, Ne plus demander l'arrête.</summary>
public partial class SupportReminderDialogViewModel : DialogViewModel<SupportReminderChoice>
{
    protected override SupportReminderChoice CancelResult => SupportReminderChoice.Later;

    [RelayCommand]
    private async Task Support()
    {
        Close(SupportReminderChoice.Support);
        await Launcher.OpenAsync(SupportReminderService.SupportUri);
    }

    [RelayCommand]
    private void Never() => Close(SupportReminderChoice.Never);
}
