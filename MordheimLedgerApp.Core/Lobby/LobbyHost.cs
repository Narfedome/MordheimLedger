using System.Net;
using System.Net.Sockets;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Ce que l'appli voit d'une partie connectée, qu'elle en soit l'hôte ou un invité.</summary>
public interface ILobbyConnection : IAsyncDisposable
{
    string PlayerId { get; }
    bool IsHost { get; }
    LobbySnapshot State { get; }

    /// <summary>Nouvel état diffusé par l'hôte (sur un thread du réseau).</summary>
    event Action<LobbySnapshot>? StateChanged;

    /// <summary>Partie terminée pour cet appareil : fermée par l'hôte, connexion perdue (sur un thread du réseau). Pas
    /// levé quand l'appareil la quitte lui-même.</summary>
    event Action<LobbyCloseReason>? Closed;

    /// <summary>Bande choisie par ce joueur : paquet d'échange (ExchangeArchive.ToJson) et résumé pour la salle d'attente.</summary>
    Task SetWarbandAsync(string packageJson, LobbyWarbandSummary summary);

    /// <summary>Quitter la partie (l'hôte la ferme pour tous).</summary>
    Task LeaveAsync();

    /// <summary>Envoie un message à un joueur (to) ou à tous les autres (to null). From est rempli avec PlayerId.</summary>
    Task SendEventAsync(string? to, string kind, string payload);

    /// <summary>Message reçu d'un autre joueur (sur un thread du réseau).</summary>
    event Action<LobbyEvent>? EventReceived;
}

/// <summary>Hôte d'une partie connectée (v2, lot 4a) : écoute en TCP sur le réseau local, accepte jusqu'à 3 invités qui
/// présentent le bon code, diffuse l'état complet (joueurs, bandes, partie commencée) à chaque changement, et annonce la
/// partie en UDP (LobbyDiscovery). Sans serveur : l'hôte est l'un des appareils de la table.</summary>
public sealed class LobbyHost : ILobbyConnection
{
    public const int MaxPlayers = 4;
    private const string HostPlayerId = "host";

    private sealed class Guest(TcpClient client)
    {
        public TcpClient Client { get; } = client;
        public NetworkStream Stream { get; } = client.GetStream();
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public string Id { get; set; } = string.Empty;
    }

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _gate = new();
    private readonly List<Guest> _guests = [];
    private readonly List<LobbyPlayer> _players = [];
    private readonly Dictionary<string, string> _packages = new(StringComparer.Ordinal);
    private bool _started;
    private bool _closed;

    public string Code { get; } = LobbyAddress.NewCode();
    public int Port { get; }
    public string PlayerId => HostPlayerId;
    public bool IsHost => true;
    public LobbySnapshot State { get; private set; } = LobbySnapshot.Empty;

    public event Action<LobbySnapshot>? StateChanged;
    public event Action<LobbyEvent>? EventReceived;

    /// <summary>Jamais levé côté hôte : c'est lui qui ferme.</summary>
    public event Action<LobbyCloseReason>? Closed { add { } remove { } }

