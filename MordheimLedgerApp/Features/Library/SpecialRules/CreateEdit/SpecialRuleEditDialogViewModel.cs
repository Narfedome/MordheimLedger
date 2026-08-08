using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

public partial class SpecialRuleEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, SpecialRuleScope> _scopeByLabel = new();

    protected override bool CancelResult => false;

    [ObservableProperty]
    private SpecialRule item;

    [ObservableProperty]
    private string title;

    public ObservableCollection<string> ScopeOptions { get; } = new();

    [ObservableProperty]
    private string selectedScopeLabel = string.Empty;

    public SpecialRuleEditDialogViewModel(SpecialRule item, string title)
    {
        this.item = item;
        this.title = title;

        foreach (var scope in Enum.GetValues<SpecialRuleScope>())
        {
            var label = Loc[$"SpecialRuleScope{scope}"];
            _scopeByLabel[label] = scope;
            ScopeOptions.Add(label);
        }
        selectedScopeLabel = Loc[$"SpecialRuleScope{item.Scope}"];
    }

    partial void OnSelectedScopeLabelChanged(string value)
    {
        if (_scopeByLabel.TryGetValue(value, out var scope))
            Item.Scope = scope;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
