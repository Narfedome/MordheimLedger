using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Lobby;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Lobby;

/// <summary>Une partie ouverte trouvée sur le réseau local.</summary>
public sealed partial class DiscoveredGameRow : ObservableObject
{
    public DiscoveredGameRow(LobbyAnnouncement announcement) => Announcement = announcement;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Detail))]
    private LobbyAnnouncement announcement;

    [ObservableProperty]
    private bool isSelected;

    public string Title => string.Format(LocalizationService.Instance["LobbyGameOf"], Announcement.HostName);
    public string Detail => string.Format(LocalizationService.Instance["LobbyGamePlayers"], Announcement.PlayerCount, LobbyHost.MaxPlayers);
}

/// <summary>Une de ses bandes, engageable dans la partie.</summary>
public sealed partial class WarbandChoiceRow(Warband warband, string detail) : ObservableObject
{
    public Warband Warband { get; } = warband;
    public string Name => Warband.Name;
    public string Detail { get; } = detail;

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>Rejoindre une partie connectée (v2, lot 4a - maquette « Partie connectée ») : parties trouvées sur le réseau
/// local (annonces UDP), ou QR de l'hôte (Android), ou son code (Windows) ; choix de la bande engagée. Résultat : vrai si
/// l'appareil a rejoint la partie (la salle d'attente suit).</summary>
public partial class JoinLobbyViewModel : DialogViewModel<bool>
{
    private readonly LobbySession _session;
    private readonly CancellationTokenSource _discovery = new();

    public JoinLobbyViewModel(LobbySession session, IEnumerable<WarbandChoiceRow> warbands)
    {
        _session = session;
        playerName = LobbySession.PlayerName;
        Warbands = new ObservableCollection<WarbandChoiceRow>(warbands);
        if (Warbands.FirstOrDefault() is { } first) first.IsSelected = true;
        HasNetwork = LobbyAddress.LocalNetworks().Count > 0;
        CloseRequested += _ => _discovery.Cancel();
        if (HasNetwork) StartDiscovery();
    }

    protected override bool CancelResult => false;

    public string Title => Loc["LobbyJoinTitle"];
    public bool HasNetwork { get; }
    public bool CanScan => DeviceInfo.Current.Platform == DevicePlatform.Android;
    public ObservableCollection<WarbandChoiceRow> Warbands { get; }
    public bool HasWarbands => Warbands.Count > 0;
    public ObservableCollection<DiscoveredGameRow> Games { get; } = [];

    [ObservableProperty]
    private bool hasGames;

    [ObservableProperty]
    private string playerName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinSelectedCommand))]
    private bool isJoining;

    private WarbandChoiceRow? SelectedWarband => Warbands.FirstOrDefault(w => w.IsSelected);
    private DiscoveredGameRow? SelectedGame => Games.FirstOrDefault(g => g.IsSelected);

    private void StartDiscovery()
    {
        var multicast = NetworkLocks.AcquireMulticast();
        _ = Task.Run(async () =>
        {
            try
            {
                await LobbyDiscovery.ListenAsync(a => MainThread.BeginInvokeOnMainThread(() => OnFound(a)), _discovery.Token);
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("JoinLobbyViewModel.Discovery", ex);
            }
            finally
            {
                multicast.Dispose();
            }
        });
    }

    private void OnFound(LobbyAnnouncement announcement)
    {
        var existing = Games.FirstOrDefault(g => g.Announcement.Code == announcement.Code);
        if (announcement.Started)
        {
            if (existing is not null) Games.Remove(existing);
        }
        else if (existing is null)
        {
            Games.Add(new DiscoveredGameRow(announcement) { IsSelected = Games.Count == 0 });
        }
        else
        {
            existing.Announcement = announcement;
        }
        HasGames = Games.Count > 0;
        JoinSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectGame(DiscoveredGameRow row)
    {
        foreach (var game in Games) game.IsSelected = game == row;
        JoinSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectWarband(WarbandChoiceRow row)
    {
        foreach (var warband in Warbands) warband.IsSelected = warband == row;
    }

    private bool CanJoinSelected => !IsJoining && SelectedGame is not null && HasWarbands;

    [RelayCommand(CanExecute = nameof(CanJoinSelected))]
    private Task JoinSelected() => SelectedGame is { } game ? JoinAsync(game.Announcement.ToAddress()) : Task.CompletedTask;

    [RelayCommand]
    private async Task ScanQr()
    {
        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
        {
            await ShowInfoAsync(Loc["QrScanTitle"], Loc["QrScanNoCamera"]);
            return;
        }
        var result = await ShowFormAsync(new Exchange.QrScanForm(new Exchange.QrScanViewModel(lobbyMode: true)));
        if (result?.Lobby is { } address) await JoinAsync(address);
        else if (result is { PickFile: true }) await EnterCode();
    }

    [RelayCommand]
    private async Task EnterCode()
    {
        var input = await ShowPromptAsync(Loc["LobbyEnterCodeTitle"], Loc["LobbyEnterCodeMessage"], "K7Q-482");
        if (input is null) return;
        // Adresse complète (texte du QR collé) : connexion directe, sans passer par l'annonce sur le réseau - utile
        // quand le réseau bloque la diffusion (Wi-Fi invité) ou pour un appareil qui ne peut pas scanner.
        if (LobbyAddress.TryParseQr(input, out var direct) && direct is not null)
        {
            await JoinAsync(direct);
            return;
        }
        if (LobbyAddress.NormalizeCode(input) is not { } code)
        {
            await ShowInfoAsync(Title, Loc["LobbyCodeInvalid"]);
            return;
        }
        // Le code retrouve l'hôte par son annonce sur le réseau : on lui laisse quelques secondes pour arriver.
        for (var i = 0; i < 20 && Games.All(g => g.Announcement.Code != code); i++) await Task.Delay(250);
        if (Games.FirstOrDefault(g => g.Announcement.Code == code) is { } game) await JoinAsync(game.Announcement.ToAddress());
        else await ShowInfoAsync(Title, Loc["LobbyCodeNotFound"]);
    }

    private async Task JoinAsync(LobbyAddress address)
    {
        if (SelectedWarband is not { } warband || IsJoining) return;
        IsJoining = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(PlayerName)) LobbySession.PlayerName = PlayerName;
            await Loading.RunAsync(() => _session.JoinAsync(address, warband.Warband.Id));
            Close(true);
        }
        catch (LobbyJoinException ex)
        {
            await ShowInfoAsync(Title, Loc[LobbyTexts.CloseReasonKey(ex.Reason)]);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
        finally
        {
            IsJoining = false;
        }
    }
}
