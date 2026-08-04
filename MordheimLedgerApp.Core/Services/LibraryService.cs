using MordheimLedgerApp.Core.Data;
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
        return rows.Select(r => r.ToModel(translations)).ToList();
    }

    public async Task<WarbandArchetype?> GetWarbandArchetypeAsync(int id, string languageCode)
    {
        await _db.Initialization;
        var row = await _db.Connection.FindAsync<WarbandArchetypeEntity>(id);
        if (row is null) return null;
        var translations = await ResolveTranslationsAsync([row.NameKey, row.DescriptionKey], languageCode);
        return row.ToModel(translations);
    }

    public async Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(int warbandArchetypeId, string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarriorArchetypeEntity>()
            .Where(w => w.WarbandArchetypeId == warbandArchetypeId)
            .ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).ToList();
    }

    public async Task<List<EquipmentItem>> GetEquipmentItemsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentItemEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).ToList();
    }

    public async Task<List<Skill>> GetSkillsAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SkillEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).ToList();
    }

    public async Task<List<Injury>> GetInjuriesAsync(string languageCode)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<InjuryEntity>().ToListAsync();
        var translations = await ResolveTranslationsAsync(rows.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }), languageCode);
        return rows.Select(r => r.ToModel(translations)).ToList();
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
            return;
        }

        var existing = await _db.Connection.FindAsync<WarbandArchetypeEntity>(archetype.Id);
        if (existing?.Source == ContentSource.Official) archetype.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(archetype.ToEntity());
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
            return;
        }

        var existing = await _db.Connection.FindAsync<WarriorArchetypeEntity>(archetype.Id);
        if (existing?.Source == ContentSource.Official) archetype.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(archetype.ToEntity());
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
            return;
        }

        var existing = await _db.Connection.FindAsync<EquipmentItemEntity>(item.Id);
        if (existing?.Source == ContentSource.Official) item.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(item.ToEntity());
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
            return;
        }

        var existing = await _db.Connection.FindAsync<SkillEntity>(skill.Id);
        if (existing?.Source == ContentSource.Official) skill.Source = ContentSource.Modified;
        await _db.Connection.UpdateAsync(skill.ToEntity());
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
}