    private LobbyHost(string hostName, int port, bool announce)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start(MaxPlayers);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _players.Add(new LobbyPlayer(HostPlayerId, hostName, IsHost: true, Warband: null));
        State = BuildSnapshot();
        _ = AcceptLoopAsync();
        if (announce)
            _ = LobbyDiscovery.AnnounceAsync(() => new LobbyAnnouncement
            {
                HostName = hostName,
                Code = Code,
                Port = Port,
                PlayerCount = State.Players.Count,
                Started = State.Started,
            }, _cts.Token);
    }

    /// <param name="port">0 = un port libre choisi par le système (il voyage dans le QR et l'annonce).</param>
    /// <param name="announce">Faux en test : pas de diffusion UDP.</param>
    public static LobbyHost Start(string hostName, int port = 0, bool announce = true) => new(hostName, port, announce);

    /// <summary>Adresse à mettre dans le QR, sur le premier réseau local de l'appareil (null si aucun).</summary>
    public LobbyAddress? AddressOnLocalNetwork() =>
        LobbyAddress.LocalNetworks().FirstOrDefault() is { Address: { } address } ? new LobbyAddress(address.ToString(), Port, Code) : null;

    public Task SetWarbandAsync(string packageJson, LobbyWarbandSummary summary) => SetWarbandAsync(HostPlayerId, packageJson, summary);

    /// <summary>Lancement de la partie : plus personne ne peut rejoindre.</summary>
    public Task StartGameAsync()
    {
        lock (_gate) _started = true;
        return BroadcastAsync();
    }

    public async Task LeaveAsync()
    {
        List<Guest> guests;
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            guests = [.. _guests];
        }
        foreach (var guest in guests)
            await SendAsync(guest, new LobbyMessage { Type = "closed", Reason = LobbyCloseReason.HostClosed });
        _cts.Cancel();
        _listener.Stop();
        foreach (var guest in guests) guest.Client.Dispose();
    }

    public async ValueTask DisposeAsync() => await LeaveAsync();

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception) when (_cts.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }
            _ = HandleGuestAsync(new Guest(client));
        }
    }

    private async Task HandleGuestAsync(Guest guest)
    {
        try
        {
            using var helloTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            helloTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var hello = await LobbyProtocol.ReadAsync(guest.Stream, helloTimeout.Token);
            if (hello is not { Type: "hello" })
            {
                guest.Client.Dispose();
                return;
            }

            LobbyCloseReason? refusal;
            lock (_gate)
            {
                refusal = hello.ProtocolVersion != LobbyProtocol.Version || hello.Token != Code ? LobbyCloseReason.WrongCode
                    : _started ? LobbyCloseReason.AlreadyStarted
                    : _players.Count >= MaxPlayers ? LobbyCloseReason.Full
                    : null;
                if (refusal is null)
                {
                    guest.Id = Guid.NewGuid().ToString("N")[..8];
                    _guests.Add(guest);
                    _players.Add(new LobbyPlayer(guest.Id, string.IsNullOrWhiteSpace(hello.Name) ? "?" : hello.Name.Trim(), IsHost: false, Warband: null));
                }
            }
            if (refusal is { } reason)
            {
                await SendAsync(guest, new LobbyMessage { Type = "reject", Reason = reason });
                guest.Client.Dispose();
                return;
            }

            await SendAsync(guest, new LobbyMessage { Type = "welcome", PlayerId = guest.Id });
            await BroadcastAsync();

            while (!_cts.IsCancellationRequested)
            {
                var message = await LobbyProtocol.ReadAsync(guest.Stream, _cts.Token);
                if (message is null || message.Type == "leave") break;
                if (message is { Type: "warband", Package: { } package, Summary: { } summary })
                    await SetWarbandAsync(guest.Id, package, summary);
                else if (message is { Type: "relay", Event: { } relayed })
                    await RouteAsync(relayed with { From = guest.Id });
            }
        }
        catch (Exception) when (_cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            // Connexion coupée (Wi-Fi perdu, appli fermée) : le joueur sort de la partie comme s'il l'avait quittée.
        }
        await RemoveGuestAsync(guest);
    }

    private async Task RemoveGuestAsync(Guest guest)
    {
        lock (_gate)
        {
            if (_closed || !_guests.Remove(guest)) return;
            _players.RemoveAll(p => p.Id == guest.Id);
            _packages.Remove(guest.Id);
        }
        guest.Client.Dispose();
        await BroadcastAsync();
    }

    private Task SetWarbandAsync(string playerId, string packageJson, LobbyWarbandSummary summary)
    {
        lock (_gate)
        {
            var index = _players.FindIndex(p => p.Id == playerId);
            if (index < 0) return Task.CompletedTask;
            _players[index] = _players[index] with { Warband = summary };
            _packages[playerId] = packageJson;
        }
        return BroadcastAsync();
    }

    public Task SendEventAsync(string? to, string kind, string payload) => RouteAsync(new LobbyEvent(HostPlayerId, to, kind, payload));

    /// <summary>Remet un message à son destinataire (ou à tous sauf l'expéditeur) : l'hôte le lève chez lui, l'envoie aux
    /// invités concernés.</summary>
    private async Task RouteAsync(LobbyEvent message)
    {
        List<Guest> targets;
        lock (_gate)
        {
            if (_closed) return;
            targets = _guests.Where(g => g.Id != message.From && (message.To is null || message.To == g.Id)).ToList();
        }
        if (message.From != HostPlayerId && (message.To is null || message.To == HostPlayerId))
            EventReceived?.Invoke(message);
        foreach (var guest in targets)
            await SendAsync(guest, new LobbyMessage { Type = "relay", Event = message });
    }

    private LobbySnapshot BuildSnapshot() => new([.. _players], _started, new Dictionary<string, string>(_packages, StringComparer.Ordinal));

    private async Task BroadcastAsync()
    {
        LobbySnapshot snapshot;
        List<Guest> guests;
        lock (_gate)
        {
            if (_closed) return;
            snapshot = State = BuildSnapshot();
            guests = [.. _guests];
        }
        StateChanged?.Invoke(snapshot);
        foreach (var guest in guests)
            await SendAsync(guest, new LobbyMessage { Type = "state", Snapshot = snapshot });
    }

    private async Task SendAsync(Guest guest, LobbyMessage message)
    {
        await guest.WriteLock.WaitAsync();
        try
        {
            await LobbyProtocol.WriteAsync(guest.Stream, message, CancellationToken.None);
        }
        catch (Exception)
        {
            // Invité injoignable : sa boucle de lecture le retirera.
        }
        finally
        {
            guest.WriteLock.Release();
        }
    }
}
