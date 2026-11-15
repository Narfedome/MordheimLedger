using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Exchange;

/// <summary>QR code(s) d'une bande ou d'une entrée du Codex (v2, lot 3) : un seul QR, ou plusieurs à faire scanner
/// l'un après l'autre (Précédent / Suivant, « QR 2/3 »), voir Core.Exchange.ExchangeQrCodes.</summary>
public partial class QrCodeDialogViewModel : ReadOnlyDialogViewModel
{
    private readonly IReadOnlyList<string> _codes;

    public QrCodeDialogViewModel(string title, IReadOnlyList<string> codes, bool isWarband)
    {
        Title = title;
        _codes = codes;
        Hint = Loc[isWarband ? "QrWarbandHint" : "QrCatalogHint"];
    }

    public string Hint { get; }
    public bool HasSeveralCodes => _codes.Count > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCode), nameof(PositionText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand), nameof(NextCommand))]
    private int index;

    public string CurrentCode => _codes[Index];
    public string PositionText => string.Format(Loc["QrPosition"], Index + 1, _codes.Count);

    private bool CanGoBack => Index > 0;
    private bool CanGoForward => Index < _codes.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Previous() => Index--;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void Next() => Index++;
}
