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

    /// <summary>English Name reference into Data/SeedData/Races.json (e.g. "Human", "Skaven") - resolved
    /// once at seed time (see AppDatabase.FindOrCreateRaceAsync/SeedRacesAsync, seeded before any
    /// warband file, same idiom as MagicSchools above). Every band declares exactly one - see
    /// Models.Library.WarbandArchetype.RaceId.</summary>
    public string Race { get; set; } = string.Empty;

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
    public int? MinWarriors { get; set; }

    /// <summary>Filename of a MauiImage under Resources/Images/Warbands/ (e.g. "orc_mob.jpg") - resolved
    /// by MAUI's flat resource lookup, no folder prefix needed. Null/empty = tile falls back to a glyph
    /// (see LibraryItemImageView).</summary>
    public string? ImagePath { get; set; }

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
    public int? MinCount { get; set; }
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
    /// archetype's Weapons/Armour line draws from - null/omitted = no curated list assigned, falls back
    /// to the full common+band equipment pool in the picker (NOT "never uses equipment" - see
    /// CanUseEquipment for that).</summary>
    public string? EquipmentListName { get; set; }

    /// <summary>False for archetypes the rulebook explicitly forbids from carrying weapons/armour
    /// (Ghouls, Zombies, Trolls, Rat Ogre, Giant Rats... - see their "No Equipment" special rule) -
    /// omitted/true for every ordinary archetype. See WarriorArchetype.CanUseEquipment.</summary>
    public bool CanUseEquipment { get; set; } = true;

    /// <summary>This archetype's row of the warband's "skill table" (e.g. ["Combat","Strength","Speed"])
    /// - matches MordheimLedgerApp.Core.Models.Library.SkillCategory member names. Empty/omitted = not
    /// sourced yet, not "may pick nothing" - see WarriorArchetype.AllowedSkillCategories.</summary>
    public List<string> SkillCategories { get; set; } = new();

    /// <summary>True for "large creature" archetypes (Rat Ogre, Ogre, Troll...) - see
    /// WarriorArchetype.IsLargeCreature.</summary>
    public bool IsLargeCreature { get; set; }

    /// <summary>False for archetypes carrying the "Never Gains Experience" special rule (Zombie...) -
    /// omitted/true for every ordinary archetype. See WarriorArchetype.GainsExperience.</summary>
    public bool GainsExperience { get; set; } = true;

    /// <summary>True for the one archetype that represents this warband's leader (e.g. the Mercenary
    /// Captain) - exactly one per warband file. See WarriorArchetype.IsLeader.</summary>
    public bool IsLeader { get; set; }
}

public class EquipmentSeedData
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.EquipmentCategory member name.</summary>
    public string Category { get; set; } = string.Empty;

    public int Cost { get; set; }
    public int? Rarity { get; set; }

    /// <summary>Null = Cost is fixed. Otherwise the maximum value of a random supplement rolled on top of
    /// Cost at purchase time (e.g. "30 + 3D6 gc" -> Cost 30, CostRandomMax 18) - see
    /// EquipmentItem.CostRandomMax.</summary>
    public int? CostRandomMax { get; set; }

    public LocalizedText? Description { get; set; }

    /// <summary>True = only this warband can buy/carry it (see WarbandArchetypeEquipmentEntity) - the
    /// Rare/Trading-Post channel, bought any time and not tied to a starting EquipmentList. Meant for an
    /// entry declared directly inside a per-band file (e.g. Orc Mob's own "Sanglier de guerre"). For a
    /// COMMON catalog entry (Equipment.json) restricted to several named bands at once, use
    /// RestrictedToWarbandNames instead - the two are mutually exclusive in practice, never both set.</summary>
    public bool RestrictedToThisWarband { get; set; }

    /// <summary>File-name stems (e.g. "Reiklanders", "Marienburgers" - the SeedWarbandFromJsonAsync
    /// argument minus ".json") of every warband allowed to buy this COMMON catalog entry (Equipment.json
    /// only - a per-band file's own entries use RestrictedToThisWarband instead, they only ever concern
    /// the one band that declares them). Resolved in a deferred pass once every warband has been seeded
    /// (see AppDatabase.SeedOfficialContentAsync) since no WarbandArchetype exists yet when Equipment.json
    /// itself is seeded. Null/empty = no multi-band restriction (still common to everyone, matching the
    /// default RestrictedToWarbandArchetypeIds = empty on the runtime model).</summary>
    public List<string>? RestrictedToWarbandNames { get; set; }

    /// <summary>English Name(s) of WarriorSeedData entries declared in the SAME warband file that
    /// alone may have this item (e.g. Averlanders' "Long bow" in the shared Scout list - Bergjaeger
    /// only) - null/empty = every warrior of the restricted warband(s)/list members can have it.</summary>
    public List<string>? RestrictedToWarriorNames { get; set; }

    /// <summary>Weapon/armour-specific rules (e.g. "Parry", "Cutting Edge") - find-or-created by
    /// English Name. Only applied when this item is newly created (see AppDatabase) - a band file
    /// re-declaring an already-seeded item by name doesn't re-attach rules, same precedent as
    /// Description there.</summary>
    public List<SpecialRuleSeedData> SpecialRules { get; set; } = new();

    /// <summary>True only for the common pool's Dagger entry - see EquipmentItem.IsFreeDagger.</summary>
    public bool IsFreeDagger { get; set; }

    /// <summary>Profile stats, only meaningful when Category is "Animal" (a mount, e.g. Warhorse/War
    /// Boar - considered equipment by the rulebook, folded into this same catalog rather than a separate
    /// Animal content type) - null for every other category. See EquipmentItem's equivalent fields.</summary>
    public int? Movement { get; set; }
    public int? WeaponSkill { get; set; }
    public int? BallisticSkill { get; set; }
    public int? Strength { get; set; }
    public int? Toughness { get; set; }
    public int? Wounds { get; set; }
    public int? Initiative { get; set; }
    public int? Attacks { get; set; }
    public int? Leadership { get; set; }

    /// <summary>Matches a MordheimLedgerApp.Core.Models.Library.SkillCategory member name - see
    /// EquipmentItem.GrantsSkillCategory. Null for almost every item.</summary>
    public string? GrantsSkillCategory { get; set; }

    /// <summary>See EquipmentItem.GrantsSpecificSkillName. Null for almost every item.</summary>
    public string? GrantsSpecificSkillName { get; set; }

    /// <summary>See EquipmentItem.GrantsRareItemSearchBonus. Null for almost every item.</summary>
    public int? GrantsRareItemSearchBonus { get; set; }

    /// <summary>See EquipmentItem.IsSellable. False/absent for almost every item.</summary>
    public bool IsSellable { get; set; }

    /// <summary>See EquipmentItem.GrantsBonusExplorationDice. Null for almost every item.</summary>
    public int? GrantsBonusExplorationDice { get; set; }
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

