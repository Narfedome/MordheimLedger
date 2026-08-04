using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface IInjuryPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Injury>> tcs);
    Task ClosePickerAsync(IReadOnlyList<Injury> result);
}

public class InjuryPickerNavigationService : IInjuryPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<Injury>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Injury>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<Injury> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
