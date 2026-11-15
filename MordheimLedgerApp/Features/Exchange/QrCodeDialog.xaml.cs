using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Exchange;

/// <summary>QR code(s) à faire scanner (voir QrCodeDialogViewModel).</summary>
public partial class QrCodeDialog : DialogContent<bool>
{
    public QrCodeDialog(QrCodeDialogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
