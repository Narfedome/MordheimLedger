using System.Net.Sockets;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Refus de l'hôte à la connexion (mauvais code, partie pleine ou déjà commencée), ou hôte injoignable.</summary>
public sealed class LobbyJoinException(LobbyCloseReason reason, Exception? inner = null) : Exception(reason.ToString(), inner)
{
    public LobbyCloseReason Reason { get; } = reason;
}

/// <summary>Invité d'une partie connectée (v2, lot 4a), voir LobbyHost.</summary>
public sealed class LobbyClient : ILobbyConnection
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private bool _leaving;

    public string PlayerId { get; }
    public bool IsHost => false;
    public LobbySnapshot State { get; private set; } = LobbySnapshot.Empty;

    public event Action<LobbySnapshot>? StateChanged;
    public event Action<LobbyCloseReason>? Closed;
    public event Action<LobbyEvent>? EventReceived;

    public Task SendEventAsync(string? to, string kind, string payload) =>
        SendAsync(new LobbyMessage { Type = "relay", Event = new LobbyEvent(PlayerId, to, kind, payload) });

    private LobbyClient(TcpClient client, string playerId)
    {
        _client = client;
        _stream = client.GetStream();
        PlayerId = playerId;
    }

    public static async Task<LobbyClient> JoinAsync(LobbyAddress address, string playerName, CancellationToken cancellationToken = default)
    {
        var client = new TcpClient(AddressFamily.InterNetwork);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            await client.ConnectAsync(address.Host, address.Port, timeout.Token);
            var stream = client.GetStream();
            await LobbyProtocol.WriteAsync(stream, new LobbyMessage { Type = "hello", Token = address.Code, Name = playerName }, timeout.Token);
            var reply = await LobbyProtocol.ReadAsync(stream, timeout.Token);
            if (reply is { Type: "welcome", PlayerId: { } playerId })
            {
                var lobby = new LobbyClient(client, playerId);
                _ = lobby.ReadLoopAsync();
                return lobby;
            }
            throw new LobbyJoinException(reply?.Reason ?? LobbyCloseReason.ConnectionLost);
        }
        catch (LobbyJoinException)
        {
            client.Dispose();
            throw;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or InvalidDataException)
        {
            client.Dispose();
            if (cancellationToken.IsCancellationRequested) throw;
            throw new LobbyJoinException(LobbyCloseReason.ConnectionLost, ex);
        }
    }

    public Task SetWarbandAsync(string packageJson, LobbyWarbandSummary summary) =>
        SendAsync(new LobbyMessage { Type = "warband", Package = packageJson, Summary = summary });

    public async Task LeaveAsync()
    {
        if (_leaving) return;
        _leaving = true;
        await SendAsync(new LobbyMessage { Type = "leave" });
        _cts.Cancel();
        _client.Dispose();
    }

    public async ValueTask DisposeAsync() => await LeaveAsync();

    private async Task ReadLoopAsync()
    {
        var reason = LobbyCloseReason.ConnectionLost;
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var message = await LobbyProtocol.ReadAsync(_stream, _cts.Token);
                if (message is null) break;
                if (message is { Type: "state", Snapshot: { } snapshot })
                {
                    State = snapshot;
                    StateChanged?.Invoke(snapshot);
                }
                else if (message is { Type: "relay", Event: { } relayed })
                {
                    EventReceived?.Invoke(relayed);
                }
                else if (message.Type == "closed")
                {
                    reason = message.Reason ?? LobbyCloseReason.HostClosed;
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Connexion coupée : signalée ci-dessous.
        }
        if (_leaving) return;
        _leaving = true;
        _client.Dispose();
        Closed?.Invoke(reason);
    }

    private async Task SendAsync(LobbyMessage message)
    {
        await _writeLock.WaitAsync();
        try
        {
            await LobbyProtocol.WriteAsync(_stream, message, CancellationToken.None);
        }
        catch (Exception) when (_leaving || message.Type == "leave")
        {
            // Déjà déconnecté : rien à prévenir.
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
