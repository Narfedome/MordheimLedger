using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Spells.CreateEdit;

/// <summary>Read-only recap of SpellEditDialog.</summary>
public partial class SpellDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Spell Item { get; }

    public bool HasDifficulty => Item.Difficulty.HasValue;

    public SpellDetailDialogViewModel(Spell item)
    {
        Item = item;
        Title = item.Name;
    }
}
