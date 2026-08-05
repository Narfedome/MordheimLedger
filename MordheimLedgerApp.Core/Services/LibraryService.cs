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
        TranslationResolver.ResolveAsync(_db.Connection, keys, languageCode);

    private Task<string> SetTranslationAsync(string? key, string languageCode, string value) =>
        TranslationResolver.SetAsync(_db.Connection, key, languageCode, value);

    public async Task<List<WarbandArchetype>> GetWarbandArchetypesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarbandArchetypeEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var specialRules = await LoadWarbandSpecialRulesAsync(languageCode);
        return rows.Select(r => r.ToModel(translations, specialRules)).ToList();
    }

    public async Task<WarbandArchetype?> GetWarbandArchetypeAsync(int id, string languageCode)
    {
        await _db.Initialization;
        var row = await _db.Connection.FindAsync<WarbandArchetypeEntity>(id);
        if (row is null) return null;
        var translations = await ResolveTranslationsAsync([row.NameKey, row.DescriptionKey], languageCode);
        var specialRules = await LoadWarbandSpecialRulesAsync(languageCode);
        return row.ToModel(translations, specialRules);
    }

    public async Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(int warbandArchetypeId, string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarriorArchetypeEntity>()
            .Where(w => w.WarbandArchetypeId == warbandArchetypeId)
            .ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var specialRules = await LoadWarriorSpecialRulesAsync(languageCode);
        return rows.Select(r => r.ToModel(translations, specialRules)).ToList();
    }

    public async Task<List<EquipmentItem>> GetEquipmentItemsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentItemEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadEquipmentRestrictionsAsync();
        return rows.Select(r => r.ToModel(translations, restrictions)).ToList();
    }

    public async Task<List<Skill>> GetSkillsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SkillEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadSkillRestrictionsAsync();
        return rows.Select(r => r.ToModel(translations, restrictions)).ToList();
    }

    public async Task<List<Injury>> GetInjuriesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<InjuryEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).ToList();
    }

    public async Task<List<Spell>> GetSpellsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SpellEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).OrderBy(s => s.SpellListName).ThenBy(s => s.RollValue).ToList();
    }

    public async Task<List<SpecialRule>> GetSpecialRulesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SpecialRuleEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).OrderBy(r => r.Name).ToList();
    }

    public async Task<List<Mutation>> GetMutationsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<MutationEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).OrderBy(r => r.Name).ToList();
    }

    public async Task<List<Mount>> GetMountsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<MountEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        var restrictions = await LoadMountRestrictionsAsync();
        var specialRules = await LoadMountSpecialRulesAsync(languageCode);
        return rows.Select(r => r.ToModel(translations, restrictions, specialRules)).OrderBy(r => r.Name).ToList();
    }

    private async Task<Dictionary<int, List<int>>> LoadMountRestrictionsAsync()
    {
        var rows = await _db.Connection.Table<WarbandArchetypeMountEntity>().ToListAsync();
        return rows.GroupBy(r => r.MountId).ToDictionary(g => g.Key, g => g.Select(r => r.WarbandArchetypeId).ToList());
    }

    private async Task<Dictionary<int, List<SpecialRule>>> LoadMountSpecialRulesAsync(string languageCode)
    {
        var rulesById = (await GetSpecialRulesAsync(languageCode)).ToDictionary(r => r.Id);
        var links = await _db.Connection.Table<MountSpecialRuleEntity>().ToListAsync();
        return links.GroupBy(l => l.MountId)
            .ToDictionary(g => g.Key, g => g.Select(l => rulesById[l.SpecialRuleId]).ToList());
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

    /// <summary>Replace-all: deletes the item's existing restriction rows and inserts the current list -
    /// no diffing needed at this scale.</summary>
    private async Task SaveEquipmentRestrictionsAsync(int equipmentItemId, List<int> warbandArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeEquipmentEntity WHERE EquipmentItemId = ?", equipmentItemId);
        foreach (var warbandArchetypeId in warbandArchetypeIds)
            await _db.Connection.InsertAsync(new WarbandArchetypeEquipmentEntity { EquipmentItemId = equipmentItemId, WarbandArchetypeId = warbandArchetypeId });
    }

    private async Task SaveSkillRestrictionsAsync(int skillId, List<int> warbandArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeSkillEntity WHERE SkillId = ?", skillId);
        foreach (var warbandArchetypeId in warbandArchetypeIds)
            await _db.Connection.InsertAsync(new WarbandArchetypeSkillEntity { SkillId = skillId, WarbandArchetypeId = warbandArchetypeId });
    }

    private async Task SaveMountRestrictionsAsync(int mountId, List<int> warbandArchetypeIds)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeMountEntity WHERE MountId = ?", mountId);
        foreach (var warbandArchetypeId in warbandArchetypeIds)
            await _db.Connection.InsertAsync(new WarbandArchetypeMountEntity { MountId = mountId, WarbandArchetypeId = warbandArchetypeId });
    }

    private async Task SaveMountSpecialRulesAsync(int mountId, List<SpecialRule> specialRules)
    {
        await _db.Connection.ExecuteAsync("DELETE FROM MountSpecialRuleEntity WHERE MountId = ?", mountId);
        foreach (var rule in specialRules)
            await _db.Connection.InsertAsync(new MountSpecialRuleEntity { MountId = mountId, SpecialRuleId = rule.Id });
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
            return;
        }

        var existing = await _db.Connection.FindAsync<MutationEntity>(mutation.Id);
        if (existing?.Source == ContentSource.Official) mutation.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(mutation.ToEntity());
    }

    public async Task SaveMountAsync(Mount mount, string languageCode)
    {
        await _db.Initialization;
        await ApplyTranslationsAsync(mount, languageCode);

        if (mount.Id == 0)
        {
            var entity = mount.ToEntity();
            await _db.Connection.InsertAsync(entity);
            mount.Id = entity.Id;
        }
        else
        {
            var existing = await _db.Connection.FindAsync<MountEntity>(mount.Id);
            if (existing?.Source == ContentSource.Official) mount.Source = ContentSource.Modified;
            await _db.Connection.UpdateAsync(mount.ToEntity());
        }

        await SaveMountRestrictionsAsync(mount.Id, mount.RestrictedToWarbandArchetypeIds);
        await SaveMountSpecialRulesAsync(mount.Id, mount.SpecialRules);
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

    private async Task ApplyTranslationsAsync(Skill m, string languageCode)
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

    private async Task ApplyTranslationsAsync(Mount m, string languageCode)
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

    public async Task DeleteSkillAsync(int skillId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<SkillEntity>(skillId);
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

    public async Task DeleteMountAsync(int mountId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<MountEntity>(mountId);
    }
}
