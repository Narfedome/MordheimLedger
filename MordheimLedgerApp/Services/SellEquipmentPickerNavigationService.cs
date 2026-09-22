using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Features.Warbands.EndOfGame;

namespace MordheimLedgerApp.Services;

public interface ISellEquipmentPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SellableEquipmentCandidate>> tcs);
    Task ClosePickerAsync(IReadOnlyList<SellableEquipmentCandidate> result);
}

public class SellEquipmentPickerNavigationService : ISellEquipmentPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<SellableEquipmentCandidate>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SellableEquipmentCandidate>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<SellableEquipmentCandidate> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "SellEquipmentPicker.Pop");
    }
}
