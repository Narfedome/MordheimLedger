using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IHiredSwordPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<HiredSword>> tcs);
    Task ClosePickerAsync(IReadOnlyList<HiredSword> result);
}

public class HiredSwordPickerNavigationService : IHiredSwordPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<HiredSword>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<HiredSword>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<HiredSword> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "HiredSwordPicker.Pop");
    }
}
