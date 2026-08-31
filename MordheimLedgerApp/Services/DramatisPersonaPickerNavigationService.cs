using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IDramatisPersonaPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<DramatisPersona>> tcs);
    Task ClosePickerAsync(IReadOnlyList<DramatisPersona> result);
}

public class DramatisPersonaPickerNavigationService : IDramatisPersonaPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<DramatisPersona>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<DramatisPersona>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<DramatisPersona> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "DramatisPersonaPicker.Pop");
    }
}
