namespace MordheimLedgerApp.Core.Rules;

/// <summary>Combien d'éléments du Codex citent une race - affiché avant sa suppression (Codex › gérer les races).</summary>
public sealed record RaceUsage(int WarbandArchetypes, int WarriorArchetypes, int HiredSwords, int DramatisPersonae, int EquipmentItems)
{
    public int Total => WarbandArchetypes + WarriorArchetypes + HiredSwords + DramatisPersonae + EquipmentItems;
    public bool IsUsed => Total > 0;
}

/// <summary>Race d'un guerrier, distincte de son type (Capitaine, Zombie…), de sa classe (Héros, Homme de main, Franc-Tireur,
/// Dramatis Persona) et de son profil racial (maximums de caractéristiques) : une bande de Morts-Vivants mélange des
/// morts-vivants (Vampire, Zombies) et des humains (Nécromancien, Larbins). Un type sans race propre prend celle de sa bande.</summary>
public static class RaceRules
{
    /// <summary>Race effective : celle du type (ou du Franc-Tireur, du Dramatis Persona) si elle est renseignée, sinon celle
    /// de la bande. Null = inconnue.</summary>
    public static int? EffectiveRaceId(int? ownRaceId, int? warbandRaceId) =>
        ownRaceId is { } own && own > 0 ? own : warbandRaceId is { } band && band > 0 ? band : null;

    /// <summary>Un objet « Sans effet sur » ces races n'agit pas sur un guerrier de race effective raceId (race inconnue :
    /// l'objet agit, rien n'est signalé).</summary>
    public static bool IsWithoutEffect(int? raceId, IReadOnlyCollection<int> noEffectOnRaceIds) =>
        raceId is { } id && noEffectOnRaceIds.Contains(id);

    /// <summary>Liste d'ids stockée en colonne CSV (« 3,7 ») ; vide ou null = aucune.</summary>
    public static List<int> ParseIds(string? csv) =>
        string.IsNullOrEmpty(csv) ? new List<int>() : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
}
