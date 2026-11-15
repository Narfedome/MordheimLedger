using System.Net;
using System.Net.Sockets;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Annonce d'une partie ouverte, diffusée en UDP sur le réseau local toutes les secondes par l'hôte.</summary>
public sealed record LobbyAnnouncement
{
    public const string AppName = "mordheim-ledger";

    public string App { get; init; } = AppName;
    public int ProtocolVersion { get; init; } = LobbyProtocol.Version;
    public string HostName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int Port { get; init; }
    public int PlayerCount { get; init; }
    public bool Started { get; init; }

    /// <summary>Adresse d'où vient l'annonce (renseignée à la réception, pas transmise).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Address { get; init; } = string.Empty;

    public LobbyAddress ToAddress() => new(Address, Port, Code);
}

/// <summary>Découverte des parties ouvertes sur le réseau local (« Parties à proximité », et le code saisi sous Windows
/// qui retrouve son hôte) : l'hôte diffuse une annonce par seconde, les invités écoutent. Certains réseaux isolent les
/// appareils les uns des autres (Wi-Fi invité) : la découverte échoue alors, le QR (qui porte l'adresse) reste possible.</summary>
public static class LobbyDiscovery
{
    /// <summary>Diffuse l'annonce de la partie jusqu'à l'annulation du jeton.</summary>
    public static async Task AnnounceAsync(Func<LobbyAnnouncement> announcement, CancellationToken cancellationToken, int port = LobbyProtocol.DiscoveryPort)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytes = LobbyProtocol.SerializeAnnouncement(announcement());
            var targets = LobbyAddress.LocalNetworks().Select(n => n.Broadcast).Append(IPAddress.Broadcast).Distinct();
            foreach (var target in targets)
            {
                try
                {
                    await udp.SendAsync(bytes, new IPEndPoint(target, port), cancellationToken);
                }
                catch (SocketException)
                {
                    // Réseau qui refuse la diffusion : on réessaie à la seconde suivante.
                }
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Écoute les annonces jusqu'à l'annulation ; chaque annonce reçue (même répétée) est passée à onFound.</summary>
    public static async Task ListenAsync(Action<LobbyAnnouncement> onFound, CancellationToken cancellationToken, int port = LobbyProtocol.DiscoveryPort)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                continue;
            }
            if (LobbyProtocol.DeserializeAnnouncement(received.Buffer) is { ProtocolVersion: LobbyProtocol.Version } announcement)
                onFound(announcement with { Address = received.RemoteEndPoint.Address.ToString() });
        }
    }
}
