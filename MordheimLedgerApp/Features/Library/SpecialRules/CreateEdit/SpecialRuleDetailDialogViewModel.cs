using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

/// <summary>Read-only recap of SpecialRuleEditDialog - simplest of the 8 (just Name + Description).</summary>
public partial class SpecialRuleDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public SpecialRule Item { get; }

    public SpecialRuleDetailDialogViewModel(SpecialRule item)
    {
        Item = item;
        Title = item.Name;
    }
}
