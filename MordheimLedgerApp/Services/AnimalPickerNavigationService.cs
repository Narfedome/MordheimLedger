using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface IAnimalPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Animal>> tcs);
    Task ClosePickerAsync(IReadOnlyList<Animal> result);
}

public class AnimalPickerNavigationService : IAnimalPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<Animal>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Animal>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<Animal> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
