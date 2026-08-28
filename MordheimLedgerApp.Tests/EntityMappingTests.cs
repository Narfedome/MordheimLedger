using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Tests;

public class EntityMappingTests
{
    [Fact]
    public void Warband_RoundTrips_ThroughEntity()
    {
        var warband = new Warband
        {
            Id = 1,
            CampaignId = 42,
            WarbandArchetypeId = 3,
            Name = "The Bleeding Roses",
            Treasury = 250,
            Notes = "House-ruled starting gold"
        };

        var roundTripped = warband.ToEntity().ToModel();

        Assert.Equal(warband.Id, roundTripped.Id);
        Assert.Equal(warband.CampaignId, roundTripped.CampaignId);
        Assert.Equal(warband.WarbandArchetypeId, roundTripped.WarbandArchetypeId);
        Assert.Equal(warband.Name, roundTripped.Name);
        Assert.Equal(warband.Treasury, roundTripped.Treasury);
        Assert.Equal(warband.Notes, roundTripped.Notes);
    }

    [Fact]
    public void Campaign_RoundTrips_ThroughEntity()
    {
        var campaign = new Campaign { Id = 1, Name = "Winter of the Damned", Notes = "Every 3rd Saturday" };

        var roundTripped = campaign.ToEntity().ToModel();

        Assert.Equal(campaign.Id, roundTripped.Id);
        Assert.Equal(campaign.Name, roundTripped.Name);
        Assert.Equal(campaign.Notes, roundTripped.Notes);
    }

    [Fact]
    public void EquipmentItem_RoundTrips_ThroughEntity()
    {
        var item = new EquipmentItem
        {
            Id = 1,
            Name = "Dagger",
            NameKey = "dagger-name",
            Category = EquipmentCategory.MeleeWeapon,
            Cost = 0,
            Rarity = null,
            Description = "Grants one extra Attack, always used in addition to another weapon.",
            DescriptionKey = "dagger-desc",
            Source = ContentSource.Official
        };
        var translations = new Dictionary<string, string> { ["dagger-name"] = item.Name, ["dagger-desc"] = item.Description };

        var roundTripped = item.ToEntity().ToModel(translations);

        Assert.Equal(item.Id, roundTripped.Id);
        Assert.Equal(item.Name, roundTripped.Name);
        Assert.Equal(item.Category, roundTripped.Category);
        Assert.Equal(item.Cost, roundTripped.Cost);
        Assert.Equal(item.Rarity, roundTripped.Rarity);
        Assert.Equal(item.Description, roundTripped.Description);
        Assert.Equal(item.Source, roundTripped.Source);
    }

    [Fact]
    public void WarbandArchetype_RoundTrips_ThroughEntity()
    {
        var archetype = new WarbandArchetype
        {
            Id = 3,
            Name = "Reiklander Mercenaries",
            NameKey = "reiklander-name",
            Source = ContentSource.Official,
            StartingTreasury = 500,
            MaxWarriors = 15,
            MinWarriors = 3,
            Description = "The default human warband of the Empire.",
            DescriptionKey = "reiklander-desc"
        };
        var translations = new Dictionary<string, string> { ["reiklander-name"] = archetype.Name, ["reiklander-desc"] = archetype.Description };

        var roundTripped = archetype.ToEntity().ToModel(translations);

        Assert.Equal(archetype.Id, roundTripped.Id);
        Assert.Equal(archetype.Name, roundTripped.Name);
        Assert.Equal(archetype.Source, roundTripped.Source);
        Assert.Equal(archetype.StartingTreasury, roundTripped.StartingTreasury);
        Assert.Equal(archetype.MaxWarriors, roundTripped.MaxWarriors);
        Assert.Equal(archetype.MinWarriors, roundTripped.MinWarriors);
        Assert.Equal(archetype.Description, roundTripped.Description);
    }

