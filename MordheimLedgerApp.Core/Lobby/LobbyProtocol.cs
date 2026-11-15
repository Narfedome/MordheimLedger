using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Résumé d'une bande dans la salle d'attente (le paquet complet voyage à part, voir LobbySnapshot.Packages).</summary>
public sealed record LobbyWarbandSummary(string Name, string ArchetypeName, int WarriorCount, int Rating);

/// <summary>Un joueur de la partie connectée. Warband null tant qu'il n'a pas choisi sa bande.</summary>
public sealed record LobbyPlayer(string Id, string Name, bool IsHost, LobbyWarbandSummary? Warband);

/// <summary>État de la partie, tel que l'hôte le diffuse à chaque changement.</summary>
/// <param name="Packages">Paquet d'échange (ExchangeArchive.ToJson) de la bande de chaque joueur, par Id de joueur.</param>
public sealed record LobbySnapshot(IReadOnlyList<LobbyPlayer> Players, bool Started, IReadOnlyDictionary<string, string> Packages)
{
    public static LobbySnapshot Empty { get; } = new([], false, new Dictionary<string, string>());
}

/// <summary>Raisons de refus ou de fermeture, traduites par l'appli.</summary>
public enum LobbyCloseReason
{
    HostClosed,
    WrongCode,
    Full,
    AlreadyStarted,
    ConnectionLost,
}

/// <summary>Message échangé sur la connexion TCP (JSON précédé de sa longueur sur 4 octets). Un seul type, les champs
/// utiles selon Type :
/// invité → hôte : hello (Token, Name), warband (Package, Summary), leave ;
/// hôte → invité : welcome (PlayerId), reject (Reason), state (Snapshot), closed (Reason).</summary>
internal sealed class LobbyMessage
{
    public const int MaxBytes = 16 * 1024 * 1024;

    public string Type { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; } = LobbyProtocol.Version;
    public string? Token { get; set; }
    public string? Name { get; set; }
    public string? PlayerId { get; set; }
    public LobbyCloseReason? Reason { get; set; }
    public string? Package { get; set; }
    public LobbyWarbandSummary? Summary { get; set; }
    public LobbySnapshot? Snapshot { get; set; }
}

internal static class LobbyProtocol
{
    /// <summary>Version du protocole : un appareil d'une autre version est refusé à la connexion (message clair plutôt
    /// qu'un échange incompréhensible).</summary>
    public const int Version = 1;

    /// <summary>Port de l'annonce UDP des parties ouvertes (LobbyDiscovery).</summary>
    public const int DiscoveryPort = 47821;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task WriteAsync(Stream stream, LobbyMessage message, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, Options);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, body.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>Message suivant, ou null si la connexion est fermée proprement.</summary>
    public static async Task<LobbyMessage?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        if (!await ReadExactlyOrEndAsync(stream, header, cancellationToken)) return null;
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > LobbyMessage.MaxBytes) throw new InvalidDataException($"Message de {length} octets");
        var body = new byte[length];
        if (!await ReadExactlyOrEndAsync(stream, body, cancellationToken)) return null;
        return JsonSerializer.Deserialize<LobbyMessage>(body, Options);
    }

    private static async Task<bool> ReadExactlyOrEndAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    public static byte[] SerializeAnnouncement(LobbyAnnouncement announcement) => JsonSerializer.SerializeToUtf8Bytes(announcement, Options);

    public static LobbyAnnouncement? DeserializeAnnouncement(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var announcement = JsonSerializer.Deserialize<LobbyAnnouncement>(bytes, Options);
            return announcement?.App == LobbyAnnouncement.AppName ? announcement : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

}
