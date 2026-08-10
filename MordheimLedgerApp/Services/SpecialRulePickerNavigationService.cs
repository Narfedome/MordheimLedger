using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

/// <summary>Pour quel contexte un appel de PickSpecialRulesAsync filtre-t-il - concept purement
/// applicatif (pas un champ de modèle persisté, contrairement à l'ex-SpecialRuleScope) : le filtrage
/// réel se déduit des tables de jointure existantes (WarbandArchetypeSpecialRuleEntity/
/// WarriorArchetypeSpecialRuleEntity), voir SpecialRuleViewModel.LoadData.</summary>
public enum SpecialRuleFilterKind
{
    Warband,
    Warrior
}

public interface ISpecialRulePickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SpecialRule>> tcs);
    Task ClosePickerAsync(IReadOnlyList<SpecialRule> result);

    /// <summary>Set by SpecialRulePickerService.PickSpecialRulesAsync right before pushing the modal - read
    /// by SpecialRuleViewModel.LoadData (selector mode only) to filter the catalog. Null = unfiltered
    /// (Animal/EquipmentItem callers, unchanged behavior).</summary>
    SpecialRuleFilterKind? RequestedFilterKind { get; set; }
}

public class SpecialRulePickerNavigationService : ISpecialRulePickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<SpecialRule>>? _tcs;

    public SpecialRuleFilterKind? RequestedFilterKind { get; set; }

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<SpecialRule>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<SpecialRule> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "SpecialRulePicker.Pop");
    }
}
