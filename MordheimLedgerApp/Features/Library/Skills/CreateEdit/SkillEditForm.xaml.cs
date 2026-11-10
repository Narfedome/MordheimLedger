using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public partial class SkillEditForm : FormContent<bool>
{
    public SkillEditForm(SkillEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
