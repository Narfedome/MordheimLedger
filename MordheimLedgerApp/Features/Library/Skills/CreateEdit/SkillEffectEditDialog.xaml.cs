using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public partial class SkillEffectEditDialog : DialogContent<SkillEffectEditResult>
{
    public SkillEffectEditDialog(SkillEffectEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
