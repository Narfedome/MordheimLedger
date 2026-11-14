using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Page ouverte par le crayon de la carte guerrier : photo et nom, plus l'accès à « Corriger la fiche »
/// (WarriorEditForm). Même ViewModel que la correction - la logique photo n'existe qu'une fois.</summary>
public partial class WarriorQuickEditForm : FormContent<bool>
{
    public WarriorQuickEditForm(WarriorEditDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
