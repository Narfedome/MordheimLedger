using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Exchange;
using MordheimLedgerApp.Core.Lobby;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Services;

/// <summary>Partie connectée en cours sur cet appareil (v2, lot 4a - maquette « Partie connectée », variantes 1A et 3A) :
/// connexion d'hôte ou d'invité (Core.Lobby), bande que ce joueur y engage, et bandes adverses lues à la demande dans des
/// bases temporaires (OpponentRosterLoader), effacées à la fin. Une seule partie à la fois. Les événements arrivent sur le
/// thread UI.</summary>
public sealed class LobbySession
{
    private const string PlayerNameKey = "lobby_player_name";

    private readonly ExchangeService _exchange;
    private readonly IWarbandService _warbands;
    private readonly Dictionary<string, (string Package, OpponentRoster Roster)> _opponents = new(StringComparer.Ordinal);
    private bool _started;

    public LobbySession(ExchangeService exchange, IWarbandService warbands)
    {
        _exchange = exchange;
        _warbands = warbands;
    }

    public ILobbyConnection? Connection { get; private set; }

    /// <summary>Bande engagée par ce joueur.</summary>
    public int? WarbandId { get; private set; }

    public bool IsActive => Connection is not null;
    public LobbySnapshot State => Connection?.State ?? LobbySnapshot.Empty;
    public bool IsStarted => _started;

    /// <summary>Partie connectée commencée avec cette bande : l'onglet Adversaires de sa fiche apparaît.</summary>
    public bool IsPlayingWith(int warbandId) => IsActive && _started && WarbandId == warbandId;

    /// <summary>Joueurs de la partie (hôte compris) ayant engagé une bande, sauf ce joueur.</summary>
    public IReadOnlyList<LobbyPlayer> Opponents =>
        Connection is { } c ? c.State.Players.Where(p => p.Id != c.PlayerId && p.Warband is not null).ToList() : [];

    /// <summary>Invité : bande à lancer (« Lancer la partie ») quand sa fiche s'ouvre, une fois la partie commencée par
    /// l'hôte. Pris une seule fois (TakePendingStart).</summary>
    private int? _pendingStartWarbandId;

    public event Action? Changed;
    public event Action<LobbyCloseReason>? Ended;

    /// <summary>Partie commencée par l'hôte (levé chez l'hôte aussi).</summary>
    public event Action? Started;

    public static string PlayerName
    {
        get => Preferences.Default.Get(PlayerNameKey, DeviceInfo.Current.Name);
        set => Preferences.Default.Set(PlayerNameKey, value.Trim());
    }

    /// <summary>Crée une partie avec cette bande ; l'appareil en est l'hôte.</summary>
    public async Task<LobbyHost> HostAsync(int warbandId)
    {
        await LeaveAsync();
        var host = LobbyHost.Start(PlayerName);
        Attach(host, warbandId);
        await ShareWarbandAsync();
        return host;
    }

    /// <summary>Rejoint la partie à cette adresse avec cette bande. Lève LobbyJoinException si l'hôte refuse.</summary>
    public async Task JoinAsync(LobbyAddress address, int warbandId)
    {
        await LeaveAsync();
        var client = await LobbyClient.JoinAsync(address, PlayerName);
        Attach(client, warbandId);
        await ShareWarbandAsync();
    }

    public async Task StartGameAsync()
    {
        if (Connection is LobbyHost host) await host.StartGameAsync();
    }

    /// <summary>La partie de cette bande a réellement commencé sur cet appareil (« Avant la partie » validé) : sa fin
    /// (plus de partie en cours) termine la partie connectée. Avant ce moment, une fiche qui se recharge (fermeture de la
    /// salle d'attente) ne doit pas la prendre pour une fin de partie - recette du 2026-11-15.</summary>
    public bool LocalGameStarted { get; private set; }

    public void MarkLocalGameStarted(int warbandId)
    {
        if (IsPlayingWith(warbandId)) LocalGameStarted = true;
    }

    public bool TakePendingStart(int warbandId)
    {
        if (_pendingStartWarbandId != warbandId) return false;
        _pendingStartWarbandId = null;
        return true;
    }

    /// <summary>Quitte la partie (l'hôte la ferme pour tous) et efface les bandes adverses.</summary>
    public async Task LeaveAsync()
    {
        if (Connection is not { } connection) return;
        Detach();
        try
        {
            await connection.LeaveAsync();
        }
        catch (Exception ex)
        {
            CrashLogger.LogException("LobbySession.LeaveAsync", ex);
        }
        Changed?.Invoke();
        WeakReferenceMessenger.Default.Send(new LobbyChangedMessage());
    }

