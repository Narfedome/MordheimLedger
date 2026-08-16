using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

public class WarbandService : IWarbandService
{
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;

    public WarbandService(AppDatabase db, ILibraryService library)
    {
        _db = db;
        _library = library;
    }

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
    public async Task<string> GetWarbandArchetypeNameAsync(int id, string languageCode)
    {
        await _db.Initialization;
        var row = await _db.Connection.FindAsync<WarbandArchetypeEntity>(id);

        var translations = await TranslationResolver.ResolveAsync(_db, [row.NameKey, row.DescriptionKey], languageCode);
        return translations[row.NameKey];
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

    public async Task<int> GetWarbandRatingAsync(int warbandId)
    {
        await _db.Initialization;
        var warriors = await _db.Connection.Table<WarriorEntity>()
            .Where(w => w.WarbandId == warbandId && w.Status == WarriorStatus.Active)
            .ToListAsync();
        return warriors.Sum(w => ((w.IsLargeCreature ? 20 : 5) + w.Experience) * w.HeadCount);
    }

    public async Task<List<Warrior>> GetWarriorsAsync(int warbandId, string languageCode)
    {
        await _db.Initialization;
        var warriorRows = await _db.Connection.Table<WarriorEntity>().Where(w => w.WarbandId == warbandId).ToListAsync();

        // Chargées une seule fois via LibraryService (résolution complète : restrictions +
        // SpecialRules, comme le Codex) plutôt qu'un FindAsync+ToModel(translations) minimal par ligne
        // portée - ce dernier laissait EquipmentItem.SpecialRules/RestrictedToXxxIds vides pour tout
        // objet/monture porté par un guerrier déjà recruté (repéré : SpecialRules manquantes dans le
        // dialog récap ouvert depuis la fiche de bande). Skill/Mutation n'ont pas de SpecialRules mais
        // ont le même trou sur leurs restrictions.
        var equipmentById = (await _library.GetEquipmentItemsAsync(languageCode)).ToDictionary(i => i.Id);
        var skillById = (await _library.GetSkillsAsync(languageCode)).ToDictionary(s => s.Id);
        var mutationById = (await _library.GetMutationsAsync(languageCode)).ToDictionary(m => m.Id);

        var warriors = new List<Warrior>();
        foreach (var row in warriorRows)
        {
            var carriedRows = await _db.Connection.Table<WarriorEquipmentEntity>().Where(e => e.WarriorId == row.Id).ToListAsync();
            var carried = new List<WarriorEquipment>();
            foreach (var carriedRow in carriedRows)
            {
                if (!equipmentById.TryGetValue(carriedRow.EquipmentItemId, out var item)) continue;

                SpecialRule? materialRule = null;
                if (carriedRow.MaterialSpecialRuleId is { } materialRuleId)
                {
                    var materialEntity = await _db.Connection.FindAsync<SpecialRuleEntity>(materialRuleId);
                    if (materialEntity is not null)
                    {
                        var materialTranslations = await TranslationResolver.ResolveAsync(_db, [materialEntity.NameKey, materialEntity.DescriptionKey], languageCode);
                        materialRule = materialEntity.ToModel(materialTranslations);
                    }
                }

                carried.Add(carriedRow.ToModel(item, materialRule));
            }

            var learnedRows = await _db.Connection.Table<WarriorSkillEntity>().Where(s => s.WarriorId == row.Id).ToListAsync();
            var learned = new List<WarriorSkill>();
            foreach (var learnedRow in learnedRows)
                if (skillById.TryGetValue(learnedRow.SkillId, out var skill))
                    learned.Add(learnedRow.ToModel(skill));

            var injuryRows = await _db.Connection.Table<WarriorInjuryEntity>().Where(i => i.WarriorId == row.Id).ToListAsync();
            var injuries = new List<WarriorInjury>();
            foreach (var injuryRow in injuryRows)
            {
                var injuryEntity = await _db.Connection.FindAsync<InjuryEntity>(injuryRow.InjuryId);
                if (injuryEntity is not null)
                {
                    var translations = await TranslationResolver.ResolveAsync(_db, [injuryEntity.NameKey, injuryEntity.DescriptionKey], languageCode);
                    injuries.Add(injuryRow.ToModel(injuryEntity.ToModel(translations)));
                }
            }

            var spellRows = await _db.Connection.Table<WarriorSpellEntity>().Where(s => s.WarriorId == row.Id).ToListAsync();
            var spells = new List<WarriorSpell>();
            foreach (var spellRow in spellRows)
            {
                var spellEntity = await _db.Connection.FindAsync<SpellEntity>(spellRow.SpellId);
                if (spellEntity is not null)
                {
                    var translations = await TranslationResolver.ResolveAsync(_db, [spellEntity.NameKey, spellEntity.DescriptionKey], languageCode);
                    spells.Add(spellRow.ToModel(spellEntity.ToModel(translations)));
                }
            }

            var mutationRows = await _db.Connection.Table<WarriorMutationEntity>().Where(m => m.WarriorId == row.Id).ToListAsync();
            var mutations = new List<WarriorMutation>();
            foreach (var mutationRow in mutationRows)
                if (mutationById.TryGetValue(mutationRow.MutationId, out var mutation))
                    mutations.Add(mutationRow.ToModel(mutation));

            EquipmentItem? animal = null;
            if (row.AnimalId is { } animalId)
                equipmentById.TryGetValue(animalId, out animal);

            warriors.Add(row.ToModel(carried, learned, injuries, spells, mutations, animal));
        }
        return warriors;
    }

    public async Task<Warrior> RecruitWarriorAsync(int warbandId, WarriorArchetype archetype, string name, int headCount = 1)
    {
        await _db.Initialization;
        var warrior = archetype.ToWarrior(name);
        warrior.WarbandId = warbandId;
        warrior.HeadCount = headCount;
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
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorSkillEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorInjuryEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorSpellEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorMutationEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.DeleteAsync<WarriorEntity>(warriorId);
    }

    public async Task<WarriorEquipment> AddWarriorEquipmentAsync(int warriorId, EquipmentItem item, int quantity = 1, SpecialRule? materialRule = null)
    {
        await _db.Initialization;
        var carried = new WarriorEquipment { WarriorId = warriorId, Item = item, Quantity = quantity, MaterialRule = materialRule };
        var entity = carried.ToEntity();
        await _db.Connection.InsertAsync(entity);
        carried.Id = entity.Id;
        return carried;
    }

    public async Task RemoveWarriorEquipmentAsync(int warriorEquipmentId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarriorEquipmentEntity>(warriorEquipmentId);
    }

    public async Task<WarriorSkill> AddWarriorSkillAsync(int warriorId, Skill skill)
    {
        await _db.Initialization;
        var learned = new WarriorSkill { WarriorId = warriorId, Item = skill };
        var entity = learned.ToEntity();
        await _db.Connection.InsertAsync(entity);
        learned.Id = entity.Id;
        return learned;
    }

    public async Task RemoveWarriorSkillAsync(int warriorSkillId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarriorSkillEntity>(warriorSkillId);
    }

    public async Task<WarriorInjury> AddWarriorInjuryAsync(int warriorId, Injury injury)
    {
        await _db.Initialization;
        var tracked = new WarriorInjury { WarriorId = warriorId, Item = injury };
        var entity = tracked.ToEntity();
        await _db.Connection.InsertAsync(entity);
        tracked.Id = entity.Id;
        return tracked;
    }

    public async Task RemoveWarriorInjuryAsync(int warriorInjuryId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarriorInjuryEntity>(warriorInjuryId);
    }

    public async Task<WarriorSpell> AddWarriorSpellAsync(int warriorId, Spell spell)
    {
        await _db.Initialization;
        var learned = new WarriorSpell { WarriorId = warriorId, Item = spell };
        var entity = learned.ToEntity();
        await _db.Connection.InsertAsync(entity);
        learned.Id = entity.Id;
        return learned;
    }

    public async Task RemoveWarriorSpellAsync(int warriorSpellId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarriorSpellEntity>(warriorSpellId);
    }

    public async Task<WarriorMutation> AddWarriorMutationAsync(int warriorId, Mutation mutation)
    {
        await _db.Initialization;
        var bought = new WarriorMutation { WarriorId = warriorId, Item = mutation };
        var entity = bought.ToEntity();
        await _db.Connection.InsertAsync(entity);
        bought.Id = entity.Id;
        return bought;
    }

    public async Task RemoveWarriorMutationAsync(int warriorMutationId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarriorMutationEntity>(warriorMutationId);
    }

    public async Task<List<HistoryEntry>> GetHistoryEntriesAsync(int warbandId)
    {
        await _db.Initialization;
        var rows = await _db.Connection.Table<HistoryEntryEntity>()
            .Where(h => h.WarbandId == warbandId)
            .OrderByDescending(h => h.Date)
            .ToListAsync();
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task AddHistoryEntryAsync(int warbandId, string text)
    {
        await _db.Initialization;
        var entry = new HistoryEntry { WarbandId = warbandId, Date = DateTime.Now, Text = text };
        await _db.Connection.InsertAsync(entry.ToEntity());
    }
}
