using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.Spells.CreateEdit;

public partial class SpellEditForm : FormContent<bool>
{
    public SpellEditForm(SpellEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
