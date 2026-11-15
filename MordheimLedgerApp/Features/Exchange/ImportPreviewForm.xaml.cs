using MordheimLedgerApp.Components.FormPage;
using MordheimLedgerApp.Core.Exchange;

namespace MordheimLedgerApp.Features.Exchange;

/// <summary>Écran d'import d'un fichier .mordheim (voir ImportPreviewViewModel).</summary>
public partial class ImportPreviewForm : FormContent<ImportDecisions?>
{
    public ImportPreviewForm(ImportPreviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
