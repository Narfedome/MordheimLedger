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

    public static WarbandArchetype ToModel(this WarbandArchetypeEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByWarbandId = null,
        IReadOnlyDictionary<int, List<MagicSchool>>? magicSchoolsByWarbandId = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Source = e.Source,
        StartingTreasury = e.StartingTreasury,
        MaxWarriors = e.MaxWarriors,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        ImagePath = e.ImagePath ?? string.Empty,
        SpecialRules = specialRulesByWarbandId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>(),
        MagicSchools = magicSchoolsByWarbandId?.GetValueOrDefault(e.Id) ?? new List<MagicSchool>()
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

    public static WarriorArchetype ToModel(this WarriorArchetypeEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByWarriorArchetypeId = null) => new()
    {
        Id = e.Id,
        WarbandArchetypeId = e.WarbandArchetypeId,
        Name = ResolveName(e.NameKey, translations),
        IsHero = e.IsHero,
        Cost = e.Cost,
        Source = e.Source,
        MaxCount = e.MaxCount,
        Movement = e.Movement,
        MovementOverride = e.MovementOverride,
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
        IsSpellcaster = e.IsSpellcaster,
        CanBuyMutations = e.CanBuyMutations,
        ImagePath = e.ImagePath ?? string.Empty,
        SpecialRules = specialRulesByWarriorArchetypeId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>()
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
        MovementOverride = m.MovementOverride,
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
        IsSpellcaster = m.IsSpellcaster,
        CanBuyMutations = m.CanBuyMutations,
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
        MovementOverride = archetype.MovementOverride,
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

    public static Skill ToModel(this SkillEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<int>>? restrictions = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Category = e.Category,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        RestrictedToWarbandArchetypeIds = restrictions?.GetValueOrDefault(e.Id) ?? new List<int>()
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

    public static SpecialRule ToModel(this SpecialRuleEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static SpecialRuleEntity ToEntity(this SpecialRule m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath
    };

    public static EquipmentItem ToModel(this EquipmentItemEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<int>>? restrictions = null) => new()
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
        ImagePath = e.ImagePath ?? string.Empty,
        RestrictedToWarbandArchetypeIds = restrictions?.GetValueOrDefault(e.Id) ?? new List<int>()
    };

    public static Spell ToModel(this SpellEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, MagicSchool>? magicSchoolsById = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        MagicSchoolId = e.MagicSchoolId,
        MagicSchool = magicSchoolsById?.GetValueOrDefault(e.MagicSchoolId),
        RollValue = e.RollValue,
        Difficulty = e.Difficulty,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static SpellEntity ToEntity(this Spell m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        MagicSchoolId = m.MagicSchoolId,
        RollValue = m.RollValue,
        Difficulty = m.Difficulty,
        Source = m.Source,
        ImagePath = m.ImagePath
    };

    public static MagicSchool ToModel(this MagicSchoolEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static MagicSchoolEntity ToEntity(this MagicSchool m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath
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
    /// <param name="skills">Learned skills, loaded separately via the join table.</param>
    /// <param name="injuries">Tracked injuries, loaded separately via the join table.</param>
    /// <param name="spells">Learned spells, loaded separately via the join table.</param>
    /// <param name="mutations">Bought mutations, loaded separately via the join table.</param>
    /// <param name="mount">Ridden mount, resolved separately from MountEntity - not a join, see WarriorEntity.MountId.</param>
    public static Warrior ToModel(this WarriorEntity e, IEnumerable<WarriorEquipment>? equipment = null, IEnumerable<WarriorSkill>? skills = null,
        IEnumerable<WarriorInjury>? injuries = null, IEnumerable<WarriorSpell>? spells = null, IEnumerable<WarriorMutation>? mutations = null,
        Mount? mount = null) => new()
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
        MovementOverride = e.MovementOverride,
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
        Injuries = injuries?.ToList() ?? new List<WarriorInjury>(),
        Spells = spells?.ToList() ?? new List<WarriorSpell>(),
        Mutations = mutations?.ToList() ?? new List<WarriorMutation>(),
        Mount = mount
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
        MovementOverride = m.MovementOverride,
        WeaponSkill = m.WeaponSkill,
        BallisticSkill = m.BallisticSkill,
        Strength = m.Strength,
        Toughness = m.Toughness,
        Wounds = m.Wounds,
        Initiative = m.Initiative,
        Attacks = m.Attacks,
        Leadership = m.Leadership,
        MountId = m.Mount?.Id
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

    /// <param name="item">The catalog spell this row references, loaded separately.</param>
    public static WarriorSpell ToModel(this WarriorSpellEntity e, Spell item) => new()
    {
        Id = e.Id,
        WarriorId = e.WarriorId,
        Item = item
    };

    public static WarriorSpellEntity ToEntity(this WarriorSpell m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        SpellId = m.Item.Id
    };

    public static Mutation ToModel(this MutationEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<int>>? restrictions = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Cost = e.Cost,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        RestrictedToWarbandArchetypeIds = restrictions?.GetValueOrDefault(e.Id) ?? new List<int>()
    };

    public static MutationEntity ToEntity(this Mutation m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        Cost = m.Cost,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath
    };

    /// <param name="item">The catalog mutation this row references, loaded separately.</param>
    public static WarriorMutation ToModel(this WarriorMutationEntity e, Mutation item) => new()
    {
        Id = e.Id,
        WarriorId = e.WarriorId,
        Item = item
    };

    public static WarriorMutationEntity ToEntity(this WarriorMutation m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        MutationId = m.Item.Id
    };

    public static Mount ToModel(this MountEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<int>>? restrictions = null, IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByMountId = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Cost = e.Cost,
        Rarity = e.Rarity,
        Movement = e.Movement,
        WeaponSkill = e.WeaponSkill,
        BallisticSkill = e.BallisticSkill,
        Strength = e.Strength,
        Toughness = e.Toughness,
        Wounds = e.Wounds,
        Initiative = e.Initiative,
        Attacks = e.Attacks,
        Leadership = e.Leadership,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        RestrictedToWarbandArchetypeIds = restrictions?.GetValueOrDefault(e.Id) ?? new List<int>(),
        SpecialRules = specialRulesByMountId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>()
    };

    public static MountEntity ToEntity(this Mount m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        Cost = m.Cost,
        Rarity = m.Rarity,
        Movement = m.Movement,
        WeaponSkill = m.WeaponSkill,
        BallisticSkill = m.BallisticSkill,
        Strength = m.Strength,
        Toughness = m.Toughness,
        Wounds = m.Wounds,
        Initiative = m.Initiative,
        Attacks = m.Attacks,
        Leadership = m.Leadership,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath
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
