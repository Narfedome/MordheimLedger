using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IWarbandArchetypePickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<WarbandArchetype>> tcs);
    Task ClosePickerAsync(IReadOnlyList<WarbandArchetype> result);
}

public class WarbandArchetypePickerNavigationService : IWarbandArchetypePickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<WarbandArchetype>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<WarbandArchetype>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<WarbandArchetype> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "WarbandArchetypePicker.Pop");
    }
}
