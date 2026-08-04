using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

public class LibraryService : ILibraryService
{
    private readonly AppDatabase _db;

    public LibraryService(AppDatabase db) => _db = db;

    public async Task<List<WarbandArchetype>> GetWarbandArchetypesAsync()
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarbandArchetypeEntity>().ToListAsync();
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<WarbandArchetype?> GetWarbandArchetypeAsync(int id)
    {
        await _db.Initialization;
        var row = await _db.Connection.FindAsync<WarbandArchetypeEntity>(id);
        return row?.ToModel();
    }

    public async Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(int warbandArchetypeId)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarriorArchetypeEntity>()
            .Where(w => w.WarbandArchetypeId == warbandArchetypeId)
            .ToListAsync();
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<List<EquipmentItem>> GetEquipmentItemsAsync()
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<EquipmentItemEntity>().ToListAsync();
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<List<Skill>> GetSkillsAsync()
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<SkillEntity>().ToListAsync();
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task SaveWarbandArchetypeAsync(WarbandArchetype archetype)
    {
        await _db.Initialization;
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

    public async Task SaveWarriorArchetypeAsync(WarriorArchetype archetype)
    {
        await _db.Initialization;
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

    public async Task SaveEquipmentItemAsync(EquipmentItem item)
    {
        await _db.Initialization;
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

    public async Task SaveSkillAsync(Skill skill)
    {
        await _db.Initialization;
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
}
