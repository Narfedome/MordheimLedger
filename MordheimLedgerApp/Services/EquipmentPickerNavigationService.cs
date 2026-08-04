using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface IEquipmentPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<EquipmentItem?> tcs);
    Task ClosePickerAsync(EquipmentItem? result);
}

public class EquipmentPickerNavigationService : IEquipmentPickerNavigationService
{
    private TaskCompletionSource<EquipmentItem?>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<EquipmentItem?> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(EquipmentItem? result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
