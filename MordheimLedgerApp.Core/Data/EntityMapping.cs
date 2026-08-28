using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

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
    internal static string ResolveName(string key, IReadOnlyDictionary<string, string> translations) =>
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
        WyrdstoneShards = e.WyrdstoneShards,
        PendingExplorationBonusDie = e.PendingExplorationBonusDie,
        NextGameNote = e.NextGameNote,
        HasCatacombReroll = e.HasCatacombReroll,
        GameInProgress = e.GameInProgress,
        Notes = e.Notes
    };

    public static WarbandEntity ToEntity(this Warband m) => new()
    {
        Id = m.Id,
        CampaignId = m.CampaignId,
        WarbandArchetypeId = m.WarbandArchetypeId,
        Name = m.Name,
        Treasury = m.Treasury,
        WyrdstoneShards = m.WyrdstoneShards,
        PendingExplorationBonusDie = m.PendingExplorationBonusDie,
        NextGameNote = m.NextGameNote,
        HasCatacombReroll = m.HasCatacombReroll,
        GameInProgress = m.GameInProgress,
        Notes = m.Notes
    };

    public static WarbandArchetype ToModel(this WarbandArchetypeEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByWarbandId = null,
        IReadOnlyDictionary<int, List<MagicSchool>>? magicSchoolsByWarbandId = null,
        IReadOnlyDictionary<int, Race>? racesById = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Source = e.Source,
        Grade = e.Grade,
        StartingTreasury = e.StartingTreasury,
        MaxWarriors = e.MaxWarriors,
        MinWarriors = e.MinWarriors,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        ImagePath = e.ImagePath ?? string.Empty,
        SpecialRules = specialRulesByWarbandId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>(),
        MagicSchools = magicSchoolsByWarbandId?.GetValueOrDefault(e.Id) ?? new List<MagicSchool>(),
        RaceId = e.RaceId,
        Race = racesById?.GetValueOrDefault(e.RaceId)
    };

    public static WarbandArchetypeEntity ToEntity(this WarbandArchetype m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        Source = m.Source,
        Grade = m.Grade,
        StartingTreasury = m.StartingTreasury,
        MaxWarriors = m.MaxWarriors,
        MinWarriors = m.MinWarriors,
        DescriptionKey = m.DescriptionKey,
        ImagePath = m.ImagePath,
        RaceId = m.RaceId
    };

    public static WarriorArchetype ToModel(this WarriorArchetypeEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByWarriorArchetypeId = null,
        IReadOnlyDictionary<int, RacialProfile>? racialProfilesById = null) => new()
    {
        Id = e.Id,
        WarbandArchetypeId = e.WarbandArchetypeId,
        Name = ResolveName(e.NameKey, translations),
        IsHero = e.IsHero,
        Cost = e.Cost,
        Source = e.Source,
        MaxCount = e.MaxCount,
        MinCount = e.MinCount,
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
        SpecialRules = specialRulesByWarriorArchetypeId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>(),
        EquipmentListId = e.EquipmentListId,
        CanUseEquipment = e.CanUseEquipment,
        AllowedSkillCategories = ParseSkillCategories(e.AllowedSkillCategories),
        IsLargeCreature = e.IsLargeCreature,
        GainsExperience = e.GainsExperience,
        IsLeader = e.IsLeader,
        RacialProfileId = e.RacialProfileId,
        RacialProfile = racialProfilesById?.GetValueOrDefault(e.RacialProfileId)
    };

    private static List<SkillCategory> ParseSkillCategories(string? csv) =>
        string.IsNullOrEmpty(csv)
            ? new List<SkillCategory>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<SkillCategory>).ToList();

    public static WarriorArchetypeEntity ToEntity(this WarriorArchetype m) => new()
    {
        Id = m.Id,
        WarbandArchetypeId = m.WarbandArchetypeId,
        NameKey = m.NameKey ?? string.Empty,
        IsHero = m.IsHero,
        Cost = m.Cost,
        Source = m.Source,
        MaxCount = m.MaxCount,
        MinCount = m.MinCount,
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
        ImagePath = m.ImagePath,
        EquipmentListId = m.EquipmentListId,
        CanUseEquipment = m.CanUseEquipment,
        AllowedSkillCategories = m.AllowedSkillCategories.Count == 0 ? null : string.Join(',', m.AllowedSkillCategories),
        IsLargeCreature = m.IsLargeCreature,
        GainsExperience = m.GainsExperience,
        IsLeader = m.IsLeader,
        RacialProfileId = m.RacialProfileId
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
        Leadership = archetype.Leadership,
        // Snapshot for the "has this stat changed since recruitment" delta indicator (WarriorRow) - see
        // Warrior.StartingMovement's doc.
        StartingMovement = archetype.Movement,
        StartingWeaponSkill = archetype.WeaponSkill,
        StartingBallisticSkill = archetype.BallisticSkill,
        StartingStrength = archetype.Strength,
        StartingToughness = archetype.Toughness,
        StartingWounds = archetype.Wounds,
        StartingInitiative = archetype.Initiative,
        StartingAttacks = archetype.Attacks,
        StartingLeadership = archetype.Leadership,
        EquipmentListId = archetype.EquipmentListId,
        CanUseEquipment = archetype.CanUseEquipment,
        AllowedSkillCategories = new List<Models.Library.SkillCategory>(archetype.AllowedSkillCategories),
        IsLargeCreature = archetype.IsLargeCreature,
        GainsExperience = archetype.GainsExperience,
        IsLeader = archetype.IsLeader,
        // Snapshot at recruitment, same convention as the rest of the stat line above - editing the
        // RacialProfile catalog later doesn't retroactively change an already-recruited Warrior. Falls
        // back to 0/null (never blocks an Advance) if the archetype's RacialProfile wasn't resolved by
        // the caller (see LibraryService.GetWarriorArchetypesAsync) rather than throwing.
        // Null (pas 0) quand RacialProfile n'est pas résolu - voir CharacteristicMaxes/Warrior.
        // MaxWeaponSkill etc. : "aucun plafond connu", jamais "plafonné à 0".
        MaxMovement = archetype.RacialProfile?.MovementOverride is null ? archetype.RacialProfile?.Movement : null,
        MaxWeaponSkill = archetype.RacialProfile?.WeaponSkill,
        MaxBallisticSkill = archetype.RacialProfile?.BallisticSkill,
        MaxStrength = archetype.RacialProfile?.Strength,
        MaxToughness = archetype.RacialProfile?.Toughness,
        MaxWounds = archetype.RacialProfile?.Wounds,
        MaxInitiative = archetype.RacialProfile?.Initiative,
        MaxAttacks = archetype.RacialProfile?.Attacks,
        MaxLeadership = archetype.RacialProfile?.Leadership
    };

    /// <summary>Recruits a Hired Sword (see Models.Library.HiredSword) as a Warrior row - mirrors
    /// WarriorArchetype.ToWarrior above, but IsHero stays false (a Hired Sword is "written into the
    /// roster in a Henchman group slot", Henchman D6 Injury table + Henchman XP-milestone spacing) while
    /// HiredSwordId makes IsHiredSword true, which independently routes Advances to the Hero table
    /// instead (see Warrior.IsHiredSword's doc - the one place these two concerns need decoupling).
    /// CanUseEquipment false enforces "cannot buy extra weapons/equipment for them" - his fixed starting
    /// gear is inserted separately by the caller (WarbandService.RecruitHiredSwordAsync) via the normal
    /// AddWarriorEquipmentAsync. No RacialProfile concept exists for a HiredSword, so every Max* stays
    /// null - an accepted gap, a Hired Sword's Advances never hit a racial ceiling.</summary>
    public static Warrior ToWarrior(this HiredSword hiredSword, string name) => new()
    {
        HiredSwordId = hiredSword.Id,
        HiredSwordBaseRating = hiredSword.BaseRating,
        Name = name,
        IsHero = false,
        Cost = hiredSword.HireCost,
        Experience = 0,
        HeadCount = 1,
        Movement = hiredSword.Movement,
        WeaponSkill = hiredSword.WeaponSkill,
        BallisticSkill = hiredSword.BallisticSkill,
        Strength = hiredSword.Strength,
        Toughness = hiredSword.Toughness,
        Wounds = hiredSword.Wounds,
        Initiative = hiredSword.Initiative,
        Attacks = hiredSword.Attacks,
        Leadership = hiredSword.Leadership,
        StartingMovement = hiredSword.Movement,
        StartingWeaponSkill = hiredSword.WeaponSkill,
        StartingBallisticSkill = hiredSword.BallisticSkill,
        StartingStrength = hiredSword.Strength,
        StartingToughness = hiredSword.Toughness,
        StartingWounds = hiredSword.Wounds,
        StartingInitiative = hiredSword.Initiative,
        StartingAttacks = hiredSword.Attacks,
        StartingLeadership = hiredSword.Leadership,
        EquipmentListId = null,
        CanUseEquipment = false,
        AllowedSkillCategories = new List<Models.Library.SkillCategory>(hiredSword.AllowedSkillCategories),
        IsLargeCreature = false,
        GainsExperience = true,
        IsLeader = false
    };

    /// <summary>Henchman-to-Hero promotion (Advance roll 10-12, see HenchmanAdvanceTable.IsPromotion) -
    /// clones the group's LIVE stats/XP onto a brand-new Warrior rather than reseeding from
    /// WarriorArchetype.ToWarrior(), since the rulebook requires the promoted model to keep every
    /// characteristic increase already earned by the group. WarriorArchetypeId stays pointed at the
    /// Henchman archetype (e.g. "Ghoul") - IsHero is already a mutable per-row copy field (see
    /// Models.Warrior), so flipping it true on this one new row is a legitimate, narrow exception to
    /// "IsHero mirrors archetype." AllowedSkillCategories is NOT copied from the Henchman archetype -
    /// the rulebook has the player pick two Hero skill tables available to the warband instead, left
    /// empty here for the caller to set explicitly. Existing carried equipment/skills/injuries stay
    /// with the source group's row (no join-table migration) - out of scope for this pass.</summary>
    public static Warrior CloneAsPromotedHero(this Warrior henchmanGroup, string name) => new()
    {
        WarbandId = henchmanGroup.WarbandId,
        WarriorArchetypeId = henchmanGroup.WarriorArchetypeId,
        Name = name,
        IsHero = true,
        Cost = henchmanGroup.Cost,
        Experience = henchmanGroup.Experience,
        HeadCount = 1,
        Movement = henchmanGroup.Movement,
        MovementOverride = henchmanGroup.MovementOverride,
        WeaponSkill = henchmanGroup.WeaponSkill,
        BallisticSkill = henchmanGroup.BallisticSkill,
        Strength = henchmanGroup.Strength,
        Toughness = henchmanGroup.Toughness,
        Wounds = henchmanGroup.Wounds,
        Initiative = henchmanGroup.Initiative,
        Attacks = henchmanGroup.Attacks,
        Leadership = henchmanGroup.Leadership,
        // Baseline resets to what the group had actually earned by the moment of promotion, not the
        // original Henchman archetype's template - this new Hero row's own "since recruitment" starts
        // now (see Warrior.StartingMovement's doc).
        StartingMovement = henchmanGroup.Movement,
        StartingWeaponSkill = henchmanGroup.WeaponSkill,
        StartingBallisticSkill = henchmanGroup.BallisticSkill,
        StartingStrength = henchmanGroup.Strength,
        StartingToughness = henchmanGroup.Toughness,
        StartingWounds = henchmanGroup.Wounds,
        StartingInitiative = henchmanGroup.Initiative,
        StartingAttacks = henchmanGroup.Attacks,
        StartingLeadership = henchmanGroup.Leadership,
        EquipmentListId = henchmanGroup.EquipmentListId,
        CanUseEquipment = henchmanGroup.CanUseEquipment,
        AllowedSkillCategories = new List<Models.Library.SkillCategory>(),
        IsLargeCreature = henchmanGroup.IsLargeCreature,
        GainsExperience = henchmanGroup.GainsExperience,
        IsLeader = false,
        MaxMovement = henchmanGroup.MaxMovement,
        MaxWeaponSkill = henchmanGroup.MaxWeaponSkill,
        MaxBallisticSkill = henchmanGroup.MaxBallisticSkill,
        MaxStrength = henchmanGroup.MaxStrength,
        MaxToughness = henchmanGroup.MaxToughness,
        MaxWounds = henchmanGroup.MaxWounds,
        MaxInitiative = henchmanGroup.MaxInitiative,
        MaxAttacks = henchmanGroup.MaxAttacks,
        MaxLeadership = henchmanGroup.MaxLeadership,
        IncreasedCharacteristics = new List<CharacteristicField>()
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
        IReadOnlyDictionary<int, List<int>>? restrictions = null,
        IReadOnlyDictionary<int, List<int>>? warriorRestrictions = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Category = e.Category,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        RestrictedToWarbandArchetypeIds = restrictions?.GetValueOrDefault(e.Id) ?? new List<int>(),
        RestrictedToWarriorArchetypeIds = warriorRestrictions?.GetValueOrDefault(e.Id) ?? new List<int>()
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

    public static HiredSword ToModel(this HiredSwordEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<int>>? restrictions = null,
        IReadOnlyDictionary<int, List<int>>? startingEquipmentByHiredSwordId = null,
        IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByHiredSwordId = null,
        IReadOnlyDictionary<int, MagicSchool>? magicSchoolsById = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        HireCost = e.HireCost,
        Upkeep = e.Upkeep,
        BaseRating = e.BaseRating,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        Movement = e.Movement,
        WeaponSkill = e.WeaponSkill,
        BallisticSkill = e.BallisticSkill,
        Strength = e.Strength,
        Toughness = e.Toughness,
        Wounds = e.Wounds,
        Initiative = e.Initiative,
        Attacks = e.Attacks,
        Leadership = e.Leadership,
        AllowedSkillCategories = ParseSkillCategories(e.AllowedSkillCategories),
        StartingEquipmentIds = startingEquipmentByHiredSwordId?.GetValueOrDefault(e.Id) ?? new List<int>(),
        RestrictedToWarbandArchetypeIds = restrictions?.GetValueOrDefault(e.Id) ?? new List<int>(),
        SpecialRules = specialRulesByHiredSwordId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>(),
        MagicSchoolId = e.MagicSchoolId,
        MagicSchool = e.MagicSchoolId is { } magicSchoolId ? magicSchoolsById?.GetValueOrDefault(magicSchoolId) : null
    };

    public static HiredSwordEntity ToEntity(this HiredSword m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        HireCost = m.HireCost,
        Upkeep = m.Upkeep,
        BaseRating = m.BaseRating,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath,
        Movement = m.Movement,
        WeaponSkill = m.WeaponSkill,
        BallisticSkill = m.BallisticSkill,
        Strength = m.Strength,
        Toughness = m.Toughness,
        Wounds = m.Wounds,
        Initiative = m.Initiative,
        Attacks = m.Attacks,
        Leadership = m.Leadership,
        MagicSchoolId = m.MagicSchoolId,
        AllowedSkillCategories = m.AllowedSkillCategories.Count == 0 ? null : string.Join(',', m.AllowedSkillCategories)
    };

    public static Injury ToModel(this InjuryEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByInjuryId = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        Category = e.Category,
        RollRange = e.RollRange,
        BranchRange = e.BranchRange,
        SpecialRules = specialRulesByInjuryId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>()
    };

    public static InjuryEntity ToEntity(this Injury m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath,
        Category = m.Category,
        RollRange = m.RollRange,
        BranchRange = m.BranchRange
    };

    public static ExplorationResult ToModel(this ExplorationResultEntity e, IReadOnlyDictionary<string, string> translations,
        IEnumerable<ExplorationOutcome>? outcomes = null) => new()
    {
        Id = e.Id,
        DiceCount = e.DiceCount,
        Value = e.Value,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations) ?? string.Empty,
        ShortDescription = ResolveDescription(e.ShortDescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        ShortDescriptionKey = e.ShortDescriptionKey,
        Source = e.Source,
        RollsIndependently = e.RollsIndependently,
        StatTestField = e.StatTestField,
        StatTestTargetsLeader = e.StatTestTargetsLeader,
        AutoPassStatTestWarbandArchetypeNames = string.IsNullOrEmpty(e.AutoPassStatTestWarbandArchetypeNamesCsv)
            ? new() : e.AutoPassStatTestWarbandArchetypeNamesCsv.Split(',').ToList(),
        RequiresDoubleRoll = e.RequiresDoubleRoll,
        BonusStatTestField = e.BonusStatTestField,
        RequiresSentHero = e.RequiresSentHero,
        Outcomes = outcomes?.ToList() ?? new List<ExplorationOutcome>()
    };

    public static ExplorationResultEntity ToEntity(this ExplorationResult m) => new()
    {
        Id = m.Id,
        DiceCount = m.DiceCount,
        Value = m.Value,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey ?? string.Empty,
        ShortDescriptionKey = m.ShortDescriptionKey,
        Source = m.Source,
        RollsIndependently = m.RollsIndependently,
        StatTestField = m.StatTestField,
        StatTestTargetsLeader = m.StatTestTargetsLeader,
        AutoPassStatTestWarbandArchetypeNamesCsv = m.AutoPassStatTestWarbandArchetypeNames.Count > 0
            ? string.Join(",", m.AutoPassStatTestWarbandArchetypeNames) : null,
        RequiresDoubleRoll = m.RequiresDoubleRoll,
        BonusStatTestField = m.BonusStatTestField,
        RequiresSentHero = m.RequiresSentHero
    };

    public static ExplorationOutcome ToModel(this ExplorationOutcomeEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        ExplorationResultId = e.ExplorationResultId,
        SubRollMin = e.SubRollMin,
        SubRollMax = e.SubRollMax,
        Kind = e.Kind,
        GoldFormula = e.GoldFormula,
        EquipmentItemName = e.EquipmentItemName,
        ItemQuantityFormula = e.ItemQuantityFormula,
        FoundValueFormula = e.FoundValueFormula,
        MaterialRuleName = e.MaterialRuleName,
        SecondaryEquipmentItemName = e.SecondaryEquipmentItemName,
        AlternativeEquipmentItemName = e.AlternativeEquipmentItemName,
        Note = e.Note,
        BranchText = ResolveDescription(e.BranchTextKey, translations),
        BranchTextKey = e.BranchTextKey,
        StatTestPass = e.StatTestPass,
        CausesSickness = e.CausesSickness,
        RequiresDoubleRoll = e.RequiresDoubleRoll,
        CausesDeath = e.CausesDeath,
        TriggersArtefactRoll = e.TriggersArtefactRoll,
        RestrictedToWarbandArchetypeNames = string.IsNullOrEmpty(e.RestrictedToWarbandArchetypeNamesCsv)
            ? new() : e.RestrictedToWarbandArchetypeNamesCsv.Split(',').ToList(),
        GrantsNextExplorationBonusDie = e.GrantsNextExplorationBonusDie,
        GrantsLeaderExperience = e.GrantsLeaderExperience,
        GrantsDistributedHeroExperienceFormula = e.GrantsDistributedHeroExperienceFormula,
        GrantsFreeHenchmanArchetypeName = e.GrantsFreeHenchmanArchetypeName,
        GrantsOptionalEquippedHenchman = e.GrantsOptionalEquippedHenchman,
        NextGameNoteText = ResolveDescription(e.NextGameNoteTextKey, translations),
        NextGameNoteTextKey = e.NextGameNoteTextKey,
        GrantsWeaponBlessing = e.GrantsWeaponBlessing,
        GrantsCatacombReroll = e.GrantsCatacombReroll,
        GrantsFreeHiredSword = e.GrantsFreeHiredSword
    };

    public static ExplorationOutcomeEntity ToEntity(this ExplorationOutcome m) => new()
    {
        Id = m.Id,
        ExplorationResultId = m.ExplorationResultId,
        SubRollMin = m.SubRollMin,
        SubRollMax = m.SubRollMax,
        Kind = m.Kind,
        GoldFormula = m.GoldFormula,
        EquipmentItemName = m.EquipmentItemName,
        ItemQuantityFormula = m.ItemQuantityFormula,
        FoundValueFormula = m.FoundValueFormula,
        MaterialRuleName = m.MaterialRuleName,
        SecondaryEquipmentItemName = m.SecondaryEquipmentItemName,
        AlternativeEquipmentItemName = m.AlternativeEquipmentItemName,
        Note = m.Note,
        BranchTextKey = m.BranchTextKey,
        StatTestPass = m.StatTestPass,
        CausesSickness = m.CausesSickness,
        RequiresDoubleRoll = m.RequiresDoubleRoll,
        CausesDeath = m.CausesDeath,
        TriggersArtefactRoll = m.TriggersArtefactRoll,
        RestrictedToWarbandArchetypeNamesCsv = m.RestrictedToWarbandArchetypeNames.Count > 0
            ? string.Join(",", m.RestrictedToWarbandArchetypeNames) : null,
        GrantsNextExplorationBonusDie = m.GrantsNextExplorationBonusDie,
        GrantsLeaderExperience = m.GrantsLeaderExperience,
        GrantsDistributedHeroExperienceFormula = m.GrantsDistributedHeroExperienceFormula,
        GrantsFreeHenchmanArchetypeName = m.GrantsFreeHenchmanArchetypeName,
        GrantsOptionalEquippedHenchman = m.GrantsOptionalEquippedHenchman,
        NextGameNoteTextKey = m.NextGameNoteTextKey,
        GrantsWeaponBlessing = m.GrantsWeaponBlessing,
        GrantsCatacombReroll = m.GrantsCatacombReroll,
        GrantsFreeHiredSword = m.GrantsFreeHiredSword
    };

    public static SpecialRule ToModel(this SpecialRuleEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        CostMultiplier = e.CostMultiplier,
        Abbreviation = e.Abbreviation,
        Rarity = e.Rarity,
        IsResaleUpgrade = e.IsResaleUpgrade,
        HatredTargetWarbandArchetypeIds = string.IsNullOrEmpty(e.HatredTargetWarbandArchetypeIds)
            ? new List<int>()
            : e.HatredTargetWarbandArchetypeIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
    };

    public static SpecialRuleEntity ToEntity(this SpecialRule m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath,
        CostMultiplier = m.CostMultiplier,
        Abbreviation = m.Abbreviation,
        Rarity = m.Rarity,
        IsResaleUpgrade = m.IsResaleUpgrade,
        HatredTargetWarbandArchetypeIds = m.HatredTargetWarbandArchetypeIds.Count == 0 ? null : string.Join(',', m.HatredTargetWarbandArchetypeIds)
    };

    public static EquipmentItem ToModel(this EquipmentItemEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<int>>? restrictions = null,
        IReadOnlyDictionary<int, List<int>>? warriorRestrictions = null,
        IReadOnlyDictionary<int, List<SpecialRule>>? specialRulesByItemId = null) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Category = e.Category,
        Cost = e.Cost,
        Rarity = e.Rarity,
        CostRandomMax = e.CostRandomMax,
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        ImagePath = e.ImagePath ?? string.Empty,
        RestrictedToWarbandArchetypeIds = restrictions?.GetValueOrDefault(e.Id) ?? new List<int>(),
        RestrictedToWarriorArchetypeIds = warriorRestrictions?.GetValueOrDefault(e.Id) ?? new List<int>(),
        SpecialRules = specialRulesByItemId?.GetValueOrDefault(e.Id) ?? new List<SpecialRule>(),
        IsFreeDagger = e.IsFreeDagger,
        Movement = e.Movement,
        WeaponSkill = e.WeaponSkill,
        BallisticSkill = e.BallisticSkill,
        Strength = e.Strength,
        Toughness = e.Toughness,
        Wounds = e.Wounds,
        Initiative = e.Initiative,
        Attacks = e.Attacks,
        Leadership = e.Leadership,
        GrantsSkillCategory = e.GrantsSkillCategory,
        GrantsSpecificSkillName = e.GrantsSpecificSkillName,
        GrantsRareItemSearchBonus = e.GrantsRareItemSearchBonus,
        IsSellable = e.IsSellable,
        GrantsBonusExplorationDice = e.GrantsBonusExplorationDice
    };

    public static EquipmentList ToModel(this EquipmentListEntity e, IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, List<int>>? itemsByListId = null) => new()
    {
        Id = e.Id,
        WarbandArchetypeId = e.WarbandArchetypeId,
        Name = ResolveName(e.NameKey, translations),
        NameKey = e.NameKey,
        Source = e.Source,
        ItemIds = itemsByListId?.GetValueOrDefault(e.Id) ?? new List<int>()
    };

    public static EquipmentListEntity ToEntity(this EquipmentList m) => new()
    {
        Id = m.Id,
        WarbandArchetypeId = m.WarbandArchetypeId,
        NameKey = m.NameKey ?? string.Empty,
        Source = m.Source
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

    public static Race ToModel(this RaceEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source
    };

    public static RaceEntity ToEntity(this Race m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source
    };

    public static RacialProfile ToModel(this RacialProfileEntity e, IReadOnlyDictionary<string, string> translations) => new()
    {
        Id = e.Id,
        Name = ResolveName(e.NameKey, translations),
        Description = ResolveDescription(e.DescriptionKey, translations),
        NameKey = e.NameKey,
        DescriptionKey = e.DescriptionKey,
        Source = e.Source,
        Movement = e.Movement,
        MovementOverride = e.MovementOverride,
        WeaponSkill = e.WeaponSkill,
        BallisticSkill = e.BallisticSkill,
        Strength = e.Strength,
        Toughness = e.Toughness,
        Wounds = e.Wounds,
        Initiative = e.Initiative,
        Attacks = e.Attacks,
        Leadership = e.Leadership
    };

    public static RacialProfileEntity ToEntity(this RacialProfile m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        Movement = m.Movement,
        MovementOverride = m.MovementOverride,
        WeaponSkill = m.WeaponSkill,
        BallisticSkill = m.BallisticSkill,
        Strength = m.Strength,
        Toughness = m.Toughness,
        Wounds = m.Wounds,
        Initiative = m.Initiative,
        Attacks = m.Attacks,
        Leadership = m.Leadership
    };

    public static EquipmentItemEntity ToEntity(this EquipmentItem m) => new()
    {
        Id = m.Id,
        NameKey = m.NameKey ?? string.Empty,
        Category = m.Category,
        Cost = m.Cost,
        Rarity = m.Rarity,
        CostRandomMax = m.CostRandomMax,
        DescriptionKey = m.DescriptionKey,
        Source = m.Source,
        ImagePath = m.ImagePath,
        IsFreeDagger = m.IsFreeDagger,
        Movement = m.Movement,
        WeaponSkill = m.WeaponSkill,
        BallisticSkill = m.BallisticSkill,
        Strength = m.Strength,
        Toughness = m.Toughness,
        Wounds = m.Wounds,
        Initiative = m.Initiative,
        Attacks = m.Attacks,
        Leadership = m.Leadership,
        GrantsSkillCategory = m.GrantsSkillCategory,
        GrantsSpecificSkillName = m.GrantsSpecificSkillName,
        GrantsRareItemSearchBonus = m.GrantsRareItemSearchBonus,
        IsSellable = m.IsSellable,
        GrantsBonusExplorationDice = m.GrantsBonusExplorationDice
    };

    /// <param name="equipment">Carried items, loaded separately via the join table (sqlite-net does no joins).</param>
    /// <param name="skills">Learned skills, loaded separately via the join table.</param>
    /// <param name="injuries">Tracked injuries, loaded separately via the join table.</param>
    /// <param name="spells">Learned spells, loaded separately via the join table.</param>
    /// <param name="mutations">Bought mutations, loaded separately via the join table.</param>
    /// <param name="animal">Ridden mount (an EquipmentItem, Category == Animal), resolved separately -
    /// not a join, see WarriorEntity.AnimalId.</param>
    public static Warrior ToModel(this WarriorEntity e, IEnumerable<WarriorEquipment>? equipment = null, IEnumerable<WarriorSkill>? skills = null,
        IEnumerable<WarriorInjury>? injuries = null, IEnumerable<WarriorSpell>? spells = null, IEnumerable<WarriorMutation>? mutations = null,
        EquipmentItem? animal = null, IEnumerable<WarriorHatred>? hatreds = null) => new()
    {
        Id = e.Id,
        WarbandId = e.WarbandId,
        WarriorArchetypeId = e.WarriorArchetypeId,
        HiredSwordId = e.HiredSwordId,
        HiredSwordBaseRating = e.HiredSwordBaseRating,
        HiredSwordUpkeepPrepaid = e.HiredSwordUpkeepPrepaid,
        Name = e.Name,
        IsHero = e.IsHero,
        Cost = e.Cost,
        Experience = e.Experience,
        Status = e.Status,
        SickGamesRemaining = e.SickGamesRemaining,
        HeadCount = e.HeadCount,
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
        StartingMovement = e.StartingMovement,
        StartingWeaponSkill = e.StartingWeaponSkill,
        StartingBallisticSkill = e.StartingBallisticSkill,
        StartingStrength = e.StartingStrength,
        StartingToughness = e.StartingToughness,
        StartingWounds = e.StartingWounds,
        StartingInitiative = e.StartingInitiative,
        StartingAttacks = e.StartingAttacks,
        StartingLeadership = e.StartingLeadership,
        EquipmentListId = e.EquipmentListId,
        CanUseEquipment = e.CanUseEquipment,
        AllowedSkillCategories = ParseSkillCategories(e.AllowedSkillCategories),
        Equipment = equipment?.ToList() ?? new List<WarriorEquipment>(),
        Skills = skills?.ToList() ?? new List<WarriorSkill>(),
        Injuries = injuries?.ToList() ?? new List<WarriorInjury>(),
        Spells = spells?.ToList() ?? new List<WarriorSpell>(),
        Mutations = mutations?.ToList() ?? new List<WarriorMutation>(),
        Hatreds = hatreds?.ToList() ?? new List<WarriorHatred>(),
        Animal = animal,
        IsLargeCreature = e.IsLargeCreature,
        GainsExperience = e.GainsExperience,
        IsLeader = e.IsLeader,
        MaxMovement = e.MaxMovement,
        MaxWeaponSkill = e.MaxWeaponSkill,
        MaxBallisticSkill = e.MaxBallisticSkill,
        MaxStrength = e.MaxStrength,
        MaxToughness = e.MaxToughness,
        MaxWounds = e.MaxWounds,
        MaxInitiative = e.MaxInitiative,
        MaxAttacks = e.MaxAttacks,
        MaxLeadership = e.MaxLeadership,
        IncreasedCharacteristics = string.IsNullOrEmpty(e.IncreasedCharacteristics)
            ? new List<CharacteristicField>()
            : e.IncreasedCharacteristics.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<CharacteristicField>).ToList()
    };

    public static WarriorEntity ToEntity(this Warrior m) => new()
    {
        Id = m.Id,
        WarbandId = m.WarbandId,
        WarriorArchetypeId = m.WarriorArchetypeId,
        HiredSwordId = m.HiredSwordId,
        HiredSwordBaseRating = m.HiredSwordBaseRating,
        HiredSwordUpkeepPrepaid = m.HiredSwordUpkeepPrepaid,
        Name = m.Name,
        IsHero = m.IsHero,
        Cost = m.Cost,
        Experience = m.Experience,
        Status = m.Status,
        SickGamesRemaining = m.SickGamesRemaining,
        HeadCount = m.HeadCount,
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
        StartingMovement = m.StartingMovement,
        StartingWeaponSkill = m.StartingWeaponSkill,
        StartingBallisticSkill = m.StartingBallisticSkill,
        StartingStrength = m.StartingStrength,
        StartingToughness = m.StartingToughness,
        StartingWounds = m.StartingWounds,
        StartingInitiative = m.StartingInitiative,
        StartingAttacks = m.StartingAttacks,
        StartingLeadership = m.StartingLeadership,
        AnimalId = m.Animal?.Id,
        EquipmentListId = m.EquipmentListId,
        CanUseEquipment = m.CanUseEquipment,
        AllowedSkillCategories = m.AllowedSkillCategories.Count == 0 ? null : string.Join(',', m.AllowedSkillCategories),
        IsLargeCreature = m.IsLargeCreature,
        GainsExperience = m.GainsExperience,
        IsLeader = m.IsLeader,
        MaxMovement = m.MaxMovement,
        MaxWeaponSkill = m.MaxWeaponSkill,
        MaxBallisticSkill = m.MaxBallisticSkill,
        MaxStrength = m.MaxStrength,
        MaxToughness = m.MaxToughness,
        MaxWounds = m.MaxWounds,
        MaxInitiative = m.MaxInitiative,
        MaxAttacks = m.MaxAttacks,
        MaxLeadership = m.MaxLeadership,
        IncreasedCharacteristics = m.IncreasedCharacteristics.Count == 0 ? null : string.Join(',', m.IncreasedCharacteristics)
    };

    /// <param name="item">The catalog item this row references, loaded separately.</param>
    /// <param name="materialRule">The chosen material rule, loaded separately - see
    /// WarriorEquipment.MaterialRule.</param>
    /// <param name="blessingRule">The attached blessing rule, loaded separately - see
    /// WarriorEquipment.BlessingRule. Independent of materialRule - both can be set at once.</param>
    public static WarriorEquipment ToModel(this WarriorEquipmentEntity e, EquipmentItem item, SpecialRule? materialRule = null, SpecialRule? blessingRule = null) => new()
    {
        Id = e.Id,
        WarriorId = e.WarriorId,
        Item = item,
        Quantity = e.Quantity,
        MaterialRule = materialRule,
        BlessingRule = blessingRule,
        FoundValueOverride = e.FoundValueOverride
    };

    public static WarbandEquipmentEntity ToEntity(this WarbandEquipment m) => new()
    {
        Id = m.Id,
        WarbandId = m.WarbandId,
        EquipmentItemId = m.Item.Id,
        Quantity = m.Quantity,
        MaterialSpecialRuleId = m.MaterialRule?.Id,
        FoundValueOverride = m.FoundValueOverride
    };

    public static WarbandEquipment ToModel(this WarbandEquipmentEntity e, EquipmentItem item, SpecialRule? materialRule = null) => new()
    {
        Id = e.Id,
        WarbandId = e.WarbandId,
        Item = item,
        Quantity = e.Quantity,
        MaterialRule = materialRule,
        FoundValueOverride = e.FoundValueOverride
    };

    public static WarriorEquipmentEntity ToEntity(this WarriorEquipment m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        EquipmentItemId = m.Item.Id,
        Quantity = m.Quantity,
        MaterialSpecialRuleId = m.MaterialRule?.Id,
        BlessingSpecialRuleId = m.BlessingRule?.Id,
        FoundValueOverride = m.FoundValueOverride
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
        Item = item,
        IsTemporary = e.IsTemporary
    };

    public static WarriorInjuryEntity ToEntity(this WarriorInjury m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        InjuryId = m.Item.Id,
        IsTemporary = m.IsTemporary
    };

    /// <param name="resolvedName">The display name of whichever Target* is set, resolved by the caller
    /// (WarbandService.GetWarriorsAsync) - see WarriorHatred.Name.</param>
    public static WarriorHatred ToModel(this WarriorHatredEntity e, string resolvedName) => new()
    {
        Id = e.Id,
        WarriorId = e.WarriorId,
        TargetWarbandArchetypeId = e.TargetWarbandArchetypeId,
        TargetFreeText = e.TargetFreeText,
        Name = resolvedName
    };

    public static WarriorHatredEntity ToEntity(this WarriorHatred m) => new()
    {
        Id = m.Id,
        WarriorId = m.WarriorId,
        TargetWarbandArchetypeId = m.TargetWarbandArchetypeId,
        TargetFreeText = m.TargetFreeText
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
