using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Où joindre une partie : adresse de l'hôte sur le réseau local, port, et code de la partie (exigé à la
/// connexion, il empêche un appareil voisin d'entrer par hasard). Le QR de la salle d'attente porte les trois
/// (« MLL1:192.168.1.23:50123:K7Q482 ») ; le code seul, à saisir sous Windows, retrouve l'hôte par l'annonce UDP
/// (LobbyDiscovery).</summary>
public sealed record LobbyAddress(string Host, int Port, string Code)
{
    private const string QrPrefix = "MLL1:";

    /// <summary>Lettres et chiffres sans ambiguïté à la lecture (pas de 0/O, 1/I/L).</summary>
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public string ToQrText() => $"{QrPrefix}{Host}:{Port}:{Code}";

    public static bool TryParseQr(string text, out LobbyAddress? address)
    {
        address = null;
        text = text.Trim();
        if (!text.StartsWith(QrPrefix, StringComparison.Ordinal)) return false;
        var parts = text[QrPrefix.Length..].Split(':');
        if (parts.Length != 3 || !IPAddress.TryParse(parts[0], out _) || !int.TryParse(parts[1], out var port)
            || port is <= 0 or > 65535 || NormalizeCode(parts[2]) is not { } code)
            return false;
        address = new LobbyAddress(parts[0], port, code);
        return true;
    }

    public static string NewCode() => string.Concat(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));

    /// <summary>Code tel qu'affiché (« K7Q-482 »).</summary>
    public static string FormatCode(string code) => code.Length == 6 ? $"{code[..3]}-{code[3..]}" : code;

    /// <summary>Code saisi par le joueur (espaces, tiret, minuscules tolérés), null s'il n'a pas la bonne forme.</summary>
    public static string? NormalizeCode(string? input)
    {
        var code = new string((input ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return code.Length == 6 && code.All(CodeAlphabet.Contains) ? code : null;
    }

    /// <summary>Adresses IPv4 privées de cet appareil (Wi-Fi, point d'accès, Ethernet), avec leur adresse de diffusion.
    /// Vide en 4G seule : pas de réseau local, pas de partie connectée.</summary>
    public static IReadOnlyList<(IPAddress Address, IPAddress Broadcast)> LocalNetworks()
    {
        var result = new List<(IPAddress, IPAddress)>();
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return result;
        }
        foreach (var networkInterface in interfaces)
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }
            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || !IsPrivate(unicast.Address)) continue;
                var mask = unicast.IPv4Mask is { } m && !m.Equals(IPAddress.Any) ? m : IPAddress.Parse("255.255.255.0");
                var address = unicast.Address.GetAddressBytes();
                var maskBytes = mask.GetAddressBytes();
                var broadcast = new byte[4];
                for (var i = 0; i < 4; i++) broadcast[i] = (byte)(address[i] | ~maskBytes[i]);
                result.Add((unicast.Address, new IPAddress(broadcast)));
            }
        }
        return result;
    }

    /// <summary>Réseaux privés (RFC 1918) : un réseau local de maison ou un point d'accès de téléphone. Exclut l'adresse
    /// 4G (opérateur) et les adresses autoconfigurées (169.254.x.x).</summary>
    internal static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
