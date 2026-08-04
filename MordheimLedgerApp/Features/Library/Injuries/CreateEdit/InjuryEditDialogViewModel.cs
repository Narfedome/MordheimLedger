using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

public partial class InjuryEditDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    [ObservableProperty]
    private Injury item;

    [ObservableProperty]
    private string title;

    public InjuryEditDialogViewModel(Injury item, string title)
    {
        this.item = item;
        this.title = title;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
