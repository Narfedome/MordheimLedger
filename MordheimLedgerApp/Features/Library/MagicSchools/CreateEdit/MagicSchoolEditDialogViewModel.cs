using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.MagicSchools.CreateEdit;

public partial class MagicSchoolEditDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    [ObservableProperty]
    private MagicSchool item;

    [ObservableProperty]
    private string title;

    public MagicSchoolEditDialogViewModel(MagicSchool item, string title)
    {
        this.item = item;
        this.title = title;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
