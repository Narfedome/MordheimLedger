using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.Races.CreateEdit;

public partial class RaceEditForm : FormContent<bool>
{
    public RaceEditForm(RaceEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
