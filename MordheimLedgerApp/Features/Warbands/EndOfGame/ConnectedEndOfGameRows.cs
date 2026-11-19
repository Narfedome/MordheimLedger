using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Lobby;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Un vainqueur possible de la partie connectée (un joueur, ou null pour le match nul).</summary>
public sealed partial class WinnerOptionRow(string? playerId, string label) : ObservableObject
{
    public string? PlayerId { get; } = playerId;
    public string Label { get; } = label;

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>Un adversaire qui a pu capturer un guerrier.</summary>
public sealed record CaptorOption(string PlayerId, string Label);

/// <summary>Captif connecté vu de son propriétaire (maquette « Fin de partie connectée », variante A : sous le jet 61 de
/// l'étape Blessures). Reflète l'état du registre (LobbySession.Captures) ; les actions passent par le ViewModel.</summary>
public sealed partial class ConnectedCaptureState : ObservableObject
{
    private readonly WarriorOutcomeRow _row;

    public ConnectedCaptureState(WarriorOutcomeRow row, IReadOnlyList<CaptorOption> captors)
    {
        _row = row;
        Captors = captors;
        // Un seul adversaire : c'est lui, rien à choisir.
        if (captors.Count == 1) selectedCaptor = captors[0];
    }

    public IReadOnlyList<CaptorOption> Captors { get; }
    public bool HasSeveralCaptors => Captors.Count > 1;

    [ObservableProperty]
    private CaptorOption? selectedCaptor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaiting), nameof(HasOffer), nameof(IsManual), nameof(IsSettled))]
    private CaptureStatus? status;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string offerText = string.Empty;

    [ObservableProperty]
    private string treasuryText = string.Empty;

    public bool IsWaiting => Status is CaptureStatus.AwaitingOffer || (Status is null && SelectedCaptor is not null);
    public bool HasOffer => Status is CaptureStatus.Offered;
    public bool IsManual => Status is CaptureStatus.Manual;
    public bool IsSettled => Status is CaptureStatus.Accepted or CaptureStatus.Refused;

    /// <summary>Le propriétaire doit encore attendre ou répondre : la ligne bloque Suivant.</summary>
    public bool IsPending => !IsManual && !IsSettled;

    partial void OnStatusChanged(CaptureStatus? value) => _row.NotifyConnectedCaptureChanged();

    private bool _localManual;

    /// <summary>« Décider à la table » avant même d'avoir déclaré le captif (capteur pas encore choisi).</summary>
    public void GoManualLocally()
    {
        _localManual = true;
        Status = CaptureStatus.Manual;
    }

    public void Update(Capture? capture, int treasury)
    {
        Status = capture?.Status ?? (_localManual ? CaptureStatus.Manual : null);
        var captor = SelectedCaptor?.Label ?? string.Empty;
        var loc = LocalizationService.Instance;
        StatusText = Status switch
        {
            CaptureStatus.AwaitingOffer => string.Format(loc["ConnectedCaptureWaiting"], captor),
            CaptureStatus.Offered => string.Empty, // la proposition s'affiche en dessous (OfferText)
            CaptureStatus.Accepted => string.Format(loc["ConnectedCapturePaid"], capture?.Amount),
            CaptureStatus.Refused => loc["ConnectedCaptureRefused"],
            CaptureStatus.Manual => loc["ConnectedCaptureManual"],
            _ => SelectedCaptor is null ? loc["ConnectedCaptureChooseCaptor"] : string.Format(loc["ConnectedCaptureWaiting"], captor),
        };
        OfferText = capture is { Status: CaptureStatus.Offered, Amount: { } amount } ? string.Format(loc["ConnectedCaptureOffer"], captor, amount) : string.Empty;
        TreasuryText = capture is { Status: CaptureStatus.Offered, Amount: { } price }
            ? string.Format(loc["ConnectedCaptureTreasury"], treasury, treasury - price)
            : string.Empty;
    }
}

/// <summary>Captif connecté vu de son capteur (étape Prisonniers ennemis) : proposer une rançon, attendre la réponse, puis
/// décider du sort sur un refus (même choix qu'un prisonnier saisi à la main, sans la rançon).</summary>
public sealed partial class ConnectedCaptiveRow : ObservableObject
{
    public ConnectedCaptiveRow(Capture capture, string ownerName, bool isUndeadWarband, bool isPossessedWarband)
    {
        Id = capture.Id;
        Title = capture.WarriorName;
        Detail = string.Format(LocalizationService.Instance["ConnectedCaptiveDetail"], capture.RoleName, ownerName, capture.WarbandName);
        OwnerName = ownerName;
        RefusedFate = new CapturedEnemyEntry(0, isUndeadWarband, isPossessedWarband, LocalizationService.Instance, allowRansom: false);
        Update(capture);
    }

    public string Id { get; }
    public string Title { get; }
    public string Detail { get; }
    public string OwnerName { get; }

    /// <summary>Sort choisi après une rançon refusée.</summary>
    public CapturedEnemyEntry RefusedFate { get; }

    [ObservableProperty]
    private string ransomInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOffer), nameof(IsOffered), nameof(IsAccepted), nameof(IsRefused), nameof(IsManual))]
    private CaptureStatus status;

    [ObservableProperty]
    private int? amount;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string? error;

    public bool CanOffer => Status is CaptureStatus.AwaitingOffer;
    public bool IsOffered => Status is CaptureStatus.Offered;
    public bool IsAccepted => Status is CaptureStatus.Accepted;
    public bool IsRefused => Status is CaptureStatus.Refused;
    public bool IsManual => Status is CaptureStatus.Manual;

    public void Update(Capture capture)
    {
        Status = capture.Status;
        Amount = capture.Amount;
        var loc = LocalizationService.Instance;
        StatusText = capture.Status switch
        {
            CaptureStatus.Offered => string.Format(loc["ConnectedCaptiveOffered"], capture.Amount, OwnerName),
            CaptureStatus.Accepted => string.Format(loc["ConnectedCaptiveAccepted"], capture.Amount),
            CaptureStatus.Refused => string.Format(loc["ConnectedCaptiveRefused"], OwnerName),
            CaptureStatus.Manual => loc["ConnectedCaptiveManual"],
            _ => string.Empty,
        };
        if (capture.Status != CaptureStatus.AwaitingOffer) Error = null;
    }
}