/// <summary>One race/species entry (Data/SeedData/Races.json, common - not declared per-band) - see
/// Models.Library.Race. Find-or-created by English Name at seed time, same shape as
/// MagicSchoolSeedData.</summary>
public class RaceSeedData
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

    /// <summary>Null = not a purchasable material. Non-null marks this rule as a weapon-material option
    /// (e.g. "Gromril" -&gt; 4) - see SpecialRule.CostMultiplier.</summary>
    public int? CostMultiplier { get; set; }

    /// <summary>Only meaningful alongside CostMultiplier - see SpecialRule.Abbreviation.</summary>
    public string? Abbreviation { get; set; }

    /// <summary>Only meaningful alongside CostMultiplier - see SpecialRule.Rarity.</summary>
    public int? Rarity { get; set; }

    /// <summary>Only meaningful alongside CostMultiplier - see SpecialRule.IsResaleUpgrade. False/absent
    /// for every material except "Ornate Weapon".</summary>
    public bool IsResaleUpgrade { get; set; }
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

    /// <summary>True = only this warband may buy it (see WarbandArchetypeMutationEntity) - meant for an
    /// entry declared directly inside a per-band file. For a COMMON catalog entry (Mutations.json)
    /// restricted to several named bands at once, use RestrictedToWarbandNames instead.</summary>
    public bool RestrictedToThisWarband { get; set; }

    /// <summary>Same mechanism as EquipmentSeedData.RestrictedToWarbandNames - only meaningful for an
    /// entry declared in the common Mutations.json catalog.</summary>
    public List<string>? RestrictedToWarbandNames { get; set; }
}

/// <summary>One Skill catalog entry - either from the common pool (Data/SeedData/Skills.json, always
/// unrestricted) or a warband's own special-skill table (WarbandSeedData.Skills).</summary>
public class SkillSeedData
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.SkillCategory member name.</summary>
    public string Category { get; set; } = string.Empty;

    public LocalizedText? Description { get; set; }

    /// <summary>True = only the declaring warband may pick it (see WarbandArchetypeSkillEntity) - meant
    /// for an entry declared directly inside a per-band file. For a COMMON catalog entry (Skills.json)
    /// restricted to several named bands at once, use RestrictedToWarbandNames instead.</summary>
    public bool RestrictedToThisWarband { get; set; }

    /// <summary>Same mechanism as EquipmentSeedData.RestrictedToWarbandNames - only meaningful for an
    /// entry declared in the common Skills.json catalog.</summary>
    public List<string>? RestrictedToWarbandNames { get; set; }

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

/// <summary>One entry of the rulebook's Exploration chart (Data/SeedData/ExplorationResults.json,
/// common - not declared per-band). See Models.Library.ExplorationResult.</summary>
public class ExplorationResultSeedData
{
    public int DiceCount { get; set; }
    public int Value { get; set; }
    public LocalizedText Name { get; set; } = new();
    public LocalizedText Description { get; set; } = new();

    /// <summary>See Models.Library.ExplorationResult.ShortDescription. Null (almost every entry) = the
    /// wizard shows Description as-is.</summary>
    public LocalizedText? ShortDescription { get; set; }

