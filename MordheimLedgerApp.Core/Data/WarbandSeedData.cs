namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// JSON shape for a warband's seed data (Data/SeedData/*.json, embedded resources) - the scalable
/// replacement for OfficialContentSeed.cs's hand-written C# object initializers, which don't scale
/// past a single warband. AppDatabase.SeedOfficialContentAsync() deserializes these and writes both
/// language values via the same SeedTranslationAsync key-allocation helper used for Reiklander.
/// </summary>
public class LocalizedText
{
    public string En { get; set; } = string.Empty;
    public string? Fr { get; set; }
}

public class WarbandSeedData
{
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Description { get; set; }
    public int StartingTreasury { get; set; }
    public int? MaxWarriors { get; set; }
    public List<WarriorSeedData> Warriors { get; set; } = new();

    /// <summary>Equipment introduced by this warband (beyond the shared CoreEquipment) - typically
    /// warband-specific rare items, see EquipmentSeedData.RestrictedToThisWarband.</summary>
    public List<EquipmentSeedData> Equipment { get; set; } = new();

    /// <summary>All entries of every spell/prayer/ritual table this warband uses (empty for non-casting
    /// warbands like the dwarfs) - see WarriorSeedData.SpellListName for the link to the caster archetype.</summary>
    public List<SpellSeedData> Spells { get; set; } = new();
}

public class WarriorSeedData
{
    public LocalizedText Name { get; set; } = new();
    public bool IsHero { get; set; }
    public int Cost { get; set; }
    public int? MaxCount { get; set; }
    public int StartingExperience { get; set; }
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }
    public LocalizedText? Description { get; set; }

    /// <summary>Non-null = this archetype rolls on the Spell entries with a matching SpellListName
    /// (see WarbandSeedData.Spells).</summary>
    public string? SpellListName { get; set; }
}

public class EquipmentSeedData
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.EquipmentCategory member name.</summary>
    public string Category { get; set; } = string.Empty;

    public int Cost { get; set; }
    public int? Rarity { get; set; }
    public LocalizedText? Description { get; set; }

    /// <summary>True = only this warband can buy/carry it (see WarbandArchetypeEquipmentEntity).</summary>
    public bool RestrictedToThisWarband { get; set; }
}

public class SpellSeedData
{
    public string SpellListName { get; set; } = string.Empty;
    public int RollValue { get; set; }
    public int? Difficulty { get; set; }
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Description { get; set; }
}
