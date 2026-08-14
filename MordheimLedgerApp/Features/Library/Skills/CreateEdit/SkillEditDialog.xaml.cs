using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public partial class SkillEditDialog : DialogContent<bool>
{
    public SkillEditDialog(SkillEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
