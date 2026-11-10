using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Page plein écran de modification d'un guerrier (autrefois WarriorEditDialog) - toute la logique
/// reste dans WarriorEditDialogViewModel.</summary>
public partial class WarriorEditForm : FormContent<bool>
{
    public WarriorEditForm(WarriorEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
