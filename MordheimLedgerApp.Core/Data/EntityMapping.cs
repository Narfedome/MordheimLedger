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

    public static WarbandArchetype ToModel(this WarbandArchetypeEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Source = e.Source,
        StartingTreasury = e.StartingTreasury,
        MaxWarriors = e.MaxWarriors,
        Description = e.Description,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static WarbandArchetypeEntity ToEntity(this WarbandArchetype m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Source = m.Source,
        StartingTreasury = m.StartingTreasury,
        MaxWarriors = m.MaxWarriors,
        Description = m.Description,
        ImagePath = m.ImagePath
    };

    public static WarriorArchetype ToModel(this WarriorArchetypeEntity e) => new()
    {
        Id = e.Id,
        WarbandArchetypeId = e.WarbandArchetypeId,
        Name = e.Name,
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
        Description = e.Description,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static WarriorArchetypeEntity ToEntity(this WarriorArchetype m) => new()
    {
        Id = m.Id,
        WarbandArchetypeId = m.WarbandArchetypeId,
        Name = m.Name,
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
        Description = m.Description,
        ImagePath = m.ImagePath
    };

    /// <summary>Seeds a newly recruited Warrior's copyable fields from its archetype (name, cost, stat line).</summary>
    public static Warrior ToWarrior(this WarriorArchetype archetype, string name) => new()
    {
        WarriorArchetypeId = archetype.Id,
        Name = name,
        IsHero = archetype.IsHero,
        Cost = archetype.Cost,
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

    public static EquipmentItem ToModel(this EquipmentItemEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Category = e.Category,
        Cost = e.Cost,
        Rarity = e.Rarity,
        Description = e.Description,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty
    };

    public static EquipmentItemEntity ToEntity(this EquipmentItem m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Category = m.Category,
        Cost = m.Cost,
        Rarity = m.Rarity,
        Description = m.Description,
        Source = m.Source,
        ImagePath = m.ImagePath
    };

    /// <param name="equipment">Carried items, loaded separately via the join table (sqlite-net does no joins).</param>
    public static Warrior ToModel(this WarriorEntity e, IEnumerable<WarriorEquipment>? equipment = null) => new()
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
        Notes = e.Notes,
        Equipment = equipment?.ToList() ?? new List<WarriorEquipment>()
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
        Leadership = m.Leadership,
        Notes = m.Notes
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
}
