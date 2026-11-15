using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Exchange;

namespace MordheimLedgerApp.Features.Exchange;

/// <summary>Issue du scan : le paquet lu, ou le joueur préfère choisir un fichier.</summary>
public sealed record QrScanResult(ExchangePackage? Package, bool PickFile);

/// <summary>Lecture de QR codes par la caméra (v2, lot 3, Android) : un QR, ou plusieurs lus dans n'importe quel ordre
/// (ExchangeQrCodes.Collector). Se ferme dès que l'envoi est complet ; un QR étranger affiche un message sans bloquer.</summary>
public partial class QrScanViewModel : DialogViewModel<QrScanResult?>
{
    private readonly ExchangeQrCodes.Collector _collector = new();
    private string? _lastCode;
    private bool _closed;

    protected override QrScanResult? CancelResult => null;

    public string Title => Loc["QrScanTitle"];

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private bool isWarning;

    public QrScanViewModel() => Status = Loc["QrScanHint"];

    /// <summary>Un QR lu par la caméra (déjà sur le thread UI). La caméra relit le même QR plusieurs fois par seconde :
    /// seul un code différent du précédent compte.</summary>
    public void OnScanned(string text)
    {
        if (_closed || text == _lastCode) return;
        _lastCode = text;
        try
        {
            if (_collector.Add(text) is { } package)
            {
                _closed = true;
                Close(new QrScanResult(package, PickFile: false));
                return;
            }
            IsWarning = false;
            Status = string.Format(Loc["QrScanProgress"], _collector.Received, _collector.Total);
        }
        catch (ExchangeFormatException)
        {
            IsWarning = true;
            Status = Loc["QrScanForeign"];
        }
    }

    [RelayCommand]
    private void PickFile() => Close(new QrScanResult(null, PickFile: true));
}
