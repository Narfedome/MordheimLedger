using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

public class LibraryService : ILibraryService
{
    private readonly AppDatabase _db;

    public LibraryService(AppDatabase db) => _db = db;

    private Task<Dictionary<string, string>> ResolveTranslationsAsync(IEnumerable<string?> keys, string languageCode) =>
        TranslationResolver.ResolveAsync(_db, keys, languageCode);

    private Task<string> SetTranslationAsync(string? key, string languageCode, string value) =>
        TranslationResolver.SetAsync(_db, key, languageCode, value);

    public async Task<List<WarbandArchetype>> GetWarbandArchetypesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarbandArchetypeEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var specialRules = await LoadWarbandSpecialRulesAsync(languageCode);
        var magicSchools = await LoadWarbandMagicSchoolsAsync(languageCode);
        var racesById = (await GetRacesAsync(languageCode)).ToDictionary(r => r.Id);
        return rows.Select(r => r.ToModel(translations, specialRules, magicSchools, racesById)).ToList();
    }

    public async Task<WarbandArchetype?> GetWarbandArchetypeAsync(int id, string languageCode)
    {
        await _db.Initialization;
        var row = await _db.Connection.FindAsync<WarbandArchetypeEntity>(id);
        if (row is null) return null;
        var translations = await ResolveTranslationsAsync([row.NameKey, row.DescriptionKey], languageCode);
        var specialRules = await LoadWarbandSpecialRulesAsync(languageCode);
        var magicSchools = await LoadWarbandMagicSchoolsAsync(languageCode);
        var racesById = (await GetRacesAsync(languageCode)).ToDictionary(r => r.Id);
        return row.ToModel(translations, specialRules, magicSchools, racesById);
    }

    public async Task<List<Race>> GetRacesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<RaceEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).OrderBy(r => r.Name).ToList();
    }

    public async Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(int warbandArchetypeId, string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarriorArchetypeEntity>()
            .Where(w => w.WarbandArchetypeId == warbandArchetypeId)
            .ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var specialRules = await LoadWarriorSpecialRulesAsync(languageCode);
        var racialProfilesById = (await GetRacialProfilesAsync(languageCode)).ToDictionary(r => r.Id);
        return rows.Select(r => r.ToModel(translations, specialRules, racialProfilesById)).ToList();
    }

    public async Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(IEnumerable<int> warbandArchetypeIds, string languageCode)
    {
        var ids = warbandArchetypeIds as ICollection<int> ?? warbandArchetypeIds.ToList();
        if (ids.Count == 0) return new List<WarriorArchetype>();

        await _db.Initialization;
        var rows = (await _db.Connection.Table<WarriorArchetypeEntity>().ToListAsync())
            .Where(w => ids.Contains(w.WarbandArchetypeId)).ToList();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var specialRules = await LoadWarriorSpecialRulesAsync(languageCode);
        var racialProfilesById = (await GetRacialProfilesAsync(languageCode)).ToDictionary(r => r.Id);
        return rows.Select(r => r.ToModel(translations, specialRules, racialProfilesById)).ToList();
    }

    public async Task<WarriorArchetype?> GetWarriorArchetypeAsync(int id, string languageCode)
    {
        await _db.Initialization;
        var row = await _db.Connection.FindAsync<WarriorArchetypeEntity>(id);
        if (row is null) return null;
        var translations = await ResolveTranslationsAsync([row.NameKey, row.DescriptionKey], languageCode);
        var specialRules = await LoadWarriorSpecialRulesAsync(languageCode);
        var racialProfilesById = (await GetRacialProfilesAsync(languageCode)).ToDictionary(r => r.Id);
        return row.ToModel(translations, specialRules, racialProfilesById);
    }

    public async Task<List<RacialProfile>> GetRacialProfilesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<RacialProfileEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).OrderBy(r => r.Name).ToList();
    }

    public async Task<List<EquipmentItem>> GetEquipmentItemsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentItemEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadEquipmentRestrictionsAsync();
        var warriorRestrictions = await LoadEquipmentWarriorRestrictionsAsync();
        var specialRules = await LoadEquipmentSpecialRulesAsync(languageCode);
        return rows.Select(r => r.ToModel(translations, restrictions, warriorRestrictions, specialRules)).ToList();
    }

    /// <summary>Same resolution as GetEquipmentItemsAsync(languageCode), but filtered to specific ids at
    /// the SQL level (WHERE Id IN (...)) instead of fetching+translating the whole Trading Post catalog
    /// and filtering in memory - used by EquipmentListDetailDialogViewModel to resolve one list's member
    /// items without paying for every other item's Name/Description translation. Empty input = empty
    /// result, no call.</summary>
    public async Task<List<EquipmentItem>> GetEquipmentItemsAsync(IEnumerable<int> ids, string languageCode)
    {
        var idSet = ids as ICollection<int> ?? ids.ToList();
        if (idSet.Count == 0) return new List<EquipmentItem>();

        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentItemEntity>().Where(r => idSet.Contains(r.Id)).ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadEquipmentRestrictionsAsync();
        var warriorRestrictions = await LoadEquipmentWarriorRestrictionsAsync();
        var specialRules = await LoadEquipmentSpecialRulesAsync(languageCode);
        return rows.Select(r => r.ToModel(translations, restrictions, warriorRestrictions, specialRules)).ToList();
    }

    public async Task<List<EquipmentList>> GetEquipmentListsAsync(int warbandArchetypeId, string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentListEntity>()
            .Where(l => l.WarbandArchetypeId == warbandArchetypeId)
            .ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.Select(r => r.NameKey), languageCode);
        var itemsByListId = await LoadEquipmentListMembershipAsync();
        return rows.Select(r => r.ToModel(translations, itemsByListId)).OrderBy(r => r.Name).ToList();
    }

    public async Task<List<NamedRef>> GetEquipmentListNamesAsync(int warbandArchetypeId, string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentListEntity>()
            .Where(l => l.WarbandArchetypeId == warbandArchetypeId)
            .ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.Select(r => r.NameKey), languageCode);
        return rows.Select(r => new NamedRef { Id = r.Id, Name = EntityMapping.ResolveName(r.NameKey, translations) })
            .OrderBy(r => r.Name).ToList();
    }

    private async Task<Dictionary<int, List<int>>> LoadEquipmentListMembershipAsync()
    {
        var rows = await _db.Connection.Table<EquipmentListItemEntity>().ToListAsync();
        return rows.GroupBy(r => r.EquipmentListId).ToDictionary(g => g.Key, g => g.Select(r => r.EquipmentItemId).ToList());
    }

    /// <summary>Item ids that are members of the given EquipmentList - the "starting list" channel
    /// consumed by the roster equipment picker, distinct from EquipmentItem.RestrictedToWarbandArchetypeIds
    /// (the Rare/Trading-Post channel).</summary>
    public async Task<HashSet<int>> GetEquipmentListItemIdsAsync(int equipmentListId)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentListItemEntity>()
            .Where(l => l.EquipmentListId == equipmentListId)
            .ToListAsync();
        return rows.Select(r => r.EquipmentItemId).ToHashSet();
    }

    public async Task<List<Skill>> GetSkillsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SkillEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadSkillRestrictionsAsync();
        var warriorRestrictions = await LoadSkillWarriorRestrictionsAsync();
        return rows.Select(r => r.ToModel(translations, restrictions, warriorRestrictions)).ToList();
    }

    public async Task<List<HiredSword>> GetHiredSwordsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<HiredSwordEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadHiredSwordRestrictionsAsync();
        var startingEquipment = await LoadHiredSwordEquipmentAsync();
        var specialRules = await LoadHiredSwordSpecialRulesAsync(languageCode);
        // Une poignée de Francs-Tireurs sont lanceurs de sorts en propre (ex. le Sorcier/"Warlock",
        // Magie Mineure) - voir Models.Library.HiredSword.MagicSchoolId, un simple FK, pas de table de
        // jointure comme les 3 chargements ci-dessus.
        var magicSchoolsById = (await GetMagicSchoolsAsync(languageCode)).ToDictionary(s => s.Id);
        return rows.Select(r => r.ToModel(translations, restrictions, startingEquipment, specialRules, magicSchoolsById)).ToList();
    }

    public async Task<List<Injury>> GetInjuriesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<InjuryEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var specialRules = await LoadInjurySpecialRulesAsync(languageCode);
        return rows.Select(r => r.ToModel(translations, specialRules)).ToList();
    }

    public async Task<List<ExplorationResult>> GetExplorationResultsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<ExplorationResultEntity>().ToListAsync();
        var outcomeEntities = await _db.Connection.Table<ExplorationOutcomeEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(
            rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey, r.ShortDescriptionKey })
                .Concat(outcomeEntities.Select(o => o.BranchTextKey))
                .Concat(outcomeEntities.Select(o => o.NextGameNoteTextKey)),
            languageCode);
        var outcomesByResultId = outcomeEntities
            .Select(o => o.ToModel(translations))
            .GroupBy(o => o.ExplorationResultId)
            .ToDictionary(g => g.Key, g => (IEnumerable<ExplorationOutcome>)g.ToList());
        return rows.Select(r => r.ToModel(translations, outcomesByResultId.GetValueOrDefault(r.Id)))
            .OrderBy(r => r.DiceCount).ThenBy(r => r.Value).ToList();
    }

    public async Task<List<Spell>> GetSpellsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SpellEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var magicSchoolsById = (await GetMagicSchoolsAsync(languageCode)).ToDictionary(s => s.Id);
        return rows.Select(r => r.ToModel(translations, magicSchoolsById))
            .OrderBy(s => s.MagicSchool?.Name).ThenBy(s => s.RollValue).ToList();
    }

    public async Task<List<MagicSchool>> GetMagicSchoolsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<MagicSchoolEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).OrderBy(r => r.Name).ToList();
    }

    public async Task<List<SpecialRule>> GetSpecialRulesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SpecialRuleEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).OrderBy(r => r.Name).ToList();
    }

    /// <summary>Ids of SpecialRules attached to at least one WarbandArchetype/WarriorArchetype
    /// (FighterRuleIds) vs. at least one EquipmentItem (ItemRuleIds - includes Animal-category items,
    /// since a mount is just an EquipmentItem now) - derived from the 3 attachment join tables rather
    /// than a stored category field, since a rule could in principle belong to both. Used by
    /// SpecialRuleViewModel's group filter (Codex "Guerriers &amp; Bandes" vs "Objets" split).</summary>
    public async Task<(HashSet<int> WarbandRuleIds, HashSet<int> WarriorRuleIds, HashSet<int> ItemRuleIds)> GetSpecialRuleAttachmentsAsync()
    {
        await _db.Initialization;
        var warbandIds = (await _db.Connection.Table<WarbandArchetypeSpecialRuleEntity>().ToListAsync()).Select(l => l.SpecialRuleId);
        var warriorIds = (await _db.Connection.Table<WarriorArchetypeSpecialRuleEntity>().ToListAsync()).Select(l => l.SpecialRuleId);
        // Un Franc-Tireur (HiredSword) est un type de guerrier recrutable comme un autre, juste sans
        // WarriorArchetype - une règle qui n'est attachée qu'à lui (ex. "Tête Dure"/"Vœu de Mort" du
        // Tueur de Troll Nain) doit tomber dans le même groupe "Guerriers" plutôt que "Non classée" -
        // trou repéré 2026-08-28 (ce join n'existait pas encore quand les Francs-Tireurs ont été ajoutés).
        var hiredSwordIds = (await _db.Connection.Table<HiredSwordSpecialRuleEntity>().ToListAsync()).Select(l => l.SpecialRuleId);
        var itemIds = (await _db.Connection.Table<EquipmentItemSpecialRuleEntity>().ToListAsync()).Select(l => l.SpecialRuleId);
        return (new HashSet<int>(warbandIds), new HashSet<int>(warriorIds.Concat(hiredSwordIds)), new HashSet<int>(itemIds));
    }

    public async Task<List<Mutation>> GetMutationsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<MutationEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadMutationRestrictionsAsync();
        return rows.Select(r => r.ToModel(translations, restrictions)).OrderBy(r => r.Name).ToList();
    }

    private async Task<Dictionary<int, List<int>>> LoadMutationRestrictionsAsync()
    {
        var rows = await _db.Connection.Table<WarbandArchetypeMutationEntity>().ToListAsync();
        return rows.GroupBy(r => r.MutationId).ToDictionary(g => g.Key, g => g.Select(r => r.WarbandArchetypeId).ToList());
    }

    private async Task SaveMutationRestrictionsAsync(int mutationId, List<int> warbandArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeMutationEntity WHERE MutationId = ?", mutationId);
        foreach (var warbandArchetypeId in warbandArchetypeIds)
            await _db.Connection.InsertAsync(new WarbandArchetypeMutationEntity { MutationId = mutationId, WarbandArchetypeId = warbandArchetypeId });
    }

    /// <summary>Loads the whole SpecialRule catalog (resolved) plus the whole band-level attachment join
    /// table once, then groups into WarbandArchetypeId -> resolved rules - same bulk-load idiom as
    /// LoadEquipmentRestrictionsAsync, except here we want the full rule (name+text), not just ids.</summary>
    private async Task<Dictionary<int, List<SpecialRule>>> LoadWarbandSpecialRulesAsync(string languageCode)
    {
        var rulesById = (await GetSpecialRulesAsync(languageCode)).ToDictionary(r => r.Id);
        var links = await _db.Connection.Table<WarbandArchetypeSpecialRuleEntity>().ToListAsync();
        return links.GroupBy(l => l.WarbandArchetypeId)
            .ToDictionary(g => g.Key, g => g.Select(l => rulesById[l.SpecialRuleId]).ToList());
    }

    private async Task<Dictionary<int, List<SpecialRule>>> LoadWarriorSpecialRulesAsync(string languageCode)
    {
        var rulesById = (await GetSpecialRulesAsync(languageCode)).ToDictionary(r => r.Id);
        var links = await _db.Connection.Table<WarriorArchetypeSpecialRuleEntity>().ToListAsync();
        return links.GroupBy(l => l.WarriorArchetypeId)
            .ToDictionary(g => g.Key, g => g.Select(l => rulesById[l.SpecialRuleId]).ToList());
    }

    /// <summary>Loads the whole MagicSchool catalog plus the whole band-grant join table once, then
    /// groups into WarbandArchetypeId -> resolved schools - same bulk idiom as LoadWarbandSpecialRulesAsync.</summary>
    private async Task<Dictionary<int, List<MagicSchool>>> LoadWarbandMagicSchoolsAsync(string languageCode)
    {
        var schoolsById = (await GetMagicSchoolsAsync(languageCode)).ToDictionary(s => s.Id);
        var links = await _db.Connection.Table<WarbandArchetypeMagicSchoolEntity>().ToListAsync();
        return links.GroupBy(l => l.WarbandArchetypeId)
            .ToDictionary(g => g.Key, g => g.Select(l => schoolsById[l.MagicSchoolId]).ToList());
    }

    /// <summary>Replace-all: deletes the band's existing MagicSchool grant rows and inserts the current
    /// list - no diffing needed at this scale.</summary>
    private async Task SaveWarbandMagicSchoolsAsync(int warbandArchetypeId, List<MagicSchool> magicSchools)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeMagicSchoolEntity WHERE WarbandArchetypeId = ?", warbandArchetypeId);
        foreach (var school in magicSchools)
            await _db.Connection.InsertAsync(new WarbandArchetypeMagicSchoolEntity { WarbandArchetypeId = warbandArchetypeId, MagicSchoolId = school.Id });
    }

    /// <summary>Replace-all: deletes the archetype's existing SpecialRule attachment rows and inserts
    /// the current list - no diffing needed at this scale.</summary>
    private async Task SaveWarbandSpecialRulesAsync(int warbandArchetypeId, List<SpecialRule> specialRules)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeSpecialRuleEntity WHERE WarbandArchetypeId = ?", warbandArchetypeId);
        foreach (var rule in specialRules)
            await _db.Connection.InsertAsync(new WarbandArchetypeSpecialRuleEntity { WarbandArchetypeId = warbandArchetypeId, SpecialRuleId = rule.Id });
    }

    private async Task SaveWarriorSpecialRulesAsync(int warriorArchetypeId, List<SpecialRule> specialRules)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorArchetypeSpecialRuleEntity WHERE WarriorArchetypeId = ?", warriorArchetypeId);
        foreach (var rule in specialRules)
            await _db.Connection.InsertAsync(new WarriorArchetypeSpecialRuleEntity { WarriorArchetypeId = warriorArchetypeId, SpecialRuleId = rule.Id });
    }

    /// <summary>Loads the whole restriction join table once (same "load whole table, filter in-memory"
    /// idiom as TranslationResolver) rather than a FindAsync per item - catalogs are small enough that
    /// this beats N+1 queries.</summary>
    private async Task<Dictionary<int, List<int>>> LoadEquipmentRestrictionsAsync()
    {
        var rows = await _db.Connection.Table<WarbandArchetypeEquipmentEntity>().ToListAsync();
        return rows.GroupBy(r => r.EquipmentItemId).ToDictionary(g => g.Key, g => g.Select(r => r.WarbandArchetypeId).ToList());
    }

    private async Task<Dictionary<int, List<int>>> LoadSkillRestrictionsAsync()
    {
        var rows = await _db.Connection.Table<WarbandArchetypeSkillEntity>().ToListAsync();
        return rows.GroupBy(r => r.SkillId).ToDictionary(g => g.Key, g => g.Select(r => r.WarbandArchetypeId).ToList());
    }

    private async Task<Dictionary<int, List<int>>> LoadHiredSwordRestrictionsAsync()
    {
        var rows = await _db.Connection.Table<WarbandArchetypeHiredSwordEntity>().ToListAsync();
        return rows.GroupBy(r => r.HiredSwordId).ToDictionary(g => g.Key, g => g.Select(r => r.WarbandArchetypeId).ToList());
    }

    private async Task<Dictionary<int, List<int>>> LoadHiredSwordEquipmentAsync()
    {
        var rows = await _db.Connection.Table<HiredSwordEquipmentEntity>().ToListAsync();
        return rows.GroupBy(r => r.HiredSwordId).ToDictionary(g => g.Key, g => g.Select(r => r.EquipmentItemId).ToList());
    }

    private async Task<Dictionary<int, List<SpecialRule>>> LoadHiredSwordSpecialRulesAsync(string languageCode)
    {
        var rulesById = (await GetSpecialRulesAsync(languageCode)).ToDictionary(r => r.Id);
        var links = await _db.Connection.Table<HiredSwordSpecialRuleEntity>().ToListAsync();
        return links.GroupBy(l => l.HiredSwordId)
            .ToDictionary(g => g.Key, g => g.Select(l => rulesById[l.SpecialRuleId]).ToList());
    }

    /// <summary>Seed-only axis (see Skill.RestrictedToWarriorArchetypeIds) - loaded/saved here so
    /// editing a seeded special skill through SkillEditDialog round-trips it instead of silently
    /// dropping it, even though the dialog has no UI to set it directly.</summary>
    private async Task<Dictionary<int, List<int>>> LoadSkillWarriorRestrictionsAsync()
    {
        var rows = await _db.Connection.Table<WarriorArchetypeSkillEntity>().ToListAsync();
        return rows.GroupBy(r => r.SkillId).ToDictionary(g => g.Key, g => g.Select(r => r.WarriorArchetypeId).ToList());
    }

    /// <summary>Seed-only axis (see EquipmentItem.RestrictedToWarriorArchetypeIds), same rationale as
    /// LoadSkillWarriorRestrictionsAsync.</summary>
    private async Task<Dictionary<int, List<int>>> LoadEquipmentWarriorRestrictionsAsync()
    {
        var rows = await _db.Connection.Table<WarriorArchetypeEquipmentEntity>().ToListAsync();
        return rows.GroupBy(r => r.EquipmentItemId).ToDictionary(g => g.Key, g => g.Select(r => r.WarriorArchetypeId).ToList());
    }

    private async Task<Dictionary<int, List<SpecialRule>>> LoadEquipmentSpecialRulesAsync(string languageCode)
    {
        var rulesById = (await GetSpecialRulesAsync(languageCode)).ToDictionary(r => r.Id);
        var links = await _db.Connection.Table<EquipmentItemSpecialRuleEntity>().ToListAsync();
        return links.GroupBy(l => l.EquipmentItemId)
            .ToDictionary(g => g.Key, g => g.Select(l => rulesById[l.SpecialRuleId]).ToList());
    }

    /// <summary>Same pattern as LoadEquipmentSpecialRulesAsync - rules permanently granted by an Injury
    /// (e.g. Stupidity/Frenzy from Madness, 24), merged into the carrying Warrior's own SpecialRules chip
    /// list by WarbandDetailViewModel.ToRow.</summary>
    private async Task<Dictionary<int, List<SpecialRule>>> LoadInjurySpecialRulesAsync(string languageCode)
    {
        var rulesById = (await GetSpecialRulesAsync(languageCode)).ToDictionary(r => r.Id);
        var links = await _db.Connection.Table<InjurySpecialRuleEntity>().ToListAsync();
        return links.GroupBy(l => l.InjuryId)
            .ToDictionary(g => g.Key, g => g.Select(l => rulesById[l.SpecialRuleId]).ToList());
    }

    /// <summary>Replace-all: deletes the item's existing restriction rows and inserts the current list -
    /// no diffing needed at this scale.</summary>
    private async Task SaveEquipmentRestrictionsAsync(int equipmentItemId, List<int> warbandArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeEquipmentEntity WHERE EquipmentItemId = ?", equipmentItemId);
        foreach (var warbandArchetypeId in warbandArchetypeIds)
            await _db.Connection.InsertAsync(new WarbandArchetypeEquipmentEntity { EquipmentItemId = equipmentItemId, WarbandArchetypeId = warbandArchetypeId });
    }

    private async Task SaveEquipmentWarriorRestrictionsAsync(int equipmentItemId, List<int> warriorArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorArchetypeEquipmentEntity WHERE EquipmentItemId = ?", equipmentItemId);
        foreach (var warriorArchetypeId in warriorArchetypeIds)
            await _db.Connection.InsertAsync(new WarriorArchetypeEquipmentEntity { EquipmentItemId = equipmentItemId, WarriorArchetypeId = warriorArchetypeId });
    }

    private async Task SaveEquipmentSpecialRulesAsync(int equipmentItemId, List<SpecialRule> specialRules)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM EquipmentItemSpecialRuleEntity WHERE EquipmentItemId = ?", equipmentItemId);
        foreach (var rule in specialRules)
            await _db.Connection.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = equipmentItemId, SpecialRuleId = rule.Id });
    }

    /// <summary>Replace-all membership of an EquipmentList's items - distinct from
    /// SaveEquipmentRestrictionsAsync (warband-gated Rare/Trading-Post channel).</summary>
    private async Task SaveEquipmentListItemsAsync(int equipmentListId, List<int> equipmentItemIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM EquipmentListItemEntity WHERE EquipmentListId = ?", equipmentListId);
        foreach (var equipmentItemId in equipmentItemIds)
            await _db.Connection.InsertAsync(new EquipmentListItemEntity { EquipmentListId = equipmentListId, EquipmentItemId = equipmentItemId });
    }

    private async Task SaveSkillRestrictionsAsync(int skillId, List<int> warbandArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeSkillEntity WHERE SkillId = ?", skillId);
        foreach (var warbandArchetypeId in warbandArchetypeIds)
            await _db.Connection.InsertAsync(new WarbandArchetypeSkillEntity { SkillId = skillId, WarbandArchetypeId = warbandArchetypeId });
    }

    private async Task SaveSkillWarriorRestrictionsAsync(int skillId, List<int> warriorArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorArchetypeSkillEntity WHERE SkillId = ?", skillId);
        foreach (var warriorArchetypeId in warriorArchetypeIds)
            await _db.Connection.InsertAsync(new WarriorArchetypeSkillEntity { SkillId = skillId, WarriorArchetypeId = warriorArchetypeId });
    }

    private async Task SaveHiredSwordRestrictionsAsync(int hiredSwordId, List<int> warbandArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeHiredSwordEntity WHERE HiredSwordId = ?", hiredSwordId);
        foreach (var warbandArchetypeId in warbandArchetypeIds)
            await _db.Connection.InsertAsync(new WarbandArchetypeHiredSwordEntity { HiredSwordId = hiredSwordId, WarbandArchetypeId = warbandArchetypeId });
    }

    private async Task SaveHiredSwordEquipmentAsync(int hiredSwordId, List<int> equipmentItemIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM HiredSwordEquipmentEntity WHERE HiredSwordId = ?", hiredSwordId);
        foreach (var equipmentItemId in equipmentItemIds)
            await _db.Connection.InsertAsync(new HiredSwordEquipmentEntity { HiredSwordId = hiredSwordId, EquipmentItemId = equipmentItemId });
    }

    private async Task SaveHiredSwordSpecialRulesAsync(int hiredSwordId, List<SpecialRule> specialRules)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM HiredSwordSpecialRuleEntity WHERE HiredSwordId = ?", hiredSwordId);
        foreach (var rule in specialRules)
            await _db.Connection.InsertAsync(new HiredSwordSpecialRuleEntity { HiredSwordId = hiredSwordId, SpecialRuleId = rule.Id });
    }

    public async Task SaveWarbandArchetypeAsync(WarbandArchetype archetype, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(archetype, languageCode);

        if (archetype.Id == 0)
        {
            var entity = archetype.ToEntity();
            await _db.Connection.InsertAsync(entity);
            archetype.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<WarbandArchetypeEntity>(archetype.Id);
            if (existing?.Source == ContentSource.Official) archetype.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(archetype.ToEntity());
        }

        await SaveWarbandSpecialRulesAsync(archetype.Id, archetype.SpecialRules);
        await SaveWarbandMagicSchoolsAsync(archetype.Id, archetype.MagicSchools);
    }

    public async Task SaveWarriorArchetypeAsync(WarriorArchetype archetype, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(archetype, languageCode);

        if (archetype.Id == 0)
        {
            var entity = archetype.ToEntity();
            await _db.Connection.InsertAsync(entity);
            archetype.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<WarriorArchetypeEntity>(archetype.Id);
            if (existing?.Source == ContentSource.Official) archetype.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(archetype.ToEntity());
        }

        await SaveWarriorSpecialRulesAsync(archetype.Id, archetype.SpecialRules);
    }

    public async Task SaveEquipmentItemAsync(EquipmentItem item, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(item, languageCode);

        if (item.Id == 0)
        {
            var entity = item.ToEntity();
            await _db.Connection.InsertAsync(entity);
            item.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<EquipmentItemEntity>(item.Id);
            if (existing?.Source == ContentSource.Official) item.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(item.ToEntity());
        }

        await SaveEquipmentRestrictionsAsync(item.Id, item.RestrictedToWarbandArchetypeIds);
        await SaveEquipmentWarriorRestrictionsAsync(item.Id, item.RestrictedToWarriorArchetypeIds);
        await SaveEquipmentSpecialRulesAsync(item.Id, item.SpecialRules);
    }

    public async Task SaveEquipmentListAsync(EquipmentList list, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(list, languageCode);

        if (list.Id == 0)
        {
            var entity = list.ToEntity();
            await _db.Connection.InsertAsync(entity);
            list.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<EquipmentListEntity>(list.Id);
            if (existing?.Source == ContentSource.Official) list.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(list.ToEntity());
        }

        await SaveEquipmentListItemsAsync(list.Id, list.ItemIds);
    }

    public async Task SaveSkillAsync(Skill skill, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(skill, languageCode);

        if (skill.Id == 0)
        {
            var entity = skill.ToEntity();
            await _db.Connection.InsertAsync(entity);
            skill.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<SkillEntity>(skill.Id);
            if (existing?.Source == ContentSource.Official) skill.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(skill.ToEntity());
        }

        await SaveSkillRestrictionsAsync(skill.Id, skill.RestrictedToWarbandArchetypeIds);
        await SaveSkillWarriorRestrictionsAsync(skill.Id, skill.RestrictedToWarriorArchetypeIds);
    }

    public async Task SaveHiredSwordAsync(HiredSword hiredSword, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(hiredSword, languageCode);

        if (hiredSword.Id == 0)
        {
            var entity = hiredSword.ToEntity();
            await _db.Connection.InsertAsync(entity);
            hiredSword.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<HiredSwordEntity>(hiredSword.Id);
            if (existing?.Source == ContentSource.Official) hiredSword.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(hiredSword.ToEntity());
        }

        await SaveHiredSwordRestrictionsAsync(hiredSword.Id, hiredSword.RestrictedToWarbandArchetypeIds);
        await SaveHiredSwordEquipmentAsync(hiredSword.Id, hiredSword.StartingEquipmentIds);
        await SaveHiredSwordSpecialRulesAsync(hiredSword.Id, hiredSword.SpecialRules);
    }

    public async Task SaveInjuryAsync(Injury injury, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(injury, languageCode);

        if (injury.Id == 0)
        {
            var entity = injury.ToEntity();
            await _db.Connection.InsertAsync(entity);
            injury.Id = entity.Id;
            return;
        }

        var existing = await _db.Connection.FindAsync<InjuryEntity>(injury.Id);
        if (existing?.Source == ContentSource.Official) injury.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(injury.ToEntity());
    }

    public async Task SaveSpellAsync(Spell spell, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(spell, languageCode);

        if (spell.Id == 0)
        {
            var entity = spell.ToEntity();
            await _db.Connection.InsertAsync(entity);
            spell.Id = entity.Id;
            return;
        }

        var existing = await _db.Connection.FindAsync<SpellEntity>(spell.Id);
        if (existing?.Source == ContentSource.Official) spell.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(spell.ToEntity());
    }

    public async Task SaveSpecialRuleAsync(SpecialRule rule, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(rule, languageCode);

        if (rule.Id == 0)
        {
            var entity = rule.ToEntity();
            await _db.Connection.InsertAsync(entity);
            rule.Id = entity.Id;
            return;
        }

        var existing = await _db.Connection.FindAsync<SpecialRuleEntity>(rule.Id);
        if (existing?.Source == ContentSource.Official) rule.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(rule.ToEntity());
    }

    public async Task SaveMutationAsync(Mutation mutation, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(mutation, languageCode);

        if (mutation.Id == 0)
        {
            var entity = mutation.ToEntity();
            await _db.Connection.InsertAsync(entity);
            mutation.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<MutationEntity>(mutation.Id);
            if (existing?.Source == ContentSource.Official) mutation.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(mutation.ToEntity());
        }

        await SaveMutationRestrictionsAsync(mutation.Id, mutation.RestrictedToWarbandArchetypeIds);
    }

    public async Task SaveMagicSchoolAsync(MagicSchool school, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(school, languageCode);

        if (school.Id == 0)
        {
            var entity = school.ToEntity();
            await _db.Connection.InsertAsync(entity);
            school.Id = entity.Id;
            return;
        }

        var existing = await _db.Connection.FindAsync<MagicSchoolEntity>(school.Id);
        if (existing?.Source == ContentSource.Official) school.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(school.ToEntity());
    }

    public async Task SaveRaceAsync(Race race, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(race, languageCode);

        if (race.Id == 0)
        {
            var entity = race.ToEntity();
            await _db.Connection.InsertAsync(entity);
            race.Id = entity.Id;
            return;
        }

        var existing = await _db.Connection.FindAsync<RaceEntity>(race.Id);
        if (existing?.Source == ContentSource.Official) race.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(race.ToEntity());
    }

    public async Task SaveRacialProfileAsync(RacialProfile racialProfile, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(racialProfile, languageCode);

        if (racialProfile.Id == 0)
        {
            var entity = racialProfile.ToEntity();
            await _db.Connection.InsertAsync(entity);
            racialProfile.Id = entity.Id;
            return;
        }

        var existing = await _db.Connection.FindAsync<RacialProfileEntity>(racialProfile.Id);
        if (existing?.Source == ContentSource.Official) racialProfile.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(racialProfile.ToEntity());
    }

    /// <summary>Writes Name (and Description, when non-blank) as the translation value for
    /// languageCode, allocating a key on first save - shared by all 5 Save*Async methods above since
    /// they otherwise only differ in entity type.</summary>
    private async Task ApplyTranslationsAsync(WarbandArchetype m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(WarriorArchetype m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(EquipmentItem m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(EquipmentList m, string languageCode) =>
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);

    private async Task ApplyTranslationsAsync(Skill m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(HiredSword m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(Injury m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(Spell m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(SpecialRule m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(Mutation m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(MagicSchool m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(Race m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    private async Task ApplyTranslationsAsync(RacialProfile m, string languageCode)
    {
        m.NameKey = await SetTranslationAsync(m.NameKey, languageCode, m.Name);
        m.DescriptionKey = string.IsNullOrWhiteSpace(m.Description)
            ? null
            : await SetTranslationAsync(m.DescriptionKey, languageCode, m.Description);
    }

    public async Task DeleteWarbandArchetypeAsync(int warbandArchetypeId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarbandArchetypeEntity>(warbandArchetypeId);
    }

    public async Task DeleteWarriorArchetypeAsync(int warriorArchetypeId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarriorArchetypeEntity>(warriorArchetypeId);
    }

    public async Task DeleteEquipmentItemAsync(int equipmentItemId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<EquipmentItemEntity>(equipmentItemId);
    }

    public async Task DeleteEquipmentListAsync(int equipmentListId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<EquipmentListEntity>(equipmentListId);
    }

    public async Task DeleteSkillAsync(int skillId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<SkillEntity>(skillId);
    }

    public async Task DeleteHiredSwordAsync(int hiredSwordId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<HiredSwordEntity>(hiredSwordId);
    }

    public async Task DeleteInjuryAsync(int injuryId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<InjuryEntity>(injuryId);
    }

    public async Task DeleteSpellAsync(int spellId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<SpellEntity>(spellId);
    }

    public async Task DeleteSpecialRuleAsync(int specialRuleId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<SpecialRuleEntity>(specialRuleId);
    }

    public async Task DeleteMutationAsync(int mutationId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<MutationEntity>(mutationId);
    }

    public async Task DeleteMagicSchoolAsync(int magicSchoolId)
    {
        await _db.Initialization;

        // Un Spell n'a pas de sens sans son école (Spell.MagicSchoolId non-nullable) - supprimer
        // l'école sans ses sorts laisserait des lignes SpellEntity orphelines.
        var orphanedSpells = await _db.Connection.Table<SpellEntity>().Where(s => s.MagicSchoolId == magicSchoolId).ToListAsync();
        foreach (var spell in orphanedSpells)
            await _db.Connection.DeleteAsync<SpellEntity>(spell.Id);

        // Idem pour les octrois de bande (WarbandArchetypeMagicSchoolEntity) - une ligne orpheline ici
        // fait planter LoadWarbandMagicSchoolsAsync (KeyNotFoundException, l'école n'existe plus dans le
        // dictionnaire résolu depuis GetMagicSchoolsAsync).
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeMagicSchoolEntity WHERE MagicSchoolId = ?", magicSchoolId);

        await _db.Connection.DeleteAsync<MagicSchoolEntity>(magicSchoolId);
    }

    public async Task DeleteRaceAsync(int raceId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<RaceEntity>(raceId);
    }

    public async Task DeleteRacialProfileAsync(int racialProfileId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<RacialProfileEntity>(racialProfileId);
    }
}
