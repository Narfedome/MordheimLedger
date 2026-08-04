using System.Reflection;
using System.Text.Json;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;
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
        await _db.CreateTableAsync<SpellEntity>();
        await _db.CreateTableAsync<WarbandArchetypeEquipmentEntity>();
        await _db.CreateTableAsync<WarbandArchetypeSkillEntity>();

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

        // Pilot for the JSON-driven seed path (see WarbandSeedData) - Reiklander above stays on the
        // original hardcoded OfficialContentSeed.cs shape to prove both mechanisms coexist. More
        // warbands get added here as their JSON files are authored.
        await SeedWarbandFromJsonAsync("MortsVivants.json");
        await SeedWarbandFromJsonAsync("ChasseursDeTresorsNains.json");
    }

    /// <summary>Deserializes an embedded Data/SeedData/*.json file and inserts its warband, warrior
    /// archetypes, band-specific equipment (with restriction rows where flagged) and spells - each
    /// translatable field gets a fresh key via SeedTranslationAsync, same as the Reiklander seed above.</summary>
    private async Task SeedWarbandFromJsonAsync(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var data = await JsonSerializer.DeserializeAsync<WarbandSeedData>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Empty or invalid seed file: {fileName}");

        var warband = new WarbandArchetype
        {
            Source = ContentSource.Official,
            StartingTreasury = data.StartingTreasury,
            MaxWarriors = data.MaxWarriors
        };
        warband.NameKey = await SeedTranslationAsync(data.Name.En, data.Name.Fr);
        warband.DescriptionKey = data.Description is null ? null : await SeedTranslationAsync(data.Description.En, data.Description.Fr);
        var warbandEntity = warband.ToEntity();
        await _db.InsertAsync(warbandEntity);

        foreach (var w in data.Warriors)
        {
            var warrior = new WarriorArchetype
            {
                WarbandArchetypeId = warbandEntity.Id,
                IsHero = w.IsHero,
                Cost = w.Cost,
                MaxCount = w.MaxCount,
                StartingExperience = w.StartingExperience,
                Movement = w.Movement,
                WeaponSkill = w.WeaponSkill,
                BallisticSkill = w.BallisticSkill,
                Strength = w.Strength,
                Toughness = w.Toughness,
                Wounds = w.Wounds,
                Initiative = w.Initiative,
                Attacks = w.Attacks,
                Leadership = w.Leadership,
                Source = ContentSource.Official,
                SpellListName = w.SpellListName
            };
            warrior.NameKey = await SeedTranslationAsync(w.Name.En, w.Name.Fr);
            warrior.DescriptionKey = w.Description is null ? null : await SeedTranslationAsync(w.Description.En, w.Description.Fr);
            await _db.InsertAsync(warrior.ToEntity());
        }

        foreach (var eq in data.Equipment)
        {
            var item = new EquipmentItem
            {
                Category = Enum.Parse<EquipmentCategory>(eq.Category),
                Cost = eq.Cost,
                Rarity = eq.Rarity,
                Source = ContentSource.Official
            };
            item.NameKey = await SeedTranslationAsync(eq.Name.En, eq.Name.Fr);
            item.DescriptionKey = eq.Description is null ? null : await SeedTranslationAsync(eq.Description.En, eq.Description.Fr);
            var itemEntity = item.ToEntity();
            await _db.InsertAsync(itemEntity);

            if (eq.RestrictedToThisWarband)
                await _db.InsertAsync(new WarbandArchetypeEquipmentEntity { WarbandArchetypeId = warbandEntity.Id, EquipmentItemId = itemEntity.Id });
        }

        foreach (var sp in data.Spells)
        {
            var spell = new Spell
            {
                SpellListName = sp.SpellListName,
                RollValue = sp.RollValue,
                Difficulty = sp.Difficulty,
                Source = ContentSource.Official
            };
            spell.NameKey = await SeedTranslationAsync(sp.Name.En, sp.Name.Fr);
            spell.DescriptionKey = sp.Description is null ? null : await SeedTranslationAsync(sp.Description.En, sp.Description.Fr);
            await _db.InsertAsync(spell.ToEntity());
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
