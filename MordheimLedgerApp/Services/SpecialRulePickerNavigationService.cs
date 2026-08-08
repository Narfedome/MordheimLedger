using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface ISpecialRulePickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SpecialRule>> tcs);
    Task ClosePickerAsync(IReadOnlyList<SpecialRule> result);

    /// <summary>Set by SpecialRulePickerService.PickSpecialRulesAsync right before pushing the modal - read
    /// by SpecialRuleViewModel.LoadData (selector mode only) to filter the catalog. Null = unfiltered
    /// (Animal/EquipmentItem callers, unchanged behavior).</summary>
    SpecialRuleScope? RequestedScope { get; set; }
}

public class SpecialRulePickerNavigationService : ISpecialRulePickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<SpecialRule>>? _tcs;

    public SpecialRuleScope? RequestedScope { get; set; }

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SpecialRule>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<SpecialRule> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
