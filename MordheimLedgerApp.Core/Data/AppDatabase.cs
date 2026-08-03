using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data;

public class AppDatabase
{
    private readonly SQLiteAsyncConnection _db;
    public SQLiteAsyncConnection Connection => _db;

    /// <summary>
    /// Table creation, run at construction. Data services must await this before their first query:
    /// SQLiteAsyncConnection does not guarantee ordering between operations, so a query issued before
    /// init completes could hit "no such table" on first launch.
    /// </summary>
    public Task Initialization { get; }

    public AppDatabase(string path)
    {
        _db = new SQLiteAsyncConnection(path);
        Initialization = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _db.CreateTableAsync<CampaignEntity>();
        await _db.CreateTableAsync<WarbandArchetypeEntity>();
        await _db.CreateTableAsync<WarbandEntity>();
        await _db.CreateTableAsync<WarriorArchetypeEntity>();
        await _db.CreateTableAsync<WarriorEntity>();
        await _db.CreateTableAsync<EquipmentItemEntity>();
        await _db.CreateTableAsync<WarriorEquipmentEntity>();
    }
}
