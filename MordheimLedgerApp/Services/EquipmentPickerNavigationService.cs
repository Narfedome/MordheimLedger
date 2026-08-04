using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface IEquipmentPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<EquipmentItem>> tcs);
    Task ClosePickerAsync(IReadOnlyList<EquipmentItem> result);
}

public class EquipmentPickerNavigationService : IEquipmentPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<EquipmentItem>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<EquipmentItem>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<EquipmentItem> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
