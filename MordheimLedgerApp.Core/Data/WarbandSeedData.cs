namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// JSON shape for a warband's seed data (Data/SeedData/*.json, embedded resources). Genuinely common
/// data (universal SpecialRules, the generic Equipment/Skill/Mutation pools, MagicSchools + their
/// Spells) lives once in its own file (SpecialRules.json/Equipment.json/Mutations.json/Skills.json/
/// MagicSchools.json), seeded before any warband file - see AppDatabase.SeedOfficialContentAsync().
/// Warband files only declare what's specific to them, referencing the shared catalogs by English Name
/// where relevant (SpecialRules/Mutations/MagicSchools). Each translatable field gets a key via the same
/// SeedTranslationAsync helper.
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

    /// <summary>Official quality/provenance tier (mordheimer.net's Core/1a/1b/1c/2a classification,
    /// the sole reference source retained for this - see CLAUDE.md). Matches
    /// MordheimLedgerApp.Core.Models.Library.WarbandGrade member names exactly (e.g. "Grade1a").</summary>
    public string Grade { get; set; } = nameof(Models.Library.WarbandGrade.Core);

    /// <summary>Rules SPECIFIC to this warband (e.g. "Ancient Enemies" for Kislevites) - genuinely
    /// common rules (Leader, Wizard, Causes Fear, ...) now live once in Data/SeedData/SpecialRules.json,
    /// seeded before any warband file, so they're never redeclared here anymore. Still find-or-created by
    /// English Name (see AppDatabase.FindOrCreateSpecialRuleAsync) for the rare case two warbands
    /// independently need the exact same band-specific rule.</summary>
    public List<SpecialRuleSeedData> SpecialRules { get; set; } = new();

    /// <summary>Magic school(s) this band grants access to (empty = no spellcasting) - name-only
    /// reference into Data/SeedData/MagicSchools.json, which is the sole owner of each school's
    /// Description and Spells (seeded before any warband file). Multiple warbands may reference the same
    /// school name (e.g. Cult of the Possessed and Beastmen Raiders both use "Chaos Rituals") without
    /// either redeclaring its spell table.</summary>
    public List<MagicSchoolSeedData> MagicSchools { get; set; } = new();

    public int StartingTreasury { get; set; }
    public int? MaxWarriors { get; set; }
    public List<WarriorSeedData> Warriors { get; set; } = new();

    /// <summary>Equipment SPECIFIC to this warband (typically rare/restricted items) - the generic common
    /// pool (Dagger, Sword, Mace, ...) now lives once in Data/SeedData/Equipment.json, seeded before any
    /// warband file. See EquipmentSeedData.RestrictedToThisWarband.</summary>
    public List<EquipmentSeedData> Equipment { get; set; } = new();

    /// <summary>Always empty now - every warband's spell table lives in Data/SeedData/MagicSchools.json
    /// instead (see MagicSchools above). Kept for JSON-schema stability rather than removed.</summary>
    public List<SpellSeedData> Spells { get; set; } = new();

    /// <summary>Mutations SPECIFIC to this warband (e.g. Kermesse du Chaos's Nurgle-themed Bénédictions)
    /// - the generic rulebook-wide pool (p.76) now lives once in Data/SeedData/Mutations.json, seeded
    /// before any warband file. Still find-or-created by English Name for the rare case two warbands
    /// independently need the exact same band-specific mutation.</summary>
    public List<MutationSeedData> Mutations { get; set; } = new();

    /// <summary>Mounts introduced by this warband (e.g. "Sanglier de guerre" for Orques) - like
    /// Equipment, typically warband-specific via RestrictedToThisWarband.</summary>
    public List<MountSeedData> Mounts { get; set; } = new();

    /// <summary>Skills SPECIFIC to this warband (its unique "special skill" table, e.g. Orc Mob's
    /// Waaagh!/'Ard 'Ead/...) - the generic common pool (Combat Master, Step Aside, ...) still lives
    /// once in Data/SeedData/Skills.json, seeded before any warband file. See
    /// SkillSeedData.RestrictedToThisWarband/RestrictedToWarriorNames.</summary>
    public List<SkillSeedData> Skills { get; set; } = new();

    /// <summary>This warband's named starting-equipment lists (e.g. "Skaven Heroes Equipment list",
    /// "Marksman Equipment list") - see EquipmentListSeedData. Each WarriorSeedData references one by
    /// name via EquipmentListName. Distinct from Equipment above, which is the Rare/Trading-Post
    /// channel (RestrictedToThisWarband), not tied to any list.</summary>
    public List<EquipmentListSeedData> EquipmentLists { get; set; } = new();
}

public class WarriorSeedData
{
    public LocalizedText Name { get; set; } = new();
    public bool IsHero { get; set; }
    public int Cost { get; set; }
    public int? MaxCount { get; set; }
    public int StartingExperience { get; set; }
    public int Movement { get; set; }

    /// <summary>Non-null overrides the displayed Movement value with free text (e.g. "2D6" for Cave
    /// Squigs) - see WarriorArchetype.MovementOverride. Movement itself stays a plain int fallback.</summary>
    public string? MovementOverride { get; set; }

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