    /// <summary>Bande d'un adversaire, lue dans une base temporaire (gardée tant que son paquet ne change pas).</summary>
    public async Task<OpponentRoster?> LoadOpponentAsync(string playerId)
    {
        if (!State.Packages.TryGetValue(playerId, out var package)) return null;
        if (_opponents.TryGetValue(playerId, out var cached) && cached.Package == package) return cached.Roster;
        Directory.CreateDirectory(OpponentFolder);
        var path = Path.Combine(OpponentFolder, $"{playerId}.db3");
        var roster = await Task.Run(() => OpponentRosterLoader.LoadAsync(path, OfficialContentService.OpenEmbeddedSeedAsync,
            ExchangeArchive.FromJson(package), LocalizationService.Instance.Language));
        _opponents[playerId] = (package, roster);
        return roster;
    }

    private static string OpponentFolder => Path.Combine(FileSystem.CacheDirectory, "lobby");

    private async Task ShareWarbandAsync()
    {
        if (Connection is not { } connection || WarbandId is not { } warbandId) return;
        var warband = await _warbands.GetWarbandAsync(warbandId) ?? throw new InvalidOperationException($"Bande {warbandId} introuvable");
        // Photos laissées de côté : la bande adverse ne s'affiche qu'en lecture, le temps de la partie.
        var package = await _exchange.ExportWarbandsAsync(new[] { warbandId }, new ExchangeExportOptions(IncludePhotos: false));
        var warriors = await _warbands.GetWarriorsAsync(warbandId, LocalizationService.Instance.Language);
        var summary = new LobbyWarbandSummary(warband.Name,
            await _warbands.GetWarbandArchetypeNameAsync(warband.WarbandArchetypeId, LocalizationService.Instance.Language),
            warriors.Where(w => w.Status is not (Core.Models.WarriorStatus.Dead or Core.Models.WarriorStatus.Retired)).Sum(w => w.HeadCount),
            await _warbands.GetWarbandRatingAsync(warbandId));
        await connection.SetWarbandAsync(ExchangeArchive.ToJson(package), summary);
    }

    private void Attach(ILobbyConnection connection, int warbandId)
    {
        Connection = connection;
        WarbandId = warbandId;
        _started = false;
        connection.StateChanged += OnStateChanged;
        connection.Closed += OnClosed;
    }

    private void Detach()
    {
        if (Connection is { } connection)
        {
            connection.StateChanged -= OnStateChanged;
            connection.Closed -= OnClosed;
        }
        Connection = null;
        WarbandId = null;
        _started = false;
        LocalGameStarted = false;
        _pendingStartWarbandId = null;
        _opponents.Clear();
        try
        {
            if (Directory.Exists(OpponentFolder)) Directory.Delete(OpponentFolder, recursive: true);
        }
        catch (IOException)
        {
            // Une base encore ouverte : le cache sera vidé au prochain départ.
        }
    }

    private void OnStateChanged(LobbySnapshot snapshot) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Connection is null) return;
        if (snapshot.Started && !_started)
        {
            _started = true;
            if (!Connection.IsHost) _pendingStartWarbandId = WarbandId;
            Started?.Invoke();
        }
        Changed?.Invoke();
        WeakReferenceMessenger.Default.Send(new LobbyChangedMessage());
    });

    private void OnClosed(LobbyCloseReason reason) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (Connection is null) return;
        Detach();
        // Fin annoncée avant le rafraîchissement : la fiche sait encore qu'elle jouait cette partie.
        Ended?.Invoke(reason);
        WeakReferenceMessenger.Default.Send(new LobbyEndedMessage(reason));
        Changed?.Invoke();
        WeakReferenceMessenger.Default.Send(new LobbyChangedMessage());
    });
}

/// <summary>La partie connectée a changé (joueurs, lancement, fin) - pour les écrans qui ne s'abonnent pas à LobbySession
/// (référence faible : une fiche fermée ne reste pas en mémoire).</summary>
public sealed record LobbyChangedMessage;

/// <summary>La partie connectée s'est terminée sans que ce joueur la quitte (hôte parti, connexion perdue).</summary>
public sealed record LobbyEndedMessage(Core.Lobby.LobbyCloseReason Reason);
