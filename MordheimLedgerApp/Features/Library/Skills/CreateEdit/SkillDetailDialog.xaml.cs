using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

/// <summary>Pure XAML wrapper bound to SkillDetailDialogViewModel: all logic lives there, not here.</summary>
public partial class SkillDetailDialog : DialogContent<bool>
{
    public SkillDetailDialog(SkillDetailDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
