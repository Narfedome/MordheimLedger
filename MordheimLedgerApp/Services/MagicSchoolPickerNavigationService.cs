using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IMagicSchoolPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<MagicSchool>> tcs);
    Task ClosePickerAsync(IReadOnlyList<MagicSchool> result);
}

public class MagicSchoolPickerNavigationService : IMagicSchoolPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<MagicSchool>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<MagicSchool>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<MagicSchool> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "MagicSchoolPicker.Pop");
    }
}
