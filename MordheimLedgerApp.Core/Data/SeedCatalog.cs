using System.Reflection;
using System.Text.Json;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Index en lecture seule du contenu des JSON de seed embarqués : nom anglais de chaque entrée à
/// partir de son id stable. Les fichiers de seed se citent entre eux par id (voir WarbandSeedData's Id) ;
/// quelques colonnes de la base stockent encore un NOM lu tel quel par l'appli (objet/matériau d'un
/// résultat d'Exploration, compétence accordée par un objet...) - le seed les remplit depuis cet index.
/// Aussi utilisé par les Backfill* qui rattrapent une base déjà installée, alors qu'aucun seed complet ne
/// tourne (donc aucun cache rempli au fil des insertions). Chargé une fois, à la demande.</summary>
internal static class SeedCatalog
{
    private static readonly Lazy<Index> _index = new(Build);

    public static string EquipmentName(string id) => Lookup(_index.Value.Equipment, id, "équipement");
    public static string SpecialRuleName(string id) => Lookup(_index.Value.SpecialRules, id, "règle spéciale");
    public static string SkillName(string id) => Lookup(_index.Value.Skills, id, "compétence");
    public static string WarbandName(string id) => Lookup(_index.Value.Warbands, id, "bande");
    public static string WarriorName(string id) => Lookup(_index.Value.Warriors, id, "guerrier");

    /// <summary>Profil racial par id - null si ce profil n'est pas (encore) écrit dans RacialProfiles.json,
    /// référence en avance tolérée (voir SeedWarbandFromJsonAsync).</summary>
    public static string? RacialProfileNameOrNull(string id) => _index.Value.RacialProfiles.GetValueOrDefault(id);

    /// <summary>Id de la règle spéciale portant ce nom anglais - pont pour une base installée avant les ids
    /// (voir AppDatabase.WarmSpecialRuleCacheAsync).</summary>
    public static string? SpecialRuleIdByName(string englishName) => _index.Value.SpecialRuleIdsByName.GetValueOrDefault(englishName);

    private static string Lookup(Dictionary<string, string> map, string id, string kind) =>
        map.TryGetValue(id, out var name) ? name : throw new InvalidOperationException($"Seed : {kind} inconnu(e) '{id}'");

    private sealed class Index
    {
        public Dictionary<string, string> Equipment { get; } = new();
        public Dictionary<string, string> SpecialRules { get; } = new();
        public Dictionary<string, string> SpecialRuleIdsByName { get; } = new();
        public Dictionary<string, string> Skills { get; } = new();
        public Dictionary<string, string> Warbands { get; } = new();
        public Dictionary<string, string> Warriors { get; } = new();
        public Dictionary<string, string> RacialProfiles { get; } = new();

        public void AddRules(IEnumerable<SpecialRuleSeedData> rules)
        {
            foreach (var r in rules)
            {
                SpecialRules.TryAdd(r.Id, r.Name.En);
                SpecialRuleIdsByName.TryAdd(r.Name.En, r.Id);
            }
        }
    }

    private static Index Build()
    {
        var index = new Index();
        index.AddRules(Load<List<SpecialRuleSeedData>>("SpecialRules.json"));
        foreach (var eq in Load<List<EquipmentSeedData>>("Equipment.json"))
        {
            index.Equipment.TryAdd(eq.Id, eq.Name.En);
            index.AddRules(eq.SpecialRules);
        }
        foreach (var sk in Load<List<SkillSeedData>>("Skills.json"))
            index.Skills.TryAdd(sk.Id, sk.Name.En);
        foreach (var inj in Load<List<InjurySeedData>>("Injuries.json"))
            index.AddRules(inj.SpecialRules);
        foreach (var hs in Load<List<HiredSwordSeedData>>("HiredSwords.json"))
            index.AddRules(hs.SpecialRules);
        foreach (var dp in Load<List<DramatisPersonaSeedData>>("DramatisPersonae.json"))
            index.AddRules(dp.SpecialRules);
        foreach (var rp in Load<List<RacialProfileSeedData>>("RacialProfiles.json"))
            index.RacialProfiles.TryAdd(rp.Id, rp.Name.En);
        foreach (var file in AppDatabase.WarbandFileNames)
        {
            var band = Load<WarbandSeedData>(file);
            index.Warbands.TryAdd(band.Id, band.Name.En);
            index.AddRules(band.SpecialRules);
            foreach (var eq in band.Equipment)
            {
                index.Equipment.TryAdd(eq.Id, eq.Name.En);
                index.AddRules(eq.SpecialRules);
            }
            foreach (var w in band.Warriors)
            {
                index.Warriors.TryAdd(w.Id, w.Name.En);
                index.AddRules(w.SpecialRules);
            }
            foreach (var sk in band.Skills)
                index.Skills.TryAdd(sk.Id, sk.Name.En);
        }
        return index;
    }

    private static T Load<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Empty or invalid seed file: {fileName}");
    }
}
