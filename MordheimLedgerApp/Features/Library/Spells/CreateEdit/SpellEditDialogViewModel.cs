using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Spells.CreateEdit;

public partial class SpellEditDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    [ObservableProperty]
    private Spell item;

    [ObservableProperty]
    private string title;

    public SpellEditDialogViewModel(Spell item, string title)
    {
        this.item = item;
        this.title = title;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