    [Fact]
    public void WarriorArchetype_RoundTrips_ThroughEntity()
    {
        var archetype = new WarriorArchetype
        {
            Id = 10,
            WarbandArchetypeId = 3,
            Name = "Mercenary Captain",
            NameKey = "captain-name",
            IsHero = true,
            Cost = 80,
            Source = ContentSource.Official,
            MaxCount = 1,
            MinCount = 1,
            Movement = 4,
            WeaponSkill = 4,
            BallisticSkill = 3,
            Strength = 3,
            Toughness = 3,
            Wounds = 1,
            Initiative = 4,
            Attacks = 1,
            Leadership = 8,
            StartingExperience = 20,
            Description = "May be given any Combat, Shooting or Strength skill.",
            DescriptionKey = "captain-desc"
        };
        var translations = new Dictionary<string, string> { ["captain-name"] = archetype.Name, ["captain-desc"] = archetype.Description };

        var roundTripped = archetype.ToEntity().ToModel(translations);

        Assert.Equal(archetype.Id, roundTripped.Id);
        Assert.Equal(archetype.WarbandArchetypeId, roundTripped.WarbandArchetypeId);
        Assert.Equal(archetype.Name, roundTripped.Name);
        Assert.Equal(archetype.IsHero, roundTripped.IsHero);
        Assert.Equal(archetype.Cost, roundTripped.Cost);
        Assert.Equal(archetype.Source, roundTripped.Source);
        Assert.Equal(archetype.MaxCount, roundTripped.MaxCount);
        Assert.Equal(archetype.MinCount, roundTripped.MinCount);
        Assert.Equal(archetype.Movement, roundTripped.Movement);
        Assert.Equal(archetype.WeaponSkill, roundTripped.WeaponSkill);
        Assert.Equal(archetype.BallisticSkill, roundTripped.BallisticSkill);
        Assert.Equal(archetype.Strength, roundTripped.Strength);
        Assert.Equal(archetype.Toughness, roundTripped.Toughness);
        Assert.Equal(archetype.Wounds, roundTripped.Wounds);
        Assert.Equal(archetype.Initiative, roundTripped.Initiative);
        Assert.Equal(archetype.Attacks, roundTripped.Attacks);
        Assert.Equal(archetype.Leadership, roundTripped.Leadership);
        Assert.Equal(archetype.StartingExperience, roundTripped.StartingExperience);
        Assert.Equal(archetype.Description, roundTripped.Description);
    }

    [Fact]
    public void RecruitingFromArchetype_PreFillsWarriorStatsCostAndStartingExperience()
    {
        var archetype = new WarriorArchetype
        {
            Id = 11,
            WarbandArchetypeId = 4,
            Name = "Witch Hunter Captain",
            IsHero = true,
            Cost = 80,
            StartingExperience = 20,
            Movement = 4,
            WeaponSkill = 4,
            BallisticSkill = 3,
            Strength = 3,
            Toughness = 3,
            Wounds = 1,
            Initiative = 4,
            Attacks = 1,
            Leadership = 8
        };

        var recruited = archetype.ToWarrior("Otto");

        Assert.Equal(archetype.Id, recruited.WarriorArchetypeId);
        Assert.Equal("Otto", recruited.Name);
        Assert.Equal(archetype.IsHero, recruited.IsHero);
        Assert.Equal(archetype.Cost, recruited.Cost);
        Assert.Equal(archetype.Movement, recruited.Movement);
        Assert.Equal(archetype.WeaponSkill, recruited.WeaponSkill);
        Assert.Equal(archetype.BallisticSkill, recruited.BallisticSkill);
        Assert.Equal(archetype.Strength, recruited.Strength);
        Assert.Equal(archetype.Toughness, recruited.Toughness);
        Assert.Equal(archetype.Wounds, recruited.Wounds);
        Assert.Equal(archetype.Initiative, recruited.Initiative);
        Assert.Equal(archetype.Attacks, recruited.Attacks);
        Assert.Equal(archetype.Leadership, recruited.Leadership);
        Assert.Equal(archetype.StartingExperience, recruited.Experience);
        Assert.Equal(WarriorStatus.Active, recruited.Status);

        // Baseline snapshot for the stat-changed color code (StatRowView) - matches the live stats at
        // recruitment, so nothing shows as changed until an Advance/Injury actually moves a stat.
        Assert.Equal(archetype.Movement, recruited.StartingMovement);
        Assert.Equal(archetype.WeaponSkill, recruited.StartingWeaponSkill);
        Assert.Equal(archetype.BallisticSkill, recruited.StartingBallisticSkill);
        Assert.Equal(archetype.Strength, recruited.StartingStrength);
        Assert.Equal(archetype.Toughness, recruited.StartingToughness);
        Assert.Equal(archetype.Wounds, recruited.StartingWounds);
        Assert.Equal(archetype.Initiative, recruited.StartingInitiative);
        Assert.Equal(archetype.Attacks, recruited.StartingAttacks);
        Assert.Equal(archetype.Leadership, recruited.StartingLeadership);
        Assert.False(recruited.WeaponSkillIncreased);
        Assert.False(recruited.WeaponSkillDecreased);
    }

