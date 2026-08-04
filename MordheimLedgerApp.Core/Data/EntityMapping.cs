using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// Entity &lt;-&gt; model conversions, centralized: a field added to a model only needs mapping here
/// (see DmTools' EntityMapping for the rationale — duplicated mapping blocks in data services risk
/// silent omissions on new fields), and the round-trip is covered by unit tests.
/// </summary>
public static class EntityMapping
{
    /// <summary>Resolves a translation key against an already-fetched (Key, LanguageCode) → Value
    /// dictionary for the requested language (see LibraryService.ResolveTranslationsAsync) - falls
    /// back to the raw key itself (visible placeholder rather than blank) if nothing was resolved.</summary>
    private static string ResolveName(string key, IReadOnlyDictionary<string, string> translations) =>
        translations.GetValueOrDefault(key, key);

    private static string? ResolveDescription(string? key, IReadOnlyDictionary<string, string> translations) =>
        key is null ? null : translations.GetValueOrDefault(key, key);


    public static Warband ToModel(this WarbandEntity e) => new()
    {
        Id = e.Id,
        CampaignId = e.CampaignId,
        WarbandArchetypeId = e.WarbandArchetypeId,
        Name = e.Name,
        Treasury = e.Treasury,
        Notes = e.Notes
    };

    public static WarbandEntity ToEntity(this Warband m) => new()
    {
        Id = m.Id,
        CampaignId = m.CampaignId,
        WarbandArchetypeId = m.WarbandArchetypeId,
        Name = m.Name,
        Treasury = m.Treasury,
        Notes = m.Notes
    };