    /// <summary>True = this archetype may learn spells - which magic school(s) it can pick from comes
    /// from the parent WarbandSeedData.MagicSchools, not from here (see WarriorArchetype.IsSpellcaster).</summary>
    public bool IsSpellcaster { get; set; }

    /// <summary>True for Mutant/Possessed-type archetypes that may buy Mutations at recruitment.</summary>
    public bool CanBuyMutations { get; set; }

    /// <summary>English Name of one of the parent WarbandSeedData's EquipmentLists entries that this
    /// archetype's Weapons/Armour line draws from - null/omitted = this archetype never uses equipment
    /// (Ghouls, Zombies, Trolls, Cave Squigs...).</summary>
    public string? EquipmentListName { get; set; }
}

public class EquipmentSeedData
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.EquipmentCategory member name.</summary>
    public string Category { get; set; } = string.Empty;

    public int Cost { get; set; }
    public int? Rarity { get; set; }
    public LocalizedText? Description { get; set; }

    /// <summary>True = only this warband can buy/carry it (see WarbandArchetypeEquipmentEntity) - the
    /// Rare/Trading-Post channel, bought any time and not tied to a starting EquipmentList.</summary>
    public bool RestrictedToThisWarband { get; set; }

    /// <summary>English Name(s) of WarriorSeedData entries declared in the SAME warband file that
    /// alone may have this item (e.g. Averlanders' "Long bow" in the shared Scout list - Bergjaeger
    /// only) - null/empty = every warrior of the restricted warband(s)/list members can have it.</summary>
    public List<string>? RestrictedToWarriorNames { get; set; }
}

/// <summary>One named starting-equipment list (see WarbandSeedData.EquipmentLists) - ItemNames
/// resolved against either the common Data/SeedData/Equipment.json pool or this same band's own
/// Equipment declarations at seed time.</summary>
public class EquipmentListSeedData
{
    public LocalizedText Name { get; set; } = new();
    public List<string> ItemNames { get; set; } = new();
}

public class SpellSeedData
{
    /// <summary>Must match the English Name of one of the parent WarbandSeedData's MagicSchools entries.</summary>
    public string MagicSchoolName { get; set; } = string.Empty;
    public int RollValue { get; set; }
    public int? Difficulty { get; set; }
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Description { get; set; }
}

/// <summary>One magic school entry (see WarbandSeedData.MagicSchools) - find-or-created by English Name
/// at seed time, same shape/rationale as SpecialRuleSeedData.</summary>
public class MagicSchoolSeedData
{
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

/// <summary>One entry of the Mutation catalog - find-or-created by English Name at seed time, see
/// WarbandSeedData.Mutations. Most entries are shared verbatim across Chaos-adjacent warbands
/// (RestrictedToThisWarband false); some warbands add their own exclusive entries instead (e.g.
/// Kermesse du Chaos's Nurgle-themed Bénédictions).</summary>
public class MutationSeedData
{
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Description { get; set; }
    public int Cost { get; set; }

    /// <summary>True = only this warband may buy it (see WarbandArchetypeMutationEntity).</summary>
    public bool RestrictedToThisWarband { get; set; }
}

/// <summary>One Skill catalog entry - either from the common pool (Data/SeedData/Skills.json, always
/// unrestricted) or a warband's own special-skill table (WarbandSeedData.Skills).</summary>
public class SkillSeedData
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.SkillCategory member name.</summary>
    public string Category { get; set; } = string.Empty;

    public LocalizedText? Description { get; set; }

    /// <summary>True = only the declaring warband may pick it (see WarbandArchetypeSkillEntity).
    /// Always false for the common pool (Skills.json).</summary>
    public bool RestrictedToThisWarband { get; set; }

    /// <summary>English Name(s) of WarriorSeedData entries declared in the SAME warband file that alone
    /// may pick this skill (e.g. "Da Cunnin' Plan" -&gt; ["Orc Boss"]) - null/empty = every warrior of the
    /// restricted warband(s) can pick it. Only meaningful alongside RestrictedToThisWarband.</summary>
    public List<string>? RestrictedToWarriorNames { get; set; }
}

/// <summary>One row of the rulebook's Serious Injuries charts (Data/SeedData/Injuries.json, common to
/// every warband - not declared per-band). See Injury.Category/RollRange.</summary>
public class InjurySeedData
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.InjuryCategory member name.</summary>
    public string Category { get; set; } = string.Empty;

    public string? RollRange { get; set; }
    public LocalizedText? Description { get; set; }
}

/// <summary>One magic school plus its full spell table (Data/SeedData/MagicSchools.json only) - the
/// explicit owner of a school's spells, referenced by warband files via WarbandSeedData.MagicSchools
/// (name-only, no Description, no Spells) to link a spellcaster without redeclaring the table.</summary>
public class MagicSchoolWithSpellsSeedData
{
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Description { get; set; }
    public List<SpellSeedData> Spells { get; set; } = new();
}

public class MountSeedData
{
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Description { get; set; }
    public int Cost { get; set; }
    public int? Rarity { get; set; }
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    /// <summary>True = only this warband may buy/ride it (see WarbandArchetypeMountEntity).</summary>
    public bool RestrictedToThisWarband { get; set; }

    /// <summary>Find-or-created by English Name, same as WarbandSeedData.SpecialRules.</summary>
    public List<SpecialRuleSeedData> SpecialRules { get; set; } = new();
}