    [Fact]
    public void Warrior_RoundTrips_ThroughEntity()
    {
        var warrior = new Warrior
        {
            Id = 1,
            WarbandId = 7,
            WarriorArchetypeId = 10,
            Name = "Otto",
            IsHero = true,
            Cost = 80,
            Experience = 12,
            Status = WarriorStatus.Active,
            Movement = 4,
            WeaponSkill = 4,
            BallisticSkill = 3,
            Strength = 3,
            Toughness = 3,
            Wounds = 1,
            Initiative = 4,
            Attacks = 1,
            Leadership = 8
        };

        var roundTripped = warrior.ToEntity().ToModel();

        Assert.Equal(warrior.Id, roundTripped.Id);
        Assert.Equal(warrior.WarbandId, roundTripped.WarbandId);
        Assert.Equal(warrior.WarriorArchetypeId, roundTripped.WarriorArchetypeId);
        Assert.Equal(warrior.Name, roundTripped.Name);
        Assert.Equal(warrior.IsHero, roundTripped.IsHero);
        Assert.Equal(warrior.Cost, roundTripped.Cost);
        Assert.Equal(warrior.Experience, roundTripped.Experience);
        Assert.Equal(warrior.Status, roundTripped.Status);
        Assert.Equal(warrior.Movement, roundTripped.Movement);
        Assert.Equal(warrior.WeaponSkill, roundTripped.WeaponSkill);
        Assert.Equal(warrior.BallisticSkill, roundTripped.BallisticSkill);
        Assert.Equal(warrior.Strength, roundTripped.Strength);
        Assert.Equal(warrior.Toughness, roundTripped.Toughness);
        Assert.Equal(warrior.Wounds, roundTripped.Wounds);
        Assert.Equal(warrior.Initiative, roundTripped.Initiative);
        Assert.Equal(warrior.Attacks, roundTripped.Attacks);
        Assert.Equal(warrior.Leadership, roundTripped.Leadership);
        Assert.Empty(roundTripped.Equipment);
    }

    // Hired Sword recruitment - exactly one of WarriorArchetypeId/HiredSwordId is ever set (see
    // Models.Warrior's class doc). IsHero stays false (Henchman-style D6 injuries/XP milestones) even
    // though Progression uses the Heroes table (IsHero || IsHiredSword at the two AdvanceRollEntry
    // construction sites in WarriorOutcomeRow.cs, not tested here - pure UI wiring).
    [Fact]
    public void RecruitingFromHiredSword_PreFillsProfileAndMarksNotHero()
    {
        var hiredSword = new HiredSword
        {
            Id = 5,
            Name = "Pit Fighter",
            HireCost = 30,
            Upkeep = 25,
            BaseRating = 22,
            Movement = 4,
            WeaponSkill = 4,
            BallisticSkill = 3,
            Strength = 4,
            Toughness = 4,
            Wounds = 1,
            Initiative = 5,
            Attacks = 2,
            Leadership = 7
        };

        var recruited = hiredSword.ToWarrior("Grimjaw");

        Assert.Null(recruited.WarriorArchetypeId);
        Assert.Equal(hiredSword.Id, recruited.HiredSwordId);
        Assert.True(recruited.IsHiredSword);
        Assert.Equal(hiredSword.BaseRating, recruited.HiredSwordBaseRating);
        Assert.False(recruited.HiredSwordUpkeepPrepaid);
        Assert.Equal("Grimjaw", recruited.Name);
        Assert.False(recruited.IsHero);
        Assert.Equal(1, recruited.HeadCount);
        Assert.False(recruited.CanUseEquipment);
        Assert.True(recruited.GainsExperience);
        Assert.Equal(hiredSword.Movement, recruited.Movement);
        Assert.Equal(hiredSword.WeaponSkill, recruited.WeaponSkill);
        Assert.Equal(hiredSword.BallisticSkill, recruited.BallisticSkill);
        Assert.Equal(hiredSword.Strength, recruited.Strength);
        Assert.Equal(hiredSword.Toughness, recruited.Toughness);
        Assert.Equal(hiredSword.Wounds, recruited.Wounds);
        Assert.Equal(hiredSword.Initiative, recruited.Initiative);
        Assert.Equal(hiredSword.Attacks, recruited.Attacks);
        Assert.Equal(hiredSword.Leadership, recruited.Leadership);
    }

