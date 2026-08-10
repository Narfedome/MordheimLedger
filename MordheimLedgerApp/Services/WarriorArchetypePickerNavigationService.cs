using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IWarriorArchetypePickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<WarriorArchetype>> tcs);
    Task ClosePickerAsync(IReadOnlyList<WarriorArchetype> result);
}

public class WarriorArchetypePickerNavigationService : IWarriorArchetypePickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<WarriorArchetype>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<WarriorArchetype>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<WarriorArchetype> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "WarriorArchetypePicker.Pop");
    }
}
