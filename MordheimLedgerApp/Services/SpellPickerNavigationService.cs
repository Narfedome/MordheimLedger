using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface ISpellPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Spell>> tcs);
    Task ClosePickerAsync(IReadOnlyList<Spell> result);
}

public class SpellPickerNavigationService : ISpellPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<Spell>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Spell>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<Spell> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "SpellPicker.Pop");
    }
}
