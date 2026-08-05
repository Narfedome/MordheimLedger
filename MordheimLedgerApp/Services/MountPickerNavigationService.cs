using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface IMountPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Mount>> tcs);
    Task ClosePickerAsync(IReadOnlyList<Mount> result);
}

public class MountPickerNavigationService : IMountPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<Mount>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Mount>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<Mount> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
