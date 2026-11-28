using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Feedback;

/// <summary>Formulaire « Nous écrire », ouvert depuis Paramètres.</summary>
public partial class FeedbackForm : FormContent<bool>
{
    public FeedbackForm(FeedbackFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