    public static WarbandArchetype ToModel(this WarbandArchetypeEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Source = e.Source,
        StartingTreasury = e.StartingTreasury,
        MaxWarriors = e.MaxWarriors,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static WarbandArchetypeEntity ToEntity(this WarbandArchetype m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        Source = m.Source,
        StartingTreasury = m.StartingTreasury,
        MaxWarriors = m.MaxWarriors,
        DescriptionKey = m.DescriptionKey,
        ImagePath = m.ImagePath
    };

    public static WarriorArchetype ToModel(this WarriorArchetypeEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        WarbandArchetypeId = e.WarbandArchetypeId,
        Name = ResolveName(e.NameKey, translations),
        IsHero = e.IsHero,
        Cost = e.Cost,
        Source = e.Source,
        MaxCount = e.MaxCount,
        Movement = e.Movement,
        WeaponSkill = e.WeaponSkill,
        BallisticSkill = e.BallisticSkill,
        Strength = e.Strength,
        Toughness = e.Toughness,
        Wounds = e.Wounds,
        Initiative = e.Initiative,
        Attacks = e.Attacks,
        Leadership = e.Leadership,
        StartingExperience = e.StartingExperience,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static WarriorArchetypeEntity ToEntity(this WarriorArchetype m) => new()
    {
        Id = m.Id,
        WarbandArchetypeId = m.WarbandArchetypeId,
        NameKey = m.NameKey ?? string.Empty,
        IsHero = m.IsHero,
        Cost = m.Cost,
        Source = m.Source,
        MaxCount = m.MaxCount,
        Movement = m.Movement,
        WeaponSkill = m.WeaponSkill,
        BallisticSkill = m.BallisticSkill,
        Strength = m.Strength,
        Toughness = m.Toughness,
        Wounds = m.Wounds,
        Initiative = m.Initiative,
        Attacks = m.Attacks,
        Leadership = m.Leadership,
        StartingExperience = m.StartingExperience,
        DescriptionKey = m.DescriptionKey,
        ImagePath = m.ImagePath
    };

    /// <summary>Seeds a newly recruited Warrior's copyable fields from its archetype (name, cost, stat line, starting XP).</summary>
    public static Warrior ToWarrior(this WarriorArchetype archetype, string name) => new()
    {
        WarriorArchetypeId = archetype.Id,
        Name = name,
        IsHero = archetype.IsHero,
        Cost = archetype.Cost,
        Experience = archetype.StartingExperience,
        Movement = archetype.Movement,
        WeaponSkill = archetype.WeaponSkill,
        BallisticSkill = archetype.BallisticSkill,
        Strength = archetype.Strength,
        Toughness = archetype.Toughness,
        Wounds = archetype.Wounds,
        Initiative = archetype.Initiative,
        Attacks = archetype.Attacks,
        Leadership = archetype.Leadership
    };

    public static Campaign ToModel(this CampaignEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Notes = e.Notes
    };

    public static CampaignEntity ToEntity(this Campaign m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Notes = m.Notes
    };

    public static Skill ToModel(this SkillEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Category = e.Category,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static SkillEntity ToEntity(this Skill m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        Category = m.Category,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath
    };

    public static Injury ToModel(this InjuryEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static InjuryEntity ToEntity(this Injury m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath
    };

    public static EquipmentItem ToModel(this EquipmentItemEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Category = e.Category,
        Cost = e.Cost,
        Rarity = e.Rarity,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static EquipmentItemEntity ToEntity(this EquipmentItem m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        Category = m.Category,
        Cost = m.Cost,
        Rarity = m.Rarity,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath
    };

    /// <param name="equipment">Carried items, loaded separately via the join table (sqlite-net does no joins).</param>
    /// <param name="skills">Learned skills/spells, loaded separately via the join table.</param>
    /// <param name="injuries">Tracked injuries, loaded separately via the join table.</param>
    public static Warrior ToModel(this WarriorEntity e, IEnumerable<WarriorEquipment>? equipment = null, IEnumerable<WarriorSkill>? skills = null, IEnumerable<WarriorInjury>? injuries = null) => new()
    {
        Id = e.Id,
        WarbandId = e.WarbandId,
        WarriorArchetypeId = e.WarriorArchetypeId,
        Name = e.Name,
        IsHero = e.IsHero,
        Cost = e.Cost,
        Experience = e.Experience,
        Status = e.Status,
        Movement = e.Movement,
        WeaponSkill = e.WeaponSkill,
        BallisticSkill = e.BallisticSkill,
        Strength = e.Strength,
        Toughness = e.Toughness,
        Wounds = e.Wounds,
        Initiative = e.Initiative,
        Attacks = e.Attacks,
        Leadership = e.Leadership,
        Equipment = equipment?.ToList() ?? new List<WarriorEquipment>(),
        Skills = skills?.ToList() ?? new List<WarriorSkill>(),
        Injuries = injuries?.ToList() ?? new List<WarriorInjury>()
    };

    public static WarriorEntity ToEntity(this Warrior m) => new()
    {
        Id = m.Id,
        WarbandId = m.WarbandId,
        WarriorArchetypeId = m.WarriorArchetypeId,
        Name = m.Name,
        IsHero = m.IsHero,
        Cost = m.Cost,
        Experience = m.Experience,
        Status = m.Status,
        Movement = m.Movement,
        WeaponSkill = m.WeaponSkill,
        BallisticSkill = m.BallisticSkill,
        Strength = m.Strength,
        Toughness = m.Toughness,
        Wounds = m.Wounds,
        Initiative = m.Initiative,
        Attacks = m.Attacks,
        Leadership = m.Leadership
    };

    /// <param name="item">The catalog item this row references, loaded separately.</param>
    public static WarriorEquipment ToModel(this WarriorEquipmentEntity e, EquipmentItem item) => new()
    {
        Id = e.Id,
        WarriorId = e.WarriorId,
        Item = item,
        Quantity = e.Quantity
    };

    public static WarriorEquipmentEntity ToEntity(this WarriorEquipment m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        EquipmentItemId = m.Item.Id,
        Quantity = m.Quantity
    };

    /// <param name="item">The catalog skill this row references, loaded separately.</param>
    public static WarriorSkill ToModel(this WarriorSkillEntity e, Skill item) => new()
    {
        Id = e.Id,
        WarriorId = e.WarriorId,
        Item = item
    };

    public static WarriorSkillEntity ToEntity(this WarriorSkill m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        SkillId = m.Item.Id
    };

    /// <param name="item">The catalog injury this row references, loaded separately.</param>
    public static WarriorInjury ToModel(this WarriorInjuryEntity e, Injury item) => new()
    {
        Id = e.Id,
        WarriorId = e.WarriorId,
        Item = item
    };

    public static WarriorInjuryEntity ToEntity(this WarriorInjury m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        InjuryId = m.Item.Id
    };

    public static HistoryEntry ToModel(this HistoryEntryEntity e) => new()
    {
        Id = e.Id,
        WarbandId = e.WarbandId,
        Date = e.Date,
        Text = e.Text
    };

    public static HistoryEntryEntity ToEntity(this HistoryEntry m) => new()
    {
        Id = m.Id,
        WarbandId = m.WarbandId,
        Date = m.Date,
        Text = m.Text
    };
}
