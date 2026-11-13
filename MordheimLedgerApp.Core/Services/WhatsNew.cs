using System.Net;
using System.Text.RegularExpressions;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Nouveautés affichées après une mise à jour. Le texte vient du changelog du site
/// (<c>Website/changelog.html</c> et <c>Website/en/changelog.html</c>, embarqués dans l'appli) : la section
/// <c>&lt;h2&gt;majeure.mineure&lt;/h2&gt;</c>, la même que <c>Deploy/PlayPublisher.cs</c> reprend pour les notes
/// Google Play - un seul texte à écrire par version.</summary>
public static class WhatsNew
{
    /// <summary>« 1.5 » pour « 1.5.392 » ou « 1.5.392.0 » ; null si la version n'a pas deux nombres.</summary>
    public static string? MajorMinor(string? version)
    {
        var match = Regex.Match(version ?? string.Empty, @"^\s*(\d+)\.(\d+)");
        return match.Success ? $"{match.Groups[1].Value}.{match.Groups[2].Value}" : null;
    }

    /// <summary>Les puces de la section <paramref name="majorMinor"/>, en texte brut (balises retirées, entités
    /// décodées, espaces resserrés). Liste vide si la section n'existe pas ou n'a aucune puce.</summary>
    public static IReadOnlyList<string> ExtractItems(string? changelogHtml, string majorMinor)
    {
        if (string.IsNullOrEmpty(changelogHtml)) return [];
        var section = Regex.Match(changelogHtml,
            $@"<h2>\s*{Regex.Escape(majorMinor)}\s*</h2>(?<body>.*?)(?=<h2>|</main>|$)",
            RegexOptions.Singleline);
        if (!section.Success) return [];

        return Regex.Matches(section.Groups["body"].Value, @"<li>(?<item>.*?)</li>", RegexOptions.Singleline)
            .Select(m => WebUtility.HtmlDecode(Regex.Replace(m.Groups["item"].Value, "<[^>]+>", "")))
            .Select(item => Regex.Replace(item, @"\s+", " ").Trim())
            .Where(item => item.Length > 0)
            .ToList();
    }

    /// <summary>Le dialog s'affiche quand la version installée (majeure.mineure) n'est pas la dernière vue et
    /// que le changelog a quelque chose à en dire. <paramref name="lastSeen"/> null = l'appli a été installée
    /// avant ce dialog (une mise à jour, donc à montrer) ; à la première installation, l'accueil enregistre la
    /// version comme vue avant que cette règle ne soit consultée.</summary>
    public static bool ShouldShow(string? lastSeen, string? current, int itemCount) =>
        current != null && itemCount > 0 && !string.Equals(lastSeen, current, StringComparison.Ordinal);
}
