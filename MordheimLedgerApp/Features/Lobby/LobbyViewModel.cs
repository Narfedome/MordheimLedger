using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Lobby;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Lobby;

/// <summary>Un joueur de la salle d'attente.</summary>
public sealed record LobbyPlayerRow(string Name, string Detail, bool IsWaiting, string Icon);

/// <summary>Salle d'attente d'une partie connectée (v2, lot 4a - maquette « Partie connectée ») : QR et code à faire
/// scanner ou saisir (hôte), joueurs et leurs bandes. L'hôte commence quand chaque joueur a engagé sa bande. Résultat :
/// vrai si la partie a commencé, faux si ce joueur l'a quittée ou si elle a été fermée.</summary>
public partial class LobbyViewModel : DialogViewModel<bool>
{
    private readonly LobbySession _session;

    public LobbyViewModel(LobbySession session, LobbyHost? host)
    {
        _session = session;
        IsHost = host is not null;
        if (host?.AddressOnLocalNetwork() is { } address)
        {
            QrText = address.ToQrText();
            AddressText = string.Format(Loc["LobbyAddress"], address.Host);
        }
        CodeText = host is null ? string.Empty : LobbyAddress.FormatCode(host.Code);
        session.Changed += Refresh;
        session.Started += OnStarted;
        session.Ended += OnEnded;
        CloseRequested += _ =>
        {
            session.Changed -= Refresh;
            session.Started -= OnStarted;
            session.Ended -= OnEnded;
        };
        Refresh();
    }

    protected override bool CancelResult => false;

    public bool IsHost { get; }
    public string Title => Loc["LobbyTitle"];
    public string Subtitle => Loc[IsHost ? "LobbySubtitleHost" : "LobbySubtitleGuest"];
    public string QrText { get; } = string.Empty;
    public bool HasQr => QrText.Length > 0;
    public string CodeText { get; }
    public string AddressText { get; } = string.Empty;
    public string PrimaryText => Loc[IsHost ? "LobbyStart" : "LobbyWaitingHost"];
    public string SecondaryText => Loc[IsHost ? "LobbyCloseGame" : "LobbyLeave"];

    public ObservableCollection<LobbyPlayerRow> Players { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool canStart;

    [ObservableProperty]
    private string playersHeader = string.Empty;

    private void Refresh()
    {
        var state = _session.State;
        Players.Clear();
        foreach (var player in state.Players)
            Players.Add(new LobbyPlayerRow(
                player.Id == _session.Connection?.PlayerId ? string.Format(Loc["LobbyYou"], player.Name) : player.Name,
                player.Warband is { } w ? string.Format(Loc["LobbyPlayerWarband"], w.Name, w.ArchetypeName, w.Rating) : Loc["LobbyChoosingWarband"],
                player.Warband is null,
                player.IsHost ? Resources.Icons.SolidFont.Crown : Resources.Icons.SolidFont.User));
        PlayersHeader = string.Format(Loc["LobbyPlayers"], state.Players.Count, LobbyHost.MaxPlayers);
        CanStart = IsHost && state.Players.Count >= 2 && state.Players.All(p => p.Warband is not null);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        await _session.StartGameAsync();
    }

    private void OnStarted() => Close(true);

    private async void OnEnded(LobbyCloseReason reason)
    {
        await ShowInfoAsync(Title, Loc[LobbyTexts.CloseReasonKey(reason)]);
        Close(false);
    }
}

/// <summary>Clés de traduction des raisons de fermeture ou de refus.</summary>
public static class LobbyTexts
{
    public static string CloseReasonKey(LobbyCloseReason reason) => reason switch
    {
        LobbyCloseReason.WrongCode => "LobbyWrongCode",
        LobbyCloseReason.Full => "LobbyFull",
        LobbyCloseReason.AlreadyStarted => "LobbyAlreadyStarted",
        LobbyCloseReason.HostClosed => "LobbyHostClosed",
        _ => "LobbyConnectionLost",
    };
}