    [Fact]
    public void HiredSwordWarrior_RoundTrips_ThroughEntity()
    {
        var warrior = new Warrior
        {
            Id = 2,
            WarbandId = 7,
            WarriorArchetypeId = null,
            HiredSwordId = 5,
            HiredSwordBaseRating = 22,
            HiredSwordUpkeepPrepaid = true,
            Name = "Grimjaw",
            IsHero = false,
            Cost = 30,
            Experience = 4,
            Status = WarriorStatus.Active
        };

        var roundTripped = warrior.ToEntity().ToModel();

        Assert.Null(roundTripped.WarriorArchetypeId);
        Assert.Equal(warrior.HiredSwordId, roundTripped.HiredSwordId);
        Assert.True(roundTripped.IsHiredSword);
        Assert.Equal(warrior.HiredSwordBaseRating, roundTripped.HiredSwordBaseRating);
        Assert.True(roundTripped.HiredSwordUpkeepPrepaid);
    }

    [Fact]
    public void RacialProfile_RoundTrips_ThroughEntity()
    {
        var profile = new RacialProfile
        {
            Id = 3,
            Name = "Human",
            Description = "Common human maximums.",
            NameKey = "k1",
            DescriptionKey = "k2",
            Source = ContentSource.Official,
            Movement = 4,
            WeaponSkill = 6,
            BallisticSkill = 6,
            Strength = 4,
            Toughness = 4,
            Wounds = 3,
            Initiative = 6,
            Attacks = 4,
            Leadership = 9
        };

        var translations = new Dictionary<string, string> { ["k1"] = profile.Name, ["k2"] = profile.Description };
        var roundTripped = profile.ToEntity().ToModel(translations);

        Assert.Equal(profile.Id, roundTripped.Id);
        Assert.Equal(profile.Name, roundTripped.Name);
        Assert.Equal(profile.Description, roundTripped.Description);
        Assert.Equal(profile.Source, roundTripped.Source);
        Assert.Equal(profile.Movement, roundTripped.Movement);
        Assert.Null(roundTripped.MovementOverride);
        Assert.Equal(profile.WeaponSkill, roundTripped.WeaponSkill);
        Assert.Equal(profile.BallisticSkill, roundTripped.BallisticSkill);
        Assert.Equal(profile.Strength, roundTripped.Strength);
        Assert.Equal(profile.Toughness, roundTripped.Toughness);
        Assert.Equal(profile.Wounds, roundTripped.Wounds);
        Assert.Equal(profile.Initiative, roundTripped.Initiative);
        Assert.Equal(profile.Attacks, roundTripped.Attacks);
        Assert.Equal(profile.Leadership, roundTripped.Leadership);
    }

    [Fact]
    public void RecruitingFromArchetype_CopiesRacialMaxSnapshotFromResolvedProfile()
    {
        var profile = new RacialProfile { Id = 1, Name = "Human", WeaponSkill = 6, BallisticSkill = 6, Strength = 4, Toughness = 4, Wounds = 3, Initiative = 6, Attacks = 4, Leadership = 9, Movement = 4 };
        var archetype = new WarriorArchetype { Id = 1, Name = "Captain", RacialProfileId = 1, RacialProfile = profile };

        var recruited = archetype.ToWarrior("Otto");

        Assert.Equal(profile.Movement, recruited.MaxMovement);
        Assert.Equal(profile.WeaponSkill, recruited.MaxWeaponSkill);
        Assert.Equal(profile.BallisticSkill, recruited.MaxBallisticSkill);
        Assert.Equal(profile.Strength, recruited.MaxStrength);
        Assert.Equal(profile.Toughness, recruited.MaxToughness);
        Assert.Equal(profile.Wounds, recruited.MaxWounds);
        Assert.Equal(profile.Initiative, recruited.MaxInitiative);
        Assert.Equal(profile.Attacks, recruited.MaxAttacks);
        Assert.Equal(profile.Leadership, recruited.MaxLeadership);
    }

    [Fact]
    public void RecruitingFromArchetype_UnresolvedRacialProfile_FallsBackToNullNeverBlocking()
    {
        var archetype = new WarriorArchetype { Id = 1, Name = "Captain", RacialProfileId = 5, RacialProfile = null };
        var recruited = archetype.ToWarrior("Otto");
        Assert.Null(recruited.MaxWeaponSkill);
        Assert.Null(recruited.MaxMovement);
    }

