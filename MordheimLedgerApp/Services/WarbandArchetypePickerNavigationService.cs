using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IWarbandArchetypePickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<WarbandArchetype>> tcs);
    void RegisterTaskSource(TaskCompletionSource<WarbandArchetype> tcs);
    Task ClosePickerAsync(IReadOnlyList<WarbandArchetype> result);
    Task ClosePickerAsync(WarbandArchetype result);
}

public class WarbandArchetypePickerNavigationService : IWarbandArchetypePickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<WarbandArchetype>>? _tcs;
    private TaskCompletionSource<WarbandArchetype>? _tcsSingle;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<WarbandArchetype>> tcs) => _tcs = tcs;
    public void RegisterTaskSource(TaskCompletionSource<WarbandArchetype> tcs) => _tcsSingle = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<WarbandArchetype> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "WarbandArchetypePicker.Pop");
    }
    public async Task ClosePickerAsync(WarbandArchetype result)
    {
        _tcsSingle?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "WarbandArchetypePicker.Pop");
    }
}
