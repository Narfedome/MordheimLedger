using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

public partial class SpecialRuleEditDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    [ObservableProperty]
    private SpecialRule item;

    [ObservableProperty]
    private string title;

    public SpecialRuleEditDialogViewModel(SpecialRule item, string title)
    {
        this.item = item;
        this.title = title;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
