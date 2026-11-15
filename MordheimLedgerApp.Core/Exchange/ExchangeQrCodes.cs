using System.Security.Cryptography;

namespace MordheimLedgerApp.Core.Exchange;

/// <summary>Paquet trop gros pour des QR codes, même découpé (au-delà de ExchangeQrCodes.MaxCodes) : à partager par
/// fichier.</summary>
public sealed class ExchangeTooLargeForQrException(int codesNeeded) : Exception($"{codesNeeded} QR codes nécessaires")
{
    public int CodesNeeded { get; } = codesNeeded;
}

/// <summary>QR codes d'un paquet (v2, lot 3). Le texte compact (ExchangeArchive.ToCompactText, « MLX1:… ») tient dans un
/// seul QR jusqu'à CharactersPerCode caractères ; au-delà il est découpé en plusieurs QR à scanner l'un après l'autre,
/// dans n'importe quel ordre (choix de l'utilisateur : « 1/3, 2/3, 3/3 ») :
/// « MLQ1:&lt;lot&gt;:&lt;n°&gt;/&lt;total&gt;:&lt;morceau&gt; ». Le lot (6 caractères aléatoires) évite de mélanger deux envois.</summary>
public static class ExchangeQrCodes
{
    /// <summary>Taille d'un QR : bien en dessous du maximum théorique (~2 900 caractères), un QR aussi dense se lit mal
    /// sur l'écran d'un autre téléphone.</summary>
    public const int CharactersPerCode = 1200;

    /// <summary>Au-delà, le fichier est plus simple que de scanner des QR à la chaîne.</summary>
    public const int MaxCodes = 10;

    private const string ChunkPrefix = "MLQ1:";

    public static IReadOnlyList<string> Encode(ExchangePackage package, int charactersPerCode = CharactersPerCode)
    {
        var compact = ExchangeArchive.ToCompactText(package);
        if (compact.Length <= charactersPerCode) return [compact];

        var data = compact[ExchangeArchive.CompactPrefix.Length..];
        var set = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
        // En-tête au plus « MLQ1:xxxxxx:10/10: » (18 caractères).
        var size = charactersPerCode - 20;
        var count = (data.Length + size - 1) / size;
        if (count > MaxCodes) throw new ExchangeTooLargeForQrException(count);
        return Enumerable.Range(0, count)
            .Select(i => $"{ChunkPrefix}{set}:{i + 1}/{count}:{data.Substring(i * size, Math.Min(size, data.Length - i * size))}")
            .ToList();
    }

    /// <summary>Assemble les QR scannés un par un. Un code d'un autre envoi recommence l'assemblage.</summary>
    public sealed class Collector
    {
        private string? _set;
        private string?[] _parts = [];

        /// <summary>QR déjà lus de l'envoi en cours (0 tant que rien n'est lu).</summary>
        public int Received => _parts.Count(p => p is not null);

        /// <summary>Nombre de QR de l'envoi en cours (0 tant que rien n'est lu).</summary>
        public int Total => _parts.Length;

        /// <summary>Ajoute un QR lu. Renvoie le paquet complet quand le dernier morceau arrive, sinon null. Lève
        /// ExchangeFormatException pour un QR qui n'est pas de l'appli (ou endommagé).</summary>
        public ExchangePackage? Add(string text)
        {
            text = text.Trim();
            if (text.StartsWith(ExchangeArchive.CompactPrefix, StringComparison.Ordinal))
            {
                Reset();
                return ExchangeArchive.FromCompactText(text);
            }
            if (!text.StartsWith(ChunkPrefix, StringComparison.Ordinal))
                throw new ExchangeFormatException("Ce n'est pas un code Mordheim Ledger.");

            var fields = text[ChunkPrefix.Length..].Split(':', 3);
            var position = fields.Length == 3 ? fields[1].Split('/') : [];
            if (position.Length != 2 || !int.TryParse(position[0], out var index) || !int.TryParse(position[1], out var count)
                || count < 1 || count > MaxCodes || index < 1 || index > count)
                throw new ExchangeFormatException("Code Mordheim Ledger endommagé.");

            if (_set != fields[0] || _parts.Length != count)
            {
                _set = fields[0];
                _parts = new string?[count];
            }
            _parts[index - 1] = fields[2];
            if (_parts.Any(p => p is null)) return null;

            var package = ExchangeArchive.FromCompactText(ExchangeArchive.CompactPrefix + string.Concat(_parts));
            Reset();
            return package;
        }

        public void Reset()
        {
            _set = null;
            _parts = [];
        }
    }
}
