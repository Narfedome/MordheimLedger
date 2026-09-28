using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Pont vers les ids stables pour une base installée AVANT eux (2026-09-28) : aucune ligne n'y porte
/// d'OfficialId, alors que le code reconnaît désormais les entrées officielles par id (OfficialIds, table
/// d'Exploration, compétence accordée par un objet...). Chaque entrée Officielle ou Modifiée reçoit son id en
/// étant retrouvée, UNE dernière fois, par son nom anglais dans les JSON de seed - avec la même portée que
/// l'id lui-même (bande pour un guerrier/une liste, catégorie pour une compétence/une blessure). C'est le
/// seul endroit de l'appli où un nom sert encore d'identifiant, et il ne tourne qu'une fois par base.
///
/// Limites acceptées : une entrée Modifiée dont tu as changé le nom anglais n'est pas reconnue (elle reste
/// sans OfficialId, comme une entrée personnalisée) ; si une ancienne base contient deux lignes au même nom
/// (doublon hérité d'un ancien Backfill), seule la plus ancienne reçoit l'id. Une base neuve (copiée depuis
/// seed.db3) a déjà tous ses ids : no-op.</summary>
public partial class AppDatabase
{
    private const int OfficialIdBridgeDataVersion = 3;

    private async Task AssignOfficialIdsOnceAsync()
    {
        if (await _db.ExecuteScalarAsync<int>("PRAGMA user_version") >= OfficialIdBridgeDataVersion) return;

        var english = (await _db.Table<TranslationEntity>().Where(t => t.LanguageCode == "en").ToListAsync())
            .GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First().Value);
        string? En(string? key) => key is not null && english.TryGetValue(key, out var value) ? value : null;

