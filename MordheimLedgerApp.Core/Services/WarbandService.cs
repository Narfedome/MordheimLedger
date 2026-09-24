using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

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
        var row = (await _db.CachedTableAsync<WarbandArchetypeEntity>()).First(a => a.Id == id);

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
            .Where(w => w.WarbandId == warbandId && w.Status != WarriorStatus.Dead && w.Status != WarriorStatus.Retired)
            .ToListAsync();
        return warriors.Sum(w => WarbandRatingRules.WarriorContribution(w.IsLargeCreature, w.Experience, w.HeadCount, w.HiredSwordBaseRating, w.DramatisPersonaRatingBonus));
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
        // Idem pour Injury depuis que Madness (24)/etc peuvent porter des SpecialRules (Stupidité/
        // Frénésie) - le FindAsync+ToModel minimal ci-dessous les aurait laissées vides, même trou que
        // celui déjà corrigé pour Equipment/Skill/Mutation (voir le commentaire ci-dessus).
        var injuryById = (await _library.GetInjuriesAsync(languageCode)).ToDictionary(i => i.Id);

        // Chaque table portée chargée UNE fois pour tous les guerriers de la bande (WHERE WarriorId IN
        // (...)) puis regroupée en mémoire, plutôt que 6 requêtes par guerrier + 2 FindAsync/traductions
        // par objet porté (N+1 repéré le 2026-09-24 - la fiche de bande et le wizard Fin de Partie
        // appellent tous deux cette méthode à l'ouverture).
        var warriorIds = warriorRows.Select(r => r.Id).ToList();
        var carriedByWarrior = (await _db.Connection.Table<WarriorEquipmentEntity>().Where(e => warriorIds.Contains(e.WarriorId)).ToListAsync()).ToLookup(e => e.WarriorId);
        var learnedByWarrior = (await _db.Connection.Table<WarriorSkillEntity>().Where(s => warriorIds.Contains(s.WarriorId)).ToListAsync()).ToLookup(s => s.WarriorId);
        var injuriesByWarrior = (await _db.Connection.Table<WarriorInjuryEntity>().Where(i => warriorIds.Contains(i.WarriorId)).ToListAsync()).ToLookup(i => i.WarriorId);
        var spellRowsByWarrior = (await _db.Connection.Table<WarriorSpellEntity>().Where(s => warriorIds.Contains(s.WarriorId)).ToListAsync()).ToLookup(s => s.WarriorId);
        var mutationsByWarrior = (await _db.Connection.Table<WarriorMutationEntity>().Where(m => warriorIds.Contains(m.WarriorId)).ToListAsync()).ToLookup(m => m.WarriorId);
        var hatredsByWarrior = (await _db.Connection.Table<WarriorHatredEntity>().Where(h => warriorIds.Contains(h.WarriorId)).ToListAsync()).ToLookup(h => h.WarriorId);

        var ruleIds = carriedByWarrior.SelectMany(g => g)
            .SelectMany(c => new[] { c.MaterialSpecialRuleId, c.BlessingSpecialRuleId });
        var rulesById = await ResolveSpecialRulesAsync(ruleIds, languageCode);
        var spellsById = await ResolveSpellsAsync(spellRowsByWarrior.SelectMany(g => g).Select(s => s.SpellId), languageCode);
        var hatredArchetypeNames = await ResolveWarbandArchetypeNamesAsync(
            hatredsByWarrior.SelectMany(g => g).Select(h => h.TargetWarbandArchetypeId), languageCode);

        var warriors = new List<Warrior>();
        foreach (var row in warriorRows)
        {
            var carried = new List<WarriorEquipment>();
            foreach (var carriedRow in carriedByWarrior[row.Id])
            {
                if (!equipmentById.TryGetValue(carriedRow.EquipmentItemId, out var item)) continue;
                carried.Add(carriedRow.ToModel(item, LookupRule(rulesById, carriedRow.MaterialSpecialRuleId), LookupRule(rulesById, carriedRow.BlessingSpecialRuleId)));
            }

            var learned = new List<WarriorSkill>();
            foreach (var learnedRow in learnedByWarrior[row.Id])
                if (skillById.TryGetValue(learnedRow.SkillId, out var skill))
                    learned.Add(learnedRow.ToModel(skill));

            var injuries = new List<WarriorInjury>();
            foreach (var injuryRow in injuriesByWarrior[row.Id])
                if (injuryById.TryGetValue(injuryRow.InjuryId, out var injury))
                    injuries.Add(injuryRow.ToModel(injury));

            var spells = new List<WarriorSpell>();
            foreach (var spellRow in spellRowsByWarrior[row.Id])
                if (spellsById.TryGetValue(spellRow.SpellId, out var spell))
                    spells.Add(spellRow.ToModel(spell));

            var mutations = new List<WarriorMutation>();
            foreach (var mutationRow in mutationsByWarrior[row.Id])
                if (mutationById.TryGetValue(mutationRow.MutationId, out var mutation))
                    mutations.Add(mutationRow.ToModel(mutation));

            EquipmentItem? animal = null;
            if (row.AnimalId is { } animalId)
                equipmentById.TryGetValue(animalId, out animal);

            // Nom de la cible de Haine - voir Models.WarriorHatred.Name : TargetWarbandArchetypeId passe
            // par une traduction, TargetFreeText est déjà le nom affiché.
            var hatreds = new List<WarriorHatred>();
            foreach (var hatredRow in hatredsByWarrior[row.Id])
            {
                var name = hatredRow.TargetWarbandArchetypeId is { } archetypeId
                    ? hatredArchetypeNames.GetValueOrDefault(archetypeId, string.Empty)
                    : hatredRow.TargetFreeText ?? string.Empty;
                hatreds.Add(hatredRow.ToModel(name));
            }

            warriors.Add(row.ToModel(carried, learned, injuries, spells, mutations, animal, hatreds));
        }
        return warriors;
    }

    private static SpecialRule? LookupRule(IReadOnlyDictionary<int, SpecialRule> rulesById, int? id) =>
        id is { } ruleId ? rulesById.GetValueOrDefault(ruleId) : null;

    /// <summary>Version groupée de ResolveSpecialRuleAsync : une requête IN + une résolution de
    /// traductions pour tout l'ensemble d'ids, quel que soit leur nombre.</summary>
    private async Task<Dictionary<int, SpecialRule>> ResolveSpecialRulesAsync(IEnumerable<int?> ids, string languageCode)
    {
        var idList = ids.OfType<int>().Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, SpecialRule>();

        var entities = (await _db.CachedTableAsync<SpecialRuleEntity>()).Where(r => idList.Contains(r.Id)).ToList();
        var translations = await TranslationResolver.ResolveAsync(_db, entities.SelectMany(e => new[] { e.NameKey, e.DescriptionKey }), languageCode);
        return entities.ToDictionary(e => e.Id, e => e.ToModel(translations));
    }

    private async Task<Dictionary<int, Spell>> ResolveSpellsAsync(IEnumerable<int> ids, string languageCode)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, Spell>();

        var entities = (await _db.CachedTableAsync<SpellEntity>()).Where(s => idList.Contains(s.Id)).ToList();
        var translations = await TranslationResolver.ResolveAsync(_db, entities.SelectMany(e => new[] { e.NameKey, e.DescriptionKey }), languageCode);
        return entities.ToDictionary(e => e.Id, e => e.ToModel(translations));
    }

    private async Task<Dictionary<int, string>> ResolveWarbandArchetypeNamesAsync(IEnumerable<int?> ids, string languageCode)
    {
        var idList = ids.OfType<int>().Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, string>();

        var entities = (await _db.CachedTableAsync<WarbandArchetypeEntity>()).Where(a => idList.Contains(a.Id)).ToList();
        var translations = await TranslationResolver.ResolveAsync(_db, entities.Select(e => e.NameKey), languageCode);
        return entities.ToDictionary(e => e.Id, e => translations[e.NameKey]);
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

    public async Task<Warrior> RecruitHiredSwordAsync(int warbandId, HiredSword hiredSword, string name, IReadOnlyList<EquipmentItem> startingEquipment)
    {
        await _db.Initialization;
        var warrior = hiredSword.ToWarrior(name);
        warrior.WarbandId = warbandId;
        var entity = warrior.ToEntity();
        await _db.Connection.InsertAsync(entity);
        warrior.Id = entity.Id;

        foreach (var item in startingEquipment)
            await AddWarriorEquipmentAsync(warrior.Id, item);

        return warrior;
    }

    public async Task<Warrior> RecruitDramatisPersonaAsync(int warbandId, DramatisPersona dramatisPersona, string name, IReadOnlyList<EquipmentItem> startingEquipment, IReadOnlyList<Skill> startingSkills)
    {
        await _db.Initialization;
        var warrior = dramatisPersona.ToWarrior(name);
        warrior.WarbandId = warbandId;
        var entity = warrior.ToEntity();
        await _db.Connection.InsertAsync(entity);
        warrior.Id = entity.Id;

        // Groupé par objet (ex. Bertha : deux Marteaux de Guerre Sigmarites, voir
        // DramatisPersonae.json.startingEquipmentNames) - une seule WarriorEquipment row par objet distinct,
        // Quantity = nombre d'occurrences, plutôt qu'une row par occurrence. La liste appelante (résolue
        // depuis DramatisPersona.StartingEquipmentIds, qui garde les doublons) contiendrait sinon le même
        // Id plusieurs fois - insérer une row par occurrence créerait autant de puces identiques sur la
        // carte au lieu d'une seule "x2" (WarriorEquipment.NameDisplay, retour utilisateur 2026-09-01).
        foreach (var group in startingEquipment.GroupBy(item => item.Id))
            await AddWarriorEquipmentAsync(warrior.Id, group.First(), quantity: group.Count());
        foreach (var skill in startingSkills)
            await AddWarriorSkillAsync(warrior.Id, skill);

        return warrior;
    }

    /// <summary>DramatisPersona ids currently "on cooldown" for this warband - see Models.Library.
    /// DramatisPersona.RequiresCooldownBeforeResearch's own doc. Consumed by the "Personnage spécial"
    /// search picker (excludes these) and by WarbandDetailViewModel.EndOfGame (clears/sets rows at Apply
    /// time).</summary>
    public async Task<List<int>> GetDramatisPersonaCooldownIdsAsync(int warbandId)
    {
        await _db.Initialization;
        return (await _db.Connection.Table<WarbandDramatisPersonaCooldownEntity>().Where(r => r.WarbandId == warbandId).ToListAsync())
            .Select(r => r.DramatisPersonaId)
            .ToList();
    }

    /// <summary>Marks a persona as on cooldown for this warband - called when a RequiresCooldownBeforeResearch
    /// persona departs (ApplyWandererDeparturesAsync). No dedup guard: a persona can only depart once per
    /// warband at a time (they're either on the roster or not), so a duplicate row can't happen in
    /// practice.</summary>
    public async Task AddDramatisPersonaCooldownAsync(int warbandId, int dramatisPersonaId)
    {
        await _db.Initialization;
        await _db.Connection.InsertAsync(new WarbandDramatisPersonaCooldownEntity { WarbandId = warbandId, DramatisPersonaId = dramatisPersonaId });
    }

    /// <summary>Clears every cooldown row for this warband - called at the START of ApplyRareItemSearchAsync's
    /// processing (before any NEW departure this same End of Game could add a fresh one): reaching this End
    /// of Game at all already means "the warband fought a battle" since the persona was excluded from being
    /// re-sought this session (picker-side filter), so whatever was on cooldown going into this wizard has
    /// now satisfied "at least one battle without them".</summary>
    public async Task ClearAllDramatisPersonaCooldownsAsync(int warbandId)
    {
        await _db.Initialization;
        await _db.Connection.ExecuteAsync("DELETE FROM WarbandDramatisPersonaCooldownEntity WHERE WarbandId = ?", warbandId);
    }

    public async Task InsertWarriorAsync(Warrior warrior)
    {
        await _db.Initialization;
        var entity = warrior.ToEntity();
        await _db.Connection.InsertAsync(entity);
        warrior.Id = entity.Id;
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
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorHatredEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorSpellEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorMutationEntity WHERE WarriorId = ?", warriorId);
        await _db.Connection.DeleteAsync<WarriorEntity>(warriorId);
    }

    public async Task<WarriorEquipment> AddWarriorEquipmentAsync(int warriorId, EquipmentItem item, int quantity = 1, SpecialRule? materialRule = null, int? foundValueOverride = null)
    {
        await _db.Initialization;
        var carried = new WarriorEquipment { WarriorId = warriorId, Item = item, Quantity = quantity, MaterialRule = materialRule, FoundValueOverride = foundValueOverride };
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

    public async Task SetWarriorEquipmentBlessingRuleAsync(int warriorEquipmentId, int? blessingSpecialRuleId)
    {
        await _db.Initialization;
        var entity = await _db.Connection.FindAsync<WarriorEquipmentEntity>(warriorEquipmentId);
        if (entity is null) return;
        entity.BlessingSpecialRuleId = blessingSpecialRuleId;
        await _db.Connection.UpdateAsync(entity);
    }

    /// <summary>Resolves a plain SpecialRule id to a localized model - shared by MaterialRule and
    /// BlessingRule resolution (both are just "an optional SpecialRule attached to a carried/stashed
    /// item"), despite the name predating BlessingRule.</summary>
    private async Task<SpecialRule?> ResolveSpecialRuleAsync(int? specialRuleId, string languageCode)
    {
        if (specialRuleId is not { } id) return null;
        var entity = (await _db.CachedTableAsync<SpecialRuleEntity>()).FirstOrDefault(r => r.Id == id);
        if (entity is null) return null;

        var translations = await TranslationResolver.ResolveAsync(_db, [entity.NameKey, entity.DescriptionKey], languageCode);
        return entity.ToModel(translations);
    }

    public async Task<List<WarbandEquipment>> GetWarbandEquipmentAsync(int warbandId, string languageCode)
    {
        await _db.Initialization;
        var equipmentById = (await _library.GetEquipmentItemsAsync(languageCode)).ToDictionary(i => i.Id);
        var rows = await _db.Connection.Table<WarbandEquipmentEntity>().Where(e => e.WarbandId == warbandId).ToListAsync();
        var rulesById = await ResolveSpecialRulesAsync(rows.Select(r => r.MaterialSpecialRuleId), languageCode);

        var result = new List<WarbandEquipment>();
        foreach (var row in rows)
        {
            if (!equipmentById.TryGetValue(row.EquipmentItemId, out var item)) continue;
            result.Add(row.ToModel(item, LookupRule(rulesById, row.MaterialSpecialRuleId)));
        }
        return result;
    }

    public async Task<WarbandEquipment> AddWarbandEquipmentAsync(int warbandId, EquipmentItem item, int quantity = 1, SpecialRule? materialRule = null, int? foundValueOverride = null)
    {
        await _db.Initialization;
        var stashed = new WarbandEquipment { WarbandId = warbandId, Item = item, Quantity = quantity, MaterialRule = materialRule, FoundValueOverride = foundValueOverride };
        var entity = stashed.ToEntity();
        await _db.Connection.InsertAsync(entity);
        stashed.Id = entity.Id;
        return stashed;
    }

    public async Task RemoveWarbandEquipmentAsync(int warbandEquipmentId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarbandEquipmentEntity>(warbandEquipmentId);
    }

    public async Task<int> SellWarbandItemAsync(int warbandEquipmentId)
    {
        await _db.Initialization;
        var stashRow = await _db.Connection.FindAsync<WarbandEquipmentEntity>(warbandEquipmentId)
            ?? throw new InvalidOperationException($"WarbandEquipment {warbandEquipmentId} introuvable.");

        // "en" suffit ici : seuls Cost/CostMultiplier comptent, même idiome que EquipWarbandItemToWarriorAsync.
        var materialRule = await ResolveSpecialRuleAsync(stashRow.MaterialSpecialRuleId, "en");
        var item = (await _library.GetEquipmentItemsAsync("en")).First(i => i.Id == stashRow.EquipmentItemId);
        // Vendable soit par le matériau (Ornate Weapon...), soit par l'objet lui-même (les gemmes du
        // Bijoutier - voir Models.WarbandEquipment.IsSellable, même distinction).
        if (materialRule?.IsResaleUpgrade != true && !item.IsSellable)
            throw new InvalidOperationException($"WarbandEquipment {warbandEquipmentId} n'est pas vendable.");

        // FoundValueOverride (ex. gemmes du Bijoutier à valeur aléatoire, voir WarbandEquipment.
        // FoundValueOverride) prime quand renseigné - la valeur a déjà été fixée au moment de la
        // trouvaille. Sinon même formule que l'achat (Core.Rules.EquipmentPricing.CalculateCost) - "vaut
        // le double du prix normal à la revente" est exactement CostMultiplier appliqué au Cost de base
        // (×1, donc Cost tel quel, quand il n'y a pas de matériau) - pas un champ à part.
        var gold = (stashRow.FoundValueOverride ?? EquipmentPricing.CalculateCost(item.Cost, materialRule?.CostMultiplier, isFree: false)) * stashRow.Quantity;

        var warband = await GetWarbandAsync(stashRow.WarbandId)
            ?? throw new InvalidOperationException($"Warband {stashRow.WarbandId} introuvable.");
        warband.Treasury += gold;
        await SaveWarbandAsync(warband);

        await _db.Connection.DeleteAsync<WarbandEquipmentEntity>(warbandEquipmentId);
        return gold;
    }

    public async Task<WarriorEquipment> EquipWarbandItemToWarriorAsync(int warbandEquipmentId, int warriorId)
    {
        await _db.Initialization;
        var stashRow = await _db.Connection.FindAsync<WarbandEquipmentEntity>(warbandEquipmentId)
            ?? throw new InvalidOperationException($"WarbandEquipment {warbandEquipmentId} introuvable.");

        // "en" suffit ici : seul l'Id compte pour AddWarriorEquipmentAsync (voir WarriorEquipment.ToEntity),
        // même idiome que la résolution par nom anglais de l'étape Exploration du wizard.
        var item = (await _library.GetEquipmentItemsAsync("en")).First(i => i.Id == stashRow.EquipmentItemId);
        var materialRule = await ResolveSpecialRuleAsync(stashRow.MaterialSpecialRuleId, "en");

        var carried = await AddWarriorEquipmentAsync(warriorId, item, stashRow.Quantity, materialRule, stashRow.FoundValueOverride);
        await _db.Connection.DeleteAsync<WarbandEquipmentEntity>(warbandEquipmentId);
        return carried;
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

    public async Task<WarriorInjury> AddWarriorInjuryAsync(int warriorId, Injury injury, bool isTemporary = false)
    {
        await _db.Initialization;
        var tracked = new WarriorInjury { WarriorId = warriorId, Item = injury, IsTemporary = isTemporary };
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

    public async Task RemoveTemporaryInjuriesAsync(int warriorId)
    {
        await _db.Initialization;
        await _db.Connection.ExecuteAsync("DELETE FROM WarriorInjuryEntity WHERE WarriorId = ? AND IsTemporary = 1", warriorId);
    }

    public async Task<WarriorHatred> AddWarriorHatredAsync(int warriorId, int? targetWarbandArchetypeId, string? targetFreeText)
    {
        await _db.Initialization;
        var tracked = new WarriorHatred
        {
            WarriorId = warriorId,
            TargetWarbandArchetypeId = targetWarbandArchetypeId,
            TargetFreeText = targetFreeText
        };
        var entity = tracked.ToEntity();
        await _db.Connection.InsertAsync(entity);
        tracked.Id = entity.Id;
        return tracked;
    }

    public async Task RemoveWarriorHatredAsync(int warriorHatredId)
    {
        await _db.Initialization;
        await _db.Connection.DeleteAsync<WarriorHatredEntity>(warriorHatredId);
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