    [Fact]
    public void CloneAsPromotedHero_CopiesLiveStatsXpAndMaxes_NotArchetypeTemplate()
    {
        var henchmanGroup = new Warrior
        {
            Id = 42,
            WarbandId = 7,
            WarriorArchetypeId = 10,
            Name = "Ghoul Pack",
            IsHero = false,
            Cost = 25,
            Experience = 14,
            HeadCount = 3,
            WeaponSkill = 4,
            BallisticSkill = 0,
            Strength = 4,
            Toughness = 4,
            Wounds = 1,
            Initiative = 4,
            Attacks = 2,
            Leadership = 6,
            MaxWeaponSkill = 6,
            MaxStrength = 5,
            IncreasedCharacteristics = new List<CharacteristicField> { CharacteristicField.WeaponSkill }
        };

        var promoted = henchmanGroup.CloneAsPromotedHero("Grull");

        Assert.Equal("Grull", promoted.Name);
        Assert.True(promoted.IsHero);
        Assert.Equal(1, promoted.HeadCount);
        Assert.Equal(henchmanGroup.WarbandId, promoted.WarbandId);
        Assert.Equal(henchmanGroup.WarriorArchetypeId, promoted.WarriorArchetypeId);
        Assert.Equal(henchmanGroup.Experience, promoted.Experience);
        Assert.Equal(henchmanGroup.WeaponSkill, promoted.WeaponSkill);
        Assert.Equal(henchmanGroup.Strength, promoted.Strength);
        Assert.Equal(henchmanGroup.MaxWeaponSkill, promoted.MaxWeaponSkill);
        Assert.Equal(henchmanGroup.MaxStrength, promoted.MaxStrength);
        Assert.Empty(promoted.IncreasedCharacteristics);
        Assert.Empty(promoted.AllowedSkillCategories);
        Assert.False(promoted.IsLeader);

        // Baseline resets to what the group had actually earned by promotion (4 WeaponSkill here, not
        // the original Henchman archetype's template) - the new Hero's own "since recruitment" starts
        // now, so nothing shows as changed on the very first card render after promotion.
        Assert.Equal(henchmanGroup.WeaponSkill, promoted.StartingWeaponSkill);
        Assert.Equal(henchmanGroup.Strength, promoted.StartingStrength);
        Assert.False(promoted.WeaponSkillIncreased);
        Assert.False(promoted.StrengthIncreased);
    }

    [Fact]
    public void Warrior_StatDeltaProperties_ReflectChangeSinceStartingSnapshot()
    {
        var warrior = new Warrior
        {
            Movement = 5,
            StartingMovement = 4,
            WeaponSkill = 3,
            StartingWeaponSkill = 4,
            Strength = 3,
            StartingStrength = 3
        };

        Assert.True(warrior.MovementIncreased);
        Assert.False(warrior.MovementDecreased);
        Assert.False(warrior.WeaponSkillIncreased);
        Assert.True(warrior.WeaponSkillDecreased);
        Assert.False(warrior.StrengthIncreased);
        Assert.False(warrior.StrengthDecreased);
    }

    [Fact]
    public void Warrior_MovementDelta_IgnoredWhenMovementOverrideSet()
    {
        var warrior = new Warrior { Movement = 5, StartingMovement = 4, MovementOverride = "2D6" };

        Assert.False(warrior.MovementIncreased);
        Assert.False(warrior.MovementDecreased);
    }

    [Fact]
    public void HistoryEntry_RoundTrips_ThroughEntity()
    {
        var entry = new HistoryEntry
        {
            Id = 1,
            WarbandId = 7,
            Date = new DateTime(2026, 3, 5, 20, 30, 0),
            Text = "Battle: Victory. Otto gained 2 XP."
        };

        var roundTripped = entry.ToEntity().ToModel();

        Assert.Equal(entry.Id, roundTripped.Id);
        Assert.Equal(entry.WarbandId, roundTripped.WarbandId);
        Assert.Equal(entry.Date, roundTripped.Date);
        Assert.Equal(entry.Text, roundTripped.Text);
    }

    [Fact]
    public void WarriorEquipment_RoundTrips_ThroughEntity()
    {
        var dagger = new EquipmentItem { Id = 3, Name = "Dagger", Category = EquipmentCategory.MeleeWeapon };
        var carried = new WarriorEquipment { Id = 1, WarriorId = 7, Item = dagger, Quantity = 2 };

        var entity = carried.ToEntity();
        var roundTripped = entity.ToModel(dagger);

        Assert.Equal(carried.Id, roundTripped.Id);
        Assert.Equal(carried.WarriorId, roundTripped.WarriorId);
        Assert.Equal(carried.Quantity, roundTripped.Quantity);
        Assert.Equal(dagger.Id, entity.EquipmentItemId);
        Assert.Same(dagger, roundTripped.Item);
    }
}
