using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

public class WarbandService : IWarbandService
{
    private readonly AppDatabase _db;

    public WarbandService(AppDatabase db) => _db = db;

    public async Task<List<Warband>> GetWarbandsAsync()
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<WarbandEntity>().ToListAsync();
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<Warband?> GetWarbandAsync(int id)
    {
        await _db.Initialization;
        var row = await _db.Connection.FindAsync<WarbandEntity>(id);
        return row?.ToModel();
    }

    public async Task<Warband> CreateWarbandAsync(string name, WarbandArchetype archetype)
    {
        await _db.Initialization;
        var warband = new Warband
        {
            Name = name,
            WarbandArchetypeId = archetype.Id,
            Treasury = archetype.StartingTreasury
        };
        var entity = warband.ToEntity();
        await _db.Connection.InsertAsync(entity);
        warband.Id = entity.Id;
        return warband;
    }

    public async Task SaveWarbandAsync(Warband warband)
    {
        await _db.Initialization;
        await _db.Connection.UpdateAsync(warband.ToEntity());
    }

    public async Task DeleteWarbandAsync(int warbandId)
    {
        await _db.Initialization;
        var warriors = await _db.Connection.Table<WarriorEntity>().Where(w => w.WarbandId == warbandId).ToListAsync();
        foreach (var warrior in warriors)
            await DeleteWarriorAsync(warrior.Id);

        await _db.Connection.DeleteAsync<WarbandEntity>(warbandId);
    }

    public async Task<List<Warrior>> GetWarriorsAsync(int warbandId)
    {
        await _db.Initialization;
        var warriorRows = await _db.Connection.Table<WarriorEntity>().Where(w => w.WarbandId == warbandId).ToListAsync();

        var warriors = new List<Warrior>();
        foreach (var row in warriorRows)
        {
            var carriedRows = await _db.Connection.Table<WarriorEquipmentEntity>().Where(e => e.WarriorId == row.Id).ToListAsync();
            var carried = new List<WarriorEquipment>();
            foreach (var carriedRow in carriedRows)
            {
                var itemEntity = await _db.Connection.FindAsync<EquipmentItemEntity>(carriedRow.EquipmentItemId);
                if (itemEntity is not null)
                    carried.Add(carriedRow.ToModel(itemEntity.ToModel()));
            }
            warriors.Add(row.ToModel(carried));
        }
        return warriors;
    }

    public async Task<Warrior> RecruitWarriorAsync(int warbandId, WarriorArchetype archetype, string name)
    {
        await _db.Initialization;
        var warrior = archetype.ToWarrior(name);
        warrior.WarbandId = warbandId;
        var entity = warrior.ToEntity();
        await _db.Connection.InsertAsync(entity);
        warrior.Id = entity.Id;
        return warrior;
    }

    public async Task SaveWarriorAsync(Warrior warrior)
    {
        await _db.Initialization;
        await _db.Connection.UpdateAsync(warrior.ToEntity());
    }

    public async Task DeleteWarriorAsync(int warriorId)
    {
        await _db.Initialization;
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorEquipmentEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.DeleteAsync<WarriorEntity>(warriorId);
    }
}