        var ids = await LoadOfficialIdsByKeyAsync();
        var warbands = await BridgeAsync<WarbandArchetypeEntity>(ids.Warbands, r => r.Id, r => r.OfficialId, r => r.Source,
            r => En(r.NameKey), (r, id) => r.OfficialId = id);
        var warbandNamesById = warbands.Where(w => En(w.NameKey) is not null).ToDictionary(w => w.Id, w => En(w.NameKey)!);
        await BridgeAsync<WarriorArchetypeEntity>(ids.Warriors, r => r.Id, r => r.OfficialId, r => r.Source,
            r => ScopedKey(warbandNamesById.GetValueOrDefault(r.WarbandArchetypeId), En(r.NameKey)), (r, id) => r.OfficialId = id);
        await BridgeAsync<EquipmentListEntity>(ids.EquipmentLists, r => r.Id, r => r.OfficialId, r => r.Source,
            r => ScopedKey(warbandNamesById.GetValueOrDefault(r.WarbandArchetypeId), En(r.NameKey)), (r, id) => r.OfficialId = id);
        await BridgeAsync<SpecialRuleEntity>(ids.SpecialRules, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<EquipmentItemEntity>(ids.Equipment, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<SkillEntity>(ids.Skills, r => r.Id, r => r.OfficialId, r => r.Source,
            r => ScopedKey(r.Category.ToString(), En(r.NameKey)), (r, id) => r.OfficialId = id);
        await BridgeAsync<InjuryEntity>(ids.Injuries, r => r.Id, r => r.OfficialId, r => r.Source,
            r => ScopedKey(r.Category.ToString(), En(r.NameKey)), (r, id) => r.OfficialId = id);
        await BridgeAsync<MutationEntity>(ids.Mutations, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<MagicSchoolEntity>(ids.MagicSchools, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<SpellEntity>(ids.Spells, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<RaceEntity>(ids.Races, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<RacialProfileEntity>(ids.RacialProfiles, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<HiredSwordEntity>(ids.HiredSwords, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);
        await BridgeAsync<DramatisPersonaEntity>(ids.DramatisPersonae, r => r.Id, r => r.OfficialId, r => r.Source, r => En(r.NameKey), (r, id) => r.OfficialId = id);

        // Compétence accordée par un objet (symbole de la Maison du Marchand -> Haggle) : colonne nouvelle,
        // vide sur une ancienne base - reprise du JSON pour chaque objet désormais identifié.
        foreach (var item in await _db.Table<EquipmentItemEntity>().Where(e => e.OfficialId != null && e.GrantsSpecificSkillOfficialId == null).ToListAsync())
        {
            if (!ids.GrantedSkillByEquipment.TryGetValue(item.OfficialId!, out var skillId)) continue;
            item.GrantsSpecificSkillOfficialId = skillId;
            await _db.UpdateAsync(item);
        }

        // Aucune ligne d'Exploration à rattraper : la table est entièrement reconstruite depuis le JSON à
        // chaque lancement (ResyncExplorationResultsAsync), ids compris.
        await _db.ExecuteAsync($"PRAGMA user_version = {OfficialIdBridgeDataVersion}");
    }

    private static string? ScopedKey(string? scope, string? name) => scope is null || name is null ? null : $"{scope}|{name}";

    /// <summary>Pose sur chaque ligne d'une table l'id trouvé pour sa clé - jamais sur une entrée personnalisée,
    /// jamais si la ligne en a déjà un, jamais deux fois le même id : ceux déjà présents en base (posés par un
    /// Backfill* plus récent que la ligne) sont réservés d'avance, et pour un doublon hérité seule la première
    /// ligne par Id croissant reçoit l'id. Renvoie toutes les lignes de la table.</summary>
    private async Task<List<T>> BridgeAsync<T>(Dictionary<string, string> idsByKey, Func<T, int> rowId, Func<T, string?> officialId,
        Func<T, ContentSource> source, Func<T, string?> key, Action<T, string> setOfficialId) where T : new()
    {
        var rows = (await _db.Table<T>().ToListAsync()).OrderBy(rowId).ToList();
        var claimed = rows.Select(officialId).OfType<string>().ToHashSet();
        foreach (var row in rows)
        {
            if (officialId(row) is not null || source(row) == ContentSource.Custom) continue;
            if (key(row) is not { } k || !idsByKey.TryGetValue(k, out var id) || !claimed.Add(id)) continue;
            setOfficialId(row, id);
            await _db.UpdateAsync(row);
        }
        return rows;
    }

    private sealed class OfficialIdsByKey
    {
        public Dictionary<string, string> Warbands { get; } = new();
        public Dictionary<string, string> Warriors { get; } = new();
        public Dictionary<string, string> EquipmentLists { get; } = new();
        public Dictionary<string, string> SpecialRules { get; } = new();
        public Dictionary<string, string> Equipment { get; } = new();
        public Dictionary<string, string> Skills { get; } = new();
        public Dictionary<string, string> Injuries { get; } = new();
        public Dictionary<string, string> Mutations { get; } = new();
        public Dictionary<string, string> MagicSchools { get; } = new();
        public Dictionary<string, string> Spells { get; } = new();
        public Dictionary<string, string> Races { get; } = new();
        public Dictionary<string, string> RacialProfiles { get; } = new();
        public Dictionary<string, string> HiredSwords { get; } = new();
        public Dictionary<string, string> DramatisPersonae { get; } = new();
        public Dictionary<string, string> GrantedSkillByEquipment { get; } = new();

        public void AddRules(IEnumerable<SpecialRuleSeedData> rules)
        {
            foreach (var r in rules) SpecialRules.TryAdd(r.Name.En, r.Id);
        }

        public void AddEquipment(IEnumerable<EquipmentSeedData> items)
        {
            foreach (var e in items)
            {
                Equipment.TryAdd(e.Name.En, e.Id);
                if (e.GrantsSpecificSkillId is { } skillId) GrantedSkillByEquipment.TryAdd(e.Id, skillId);
                AddRules(e.SpecialRules);
            }
        }
    }

    /// <summary>Clé de correspondance -> id officiel, pour tout le contenu des JSON de seed. La clé suit la
    /// portée de l'id : nom anglais, préfixé du nom de la bande (guerrier, liste) ou de la catégorie
    /// (compétence, blessure).</summary>
    private static async Task<OfficialIdsByKey> LoadOfficialIdsByKeyAsync()
    {
        var ids = new OfficialIdsByKey();
        ids.AddRules(await LoadSeedArrayAsync<SpecialRuleSeedData>("SpecialRules.json"));
        ids.AddEquipment(await LoadSeedArrayAsync<EquipmentSeedData>("Equipment.json"));
        foreach (var m in await LoadSeedArrayAsync<MutationSeedData>("Mutations.json")) ids.Mutations.TryAdd(m.Name.En, m.Id);
        foreach (var s in await LoadSeedArrayAsync<SkillSeedData>("Skills.json")) ids.Skills.TryAdd($"{s.Category}|{s.Name.En}", s.Id);
        foreach (var i in await LoadSeedArrayAsync<InjurySeedData>("Injuries.json"))
        {
            ids.Injuries.TryAdd($"{i.Category}|{i.Name.En}", i.Id);
            ids.AddRules(i.SpecialRules);
        }
        foreach (var school in await LoadSeedArrayAsync<MagicSchoolWithSpellsSeedData>("MagicSchools.json"))
        {
            ids.MagicSchools.TryAdd(school.Name.En, school.Id);
            foreach (var sp in school.Spells) ids.Spells.TryAdd(sp.Name.En, sp.Id);
        }
        foreach (var r in await LoadSeedArrayAsync<RaceSeedData>("Races.json")) ids.Races.TryAdd(r.Name.En, r.Id);
        foreach (var p in await LoadSeedArrayAsync<RacialProfileSeedData>("RacialProfiles.json")) ids.RacialProfiles.TryAdd(p.Name.En, p.Id);
        foreach (var hs in await LoadSeedArrayAsync<HiredSwordSeedData>("HiredSwords.json"))
        {
            ids.HiredSwords.TryAdd(hs.Name.En, hs.Id);
            ids.AddRules(hs.SpecialRules);
        }
        foreach (var dp in await LoadSeedArrayAsync<DramatisPersonaSeedData>("DramatisPersonae.json"))
        {
            ids.DramatisPersonae.TryAdd(dp.Name.En, dp.Id);
            ids.AddRules(dp.SpecialRules);
        }
        foreach (var file in WarbandFileNames)
        {
            var band = await LoadWarbandSeedDataAsync(file);
            ids.Warbands.TryAdd(band.Name.En, band.Id);
            ids.AddRules(band.SpecialRules);
            foreach (var ms in band.MagicSchools) ids.MagicSchools.TryAdd(ms.Name.En, ms.Id);
            ids.AddEquipment(band.Equipment);
            foreach (var l in band.EquipmentLists) ids.EquipmentLists.TryAdd($"{band.Name.En}|{l.Name.En}", l.Id);
            foreach (var w in band.Warriors)
            {
                ids.Warriors.TryAdd($"{band.Name.En}|{w.Name.En}", w.Id);
                ids.AddRules(w.SpecialRules);
            }
            foreach (var s in band.Skills) ids.Skills.TryAdd($"{s.Category}|{s.Name.En}", s.Id);
            foreach (var sp in band.Spells) ids.Spells.TryAdd(sp.Name.En, sp.Id);
            foreach (var m in band.Mutations) ids.Mutations.TryAdd(m.Name.En, m.Id);
        }
        return ids;
    }
}
