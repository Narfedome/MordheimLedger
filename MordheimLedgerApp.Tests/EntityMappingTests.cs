using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

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
