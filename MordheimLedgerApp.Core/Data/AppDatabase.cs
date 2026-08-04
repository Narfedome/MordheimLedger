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
        await _db.CreateTableAsync<SkillEntity>();
        await _db.CreateTableAsync<InjuryEntity>();
        await _db.CreateTableAsync<WarriorEquipmentEntity>();
        await _db.CreateTableAsync<WarriorSkillEntity>();
        await _db.CreateTableAsync<WarriorInjuryEntity>();
        await _db.CreateTableAsync<HistoryEntryEntity>();
        await _db.CreateTableAsync<TranslationEntity>();

        // First-launch only: if the archetype catalog is empty, nothing has been seeded yet (and
        // nothing the player made is at risk of being duplicated).
        if (await _db.Table<WarbandArchetypeEntity>().CountAsync() == 0)
            await SeedOfficialContentAsync();
    }

    private async Task SeedOfficialContentAsync()
    {
        var warband = OfficialContentSeed.ReiklanderMercenaries;
        warband.NameKey = await SeedTranslationAsync(warband.Name, OfficialContentSeed.ReiklanderMercenariesFr.Name);
        warband.DescriptionKey = await SeedTranslationAsync(warband.Description!, OfficialContentSeed.ReiklanderMercenariesFr.Description);
        var warbandArchetypeEntity = warband.ToEntity();
        await _db.InsertAsync(warbandArchetypeEntity);

        var warriors = OfficialContentSeed.ReiklanderMercenariesWarriors(warbandArchetypeEntity.Id);
        for (var i = 0; i < warriors.Count; i++)
        {
            var warrior = warriors[i];
            var fr = OfficialContentSeed.ReiklanderMercenariesWarriorsFr[i];
            warrior.NameKey = await SeedTranslationAsync(warrior.Name, fr.Name);
            warrior.DescriptionKey = warrior.Description is null
                ? null
                : await SeedTranslationAsync(warrior.Description, fr.Description);
            await _db.InsertAsync(warrior.ToEntity());
        }

        var equipment = OfficialContentSeed.CoreEquipment;
        for (var i = 0; i < equipment.Count; i++)
        {
            var item = equipment[i];
            var fr = OfficialContentSeed.CoreEquipmentFr[i];
            item.NameKey = await SeedTranslationAsync(item.Name, fr.Name);
            item.DescriptionKey = item.Description is null
                ? null
                : await SeedTranslationAsync(item.Description, fr.Description);
            await _db.InsertAsync(item.ToEntity());
        }
    }

    /// <summary>Allocates a fresh translation key and writes the English (seed data's authoring
    /// language) and, when supplied, French values for it directly - bypasses LibraryService (which
    /// also does the Official-&gt;Modified flip check, irrelevant for a brand new insert).</summary>
    private async Task<string> SeedTranslationAsync(string en, string? fr)
    {
        var key = Guid.NewGuid().ToString("N");
        await _db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = "en", Value = en });
        if (!string.IsNullOrEmpty(fr))
            await _db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = "fr", Value = fr });
        return key;
    }
}
