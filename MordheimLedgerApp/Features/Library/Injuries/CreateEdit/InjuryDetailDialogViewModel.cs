using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

/// <summary>Read-only recap of InjuryEditDialog.</summary>
public partial class InjuryDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Injury Item { get; }
    public string CategoryLabel { get; }

    public InjuryDetailDialogViewModel(Injury item)
    {
        Item = item;
        Title = item.Name;
        CategoryLabel = Loc[$"InjuryCategory{item.Category}"];
    }
}
