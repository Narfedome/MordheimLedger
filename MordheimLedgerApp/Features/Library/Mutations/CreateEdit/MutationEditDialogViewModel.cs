using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

public partial class MutationEditDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    [ObservableProperty]
    private Mutation item;

    [ObservableProperty]
    private string title;

    public MutationEditDialogViewModel(Mutation item, string title)
    {
        this.item = item;
        this.title = title;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
