using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface IMutationPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Mutation>> tcs);
    Task ClosePickerAsync(IReadOnlyList<Mutation> result);
}

public class MutationPickerNavigationService : IMutationPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<Mutation>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Mutation>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<Mutation> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
