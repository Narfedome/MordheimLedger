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

    /// <summary>Rules that apply to every warrior in the band regardless of type (e.g. "Autonome" for
    /// Ostlanders) - distinct from each WarriorSeedData's own SpecialRules, which only apply to that one
    /// warrior type. See RulesReference/*.md: most warbands split their "Règles Spéciales" this way. Each
    /// entry is find-or-created by its English Name (see AppDatabase.FindOrCreateSpecialRuleAsync) so a
    /// rule like "Leader" reused across many warbands' JSON files resolves to the same catalog row -
    /// keep the English Name identical (verbatim) across files when it's meant to be the same rule, and
    /// keep its Description generic/mechanical (no per-archetype flavor) so it reads correctly wherever
    /// it's attached.</summary>
    public List<SpecialRuleSeedData> SpecialRules { get; set; } = new();

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

    /// <summary>Rules specific to this one warrior type (e.g. "Chef", "Provoque la Peur") - distinct from
    /// the parent WarbandSeedData.SpecialRules, which apply band-wide regardless of warrior type. Same
    /// find-or-create-by-English-Name reuse as WarbandSeedData.SpecialRules.</summary>
    public List<SpecialRuleSeedData> SpecialRules { get; set; } = new();

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

/// <summary>One named, reusable SpecialRule entry (see WarbandSeedData.SpecialRules/WarriorSeedData.
/// SpecialRules) - find-or-created by English Name at seed time so the same rule attached across
/// multiple warbands/warrior types resolves to a single shared catalog row instead of duplicate text.</summary>
public class SpecialRuleSeedData
{
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Description { get; set; }
}
