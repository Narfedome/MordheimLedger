namespace MordheimLedgerApp.Core.Rules;

/// <summary>Assemblage d'une entrée d'Historique à partir de ses phrases. Certaines phrases se terminent par une
/// valeur insérée (« Hans a progressé : CT +1 ») qui n'a pas de point : jointes telles quelles, elles se
/// collaient à la phrase suivante (recette du 2026-11-11).</summary>
public static class HistoryText
{
    private static readonly char[] SentenceEnds = ['.', '!', '?', '…', '»', ')'];

    /// <summary>Phrases non vides, chacune terminée par une ponctuation (un point ajouté si besoin), séparées
    /// par une espace.</summary>
    public static string Join(IEnumerable<string> sentences) =>
        string.Join(" ", sentences
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Select(s => SentenceEnds.Contains(s[^1]) ? s : s + "."));
}
