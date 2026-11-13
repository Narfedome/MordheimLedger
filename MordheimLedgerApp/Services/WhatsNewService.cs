using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Services;

/// <summary>Nouveautés de la version installée (règles et extraction : <see cref="WhatsNew"/>). Le changelog du site
/// est embarqué tel quel (<c>changelog/fr.html</c>, <c>changelog/en.html</c>, voir le csproj) ; la dernière version
/// vue (majeure.mineure) est gardée dans les préférences.</summary>
public static class WhatsNewService
{
    private const string LastSeenKey = "whats_new_seen";

    /// <summary>Majeure.mineure de l'appli installée (« 1.5 »).</summary>
    public static string? CurrentVersion => WhatsNew.MajorMinor(AppInfo.Current.VersionString);

    public static bool IsEnglish => LocalizationService.Instance.Culture.TwoLetterISOLanguageName == "en";

    /// <summary>Page changelog du site, dans la langue de l'appli.</summary>
    public static Uri AllVersionsUri => new(IsEnglish
        ? "https://mordheimledger.com/en/changelog.html"
        : "https://mordheimledger.com/changelog.html");

    /// <summary>Puces de la version installée, dans la langue de l'appli (vide si le changelog n'en a pas).</summary>
    public static async Task<IReadOnlyList<string>> LoadCurrentItemsAsync()
    {
        if (CurrentVersion is not { } version) return [];
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(IsEnglish ? "changelog/en.html" : "changelog/fr.html");
            using var reader = new StreamReader(stream);
            return WhatsNew.ExtractItems(await reader.ReadToEndAsync(), version);
        }
        catch (Exception)
        {
            return []; // changelog absent du paquet : rien à annoncer plutôt qu'une erreur au lancement
        }
    }

    /// <summary>Puces à annoncer au lancement, ou vide si la version installée a déjà été vue.</summary>
    public static async Task<IReadOnlyList<string>> TakePendingItemsAsync()
    {
        if (E2EMode.IsEnabled) return [];
        var items = await LoadCurrentItemsAsync();
        if (!WhatsNew.ShouldShow(Preferences.Default.Get<string?>(LastSeenKey, null), CurrentVersion, items.Count)) return [];
        MarkSeen();
        return items;
    }

    /// <summary>Version installée considérée comme vue : appelé à la fin de l'accueil (première installation,
    /// rien de « nouveau » à annoncer) et quand le dialog est montré.</summary>
    public static void MarkSeen()
    {
        if (CurrentVersion is { } version) Preferences.Default.Set(LastSeenKey, version);
    }
}
