using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public partial class SkillEffectEditForm : FormContent<SkillEffectEditResult>
{
    public SkillEffectEditForm(SkillEffectEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
