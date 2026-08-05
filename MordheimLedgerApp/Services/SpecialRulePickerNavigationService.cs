using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface ISpecialRulePickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SpecialRule>> tcs);
    Task ClosePickerAsync(IReadOnlyList<SpecialRule> result);
}

public class SpecialRulePickerNavigationService : ISpecialRulePickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<SpecialRule>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SpecialRule>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<SpecialRule> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