    public bool RollsIndependently { get; set; }

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.ExplorationStatField member name (e.g.
    /// "Toughness") - null (the vast majority) for entries with no stat test. See
    /// Models.Library.ExplorationResult.StatTestField.</summary>
    public string? StatTestField { get; set; }

    /// <summary>See Models.Library.ExplorationResult.StatTestTargetsLeader. False/absent for almost every
    /// entry - so far only Tavern (Taverne).</summary>
    public bool StatTestTargetsLeader { get; set; }

    /// <summary>See Models.Library.ExplorationResult.AutoPassStatTestWarbandArchetypeNames. Null/absent
    /// for almost every entry - so far only Tavern (Taverne).</summary>
    public List<string>? AutoPassStatTestWarbandArchetypeNames { get; set; }

    /// <summary>See Models.Library.ExplorationResult.RequiresDoubleRoll. False/absent for almost every
    /// entry - so far only Merchant's House (Maison du Marchand).</summary>
    public bool RequiresDoubleRoll { get; set; }

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.ExplorationStatField member name - see
    /// Models.Library.ExplorationResult.BonusStatTestField. Null for almost every entry - so far only
    /// Shattered Building (Bâtiment Éventré).</summary>
    public string? BonusStatTestField { get; set; }

    /// <summary>See Models.Library.ExplorationResult.RequiresSentHero. False/absent for almost every
    /// entry - so far only the Pit (La Fosse).</summary>
    public bool RequiresSentHero { get; set; }

    public List<ExplorationOutcomeSeedData> Outcomes { get; set; } = new();
}

/// <summary>One mechanized branch of an ExplorationResultSeedData - see
/// Models.Library.ExplorationOutcome for the field-by-field meaning.</summary>
public class ExplorationOutcomeSeedData
{
    public int? SubRollMin { get; set; }
    public int? SubRollMax { get; set; }

    /// <summary>Matches an MordheimLedgerApp.Core.Models.Library.ExplorationOutcomeKind member name.</summary>
    public string Kind { get; set; } = "None";

    public string? GoldFormula { get; set; }
    public string? EquipmentItemName { get; set; }
    public string? ItemQuantityFormula { get; set; }
    public string? FoundValueFormula { get; set; }
    public string? MaterialRuleName { get; set; }
    public string? SecondaryEquipmentItemName { get; set; }
    public string? AlternativeEquipmentItemName { get; set; }
    public string? Note { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.BranchText. Null for almost every outcome - so far
    /// only "conditioned by warband identity" branches (Straggler).</summary>
    public LocalizedText? BranchText { get; set; }

    public bool? StatTestPass { get; set; }
    public bool CausesSickness { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.RequiresDoubleRoll. False/absent for almost every
    /// outcome.</summary>
    public bool RequiresDoubleRoll { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.CausesDeath. False/absent for almost every
    /// outcome - so far only the Pit's (La Fosse) "devoured" branch.</summary>
    public bool CausesDeath { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.TriggersArtefactRoll. False/absent for almost
    /// every outcome - so far only Noble's Villa (Villa d'un Noble) and Hidden Treasure (Trésor Caché).</summary>
    public bool TriggersArtefactRoll { get; set; }

    /// <summary>English WarbandArchetype.Name(s) - see Models.Library.ExplorationOutcome.
    /// RestrictedToWarbandArchetypeNames. Null/absent (almost every outcome) = no restriction.</summary>
    public List<string>? RestrictedToWarbandArchetypeNames { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.GrantsNextExplorationBonusDie. False/absent for
    /// almost every outcome - so far only Straggler's (Traînard) "any other warband" branch.</summary>
    public bool GrantsNextExplorationBonusDie { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.GrantsLeaderExperience. Null/absent for almost
    /// every outcome - so far only Straggler's (Traînard) Possessed branch.</summary>
    public int? GrantsLeaderExperience { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.GrantsFreeHenchmanArchetypeName. Null/absent for
    /// almost every outcome - so far only Straggler's (Traînard) Undead branch ("Zombie").</summary>
    public string? GrantsFreeHenchmanArchetypeName { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.GrantsDistributedHeroExperienceFormula. Null for
    /// almost every outcome - so far only Prisoners' Possessed branch ("D3").</summary>
    public string? GrantsDistributedHeroExperienceFormula { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.GrantsOptionalEquippedHenchman. False/absent for
    /// almost every outcome - so far only Prisoners' "other warbands" catch-all branch.</summary>
    public bool GrantsOptionalEquippedHenchman { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.NextGameNoteText. Null for almost every outcome -
    /// so far only Graveyard's catch-all branch.</summary>
    public LocalizedText? NextGameNoteText { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.GrantsWeaponBlessing. False/absent for almost
    /// every outcome - so far only Shrine's Sisters of Sigmar/Witch Hunters branch.</summary>
    public bool GrantsWeaponBlessing { get; set; }

    /// <summary>See Models.Library.ExplorationOutcome.GrantsCatacombReroll. False/absent for almost
    /// every outcome - so far only Entrance to the Catacombs' single branch.</summary>
    public bool GrantsCatacombReroll { get; set; }
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
