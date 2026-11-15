using MordheimLedgerApp.Components.FormPage;
using ZXing.Net.Maui;

namespace MordheimLedgerApp.Features.Exchange;

/// <summary>Lecture de QR codes par la caméra (voir QrScanViewModel). Android seulement : pas de webcam sous Windows
/// (décision 1.3), où l'équivalent d'un QR est le fichier .mordheim.</summary>
public partial class QrScanForm : FormContent<QrScanResult?>
{
    private readonly QrScanViewModel _viewModel;

    public QrScanForm(QrScanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Reader.Options = new BarcodeReaderOptions { Formats = BarcodeFormat.QrCode, AutoRotate = true, Multiple = false };
        // Caméra coupée dès la fermeture (Annuler, ✕, retour, envoi complet) : la page peut rester en mémoire un instant.
        viewModel.CloseRequested += _ => Reader.IsDetecting = false;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        // Événement sur le thread de la caméra.
        if (e.Results.FirstOrDefault()?.Value is not { Length: > 0 } text) return;
        MainThread.BeginInvokeOnMainThread(() => _viewModel.OnScanned(text));
    }
}
