using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Tests;

/// <summary>Tests for the rule-decision logic moved into Core.Rules (see D:\Dev\MordheimLedger
/// CLAUDE.md's rules-to-Core migration note) - previously untestable because it lived in the MAUI
/// head, which MordheimLedgerApp.Tests doesn't reference. Covers what the game mechanics audit found
/// unenforced by no automated test: weapon limits, recruitment caps, and the D66/2D6 roll tables.</summary>
public class RulesTests
{
    private static EquipmentItem Weapon(EquipmentCategory category, int id = 1, bool isFreeDagger = false) =>
        new() { Id = id, Category = category, IsFreeDagger = isFreeDagger };

    // --- WeaponLimits -------------------------------------------------------------------------

    [Fact]
    public void WeaponLimits_TwoMeleeWeapons_DoesNotExceed()
    {
        var items = new[] { Weapon(EquipmentCategory.MeleeWeapon), Weapon(EquipmentCategory.MeleeWeapon, 2) };
        Assert.False(WeaponLimits.ExceedsLimits(items));
    }

    [Fact]
    public void WeaponLimits_ThreeMeleeWeapons_Exceeds()
    {
        var items = new[]
        {
            Weapon(EquipmentCategory.MeleeWeapon, 1), Weapon(EquipmentCategory.MeleeWeapon, 2),
            Weapon(EquipmentCategory.MeleeWeapon, 3)
        };
        Assert.True(WeaponLimits.ExceedsLimits(items));
    }

    [Fact]
    public void WeaponLimits_FreeDagger_IsExemptFromMeleeCount()
    {
        // Free dagger + 2 bought melee weapons = 2 counted (dagger exempted), not 3.
        var items = new[]
        {
            Weapon(EquipmentCategory.MeleeWeapon, 1, isFreeDagger: true),
            Weapon(EquipmentCategory.MeleeWeapon, 2), Weapon(EquipmentCategory.MeleeWeapon, 3)
        };
        Assert.False(WeaponLimits.ExceedsLimits(items));
    }

    [Fact]
    public void WeaponLimits_ThreeDistinctMissileTypes_Exceeds()
    {
        var items = new[]
        {
            Weapon(EquipmentCategory.MissileWeapon, 1), Weapon(EquipmentCategory.MissileWeapon, 2),
            Weapon(EquipmentCategory.BlackPowderWeapon, 3)
        };
        Assert.True(WeaponLimits.ExceedsLimits(items));
    }

    [Fact]
    public void WeaponLimits_SameMissileTypeTwice_CountsOnce()
    {
        // Same EquipmentItem.Id twice (e.g. two crossbows bought separately) is one distinct type.
        var items = new[] { Weapon(EquipmentCategory.MissileWeapon, 1), Weapon(EquipmentCategory.MissileWeapon, 1) };
        Assert.False(WeaponLimits.ExceedsLimits(items));
    }

    // --- ExperienceMilestones ------------------------------------------------------------------

    [Fact]
    public void ExperienceMilestones_HeroMilestones_Has21MilestonesEndingAt90()
    {
        // 21 thick-border boxes total (4+4+4+3+3+3 per tier) - not the 90 total boxes on the printed
        // track (ExperienceTrackView.HeroBoxCount), just the milestone positions among them.
        var milestones = ExperienceMilestones.HeroMilestones();
        Assert.Equal(21, milestones.Count);
        Assert.Equal(90, milestones[^1]);
    }

    [Fact]
    public void ExperienceMilestones_HeroMilestones_FirstFewGapsMatchSheet()
    {
        // Heroes: gap-1 x4 (boxes 2,4,6,8), then gap-2 x4 starts at 11...
        var milestones = ExperienceMilestones.HeroMilestones();
        Assert.Equal(new[] { 2, 4, 6, 8 }, milestones.Take(4));
    }

    [Fact]
    public void ExperienceMilestones_HenchmanMilestones_GapWidensEachTime()
    {
        // Henchmen: gap widens by 1 each time - 2, 5, 9, 14, 20...
        var milestones = ExperienceMilestones.HenchmanMilestones(30);
        Assert.Equal(new[] { 2, 5, 9, 14, 20, 27 }, milestones);
    }

    [Fact]
    public void ExperienceMilestones_MilestonesCrossedCount_CountsMultipleAtOnce()
    {
        // Hero from 0 to 8 XP crosses milestones at 2, 4, 6, 8 - four at once.
        Assert.Equal(4, ExperienceMilestones.MilestonesCrossedCount(isHero: true, from: 0, to: 8));
    }

    [Fact]
    public void ExperienceMilestones_MilestonesCrossedCount_ZeroWhenNoGain()
    {
        Assert.Equal(0, ExperienceMilestones.MilestonesCrossedCount(isHero: true, from: 5, to: 5));
    }

    // --- SeriousInjuryTable (Heroes, D66) -------------------------------------------------------

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void SeriousInjuryTable_11To15_IsDeath(int roll)
    {
        Assert.True(SeriousInjuryTable.IsDeath(roll));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(21)]
    [InlineData(66)]
    public void SeriousInjuryTable_OtherResults_IsNotDeath(int roll)
    {
        Assert.False(SeriousInjuryTable.IsDeath(roll));
    }

    [Fact]
    public void SeriousInjuryTable_ValidRoll_ReturnsKey()
    {
        Assert.True(SeriousInjuryTable.TryGetTextKey(34, out var key));
        Assert.Equal("InjurySerious34", key);
    }

    [Theory]
    [InlineData(17)] // no die shows 7 on a D66's units digit representation used here
    [InlineData(10)]
    [InlineData(0)]
    public void SeriousInjuryTable_InvalidRoll_ReturnsFalse(int roll)
    {
        Assert.False(SeriousInjuryTable.TryGetTextKey(roll, out _));
    }

    [Fact]
    public void SeriousInjuryTable_RollDice_AlwaysProducesAValidRoll()
    {
        for (var i = 0; i < 200; i++)
            Assert.True(SeriousInjuryTable.TryGetTextKey(SeriousInjuryTable.RollDice(), out _));
    }

    // --- HenchmanInjuryTable (D6) ---------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void HenchmanInjuryTable_1And2_IsDeath(int roll)
    {
        Assert.True(HenchmanInjuryTable.IsDeath(roll));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void HenchmanInjuryTable_3To6_IsNotDeath(int roll)
    {
        Assert.False(HenchmanInjuryTable.IsDeath(roll));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void HenchmanInjuryTable_OutOfRange_ReturnsFalse(int roll)
    {
        Assert.False(HenchmanInjuryTable.TryGetTextKey(roll, out _));
    }

    // --- HeroAdvanceTable / HenchmanAdvanceTable (2D6) ------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void HeroAdvanceTable_SkillRolls_IsSkillTrue(int roll)
    {
        Assert.True(HeroAdvanceTable.IsSkill(roll));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void HeroAdvanceTable_StatOrChoiceRolls_IsSkillFalse(int roll)
    {
        Assert.False(HeroAdvanceTable.IsSkill(roll));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    public void HeroAdvanceTable_OutOfRange_ReturnsFalse(int roll)
    {
        Assert.False(HeroAdvanceTable.TryGetTextKey(roll, out _));
    }

    [Fact]
    public void HenchmanAdvanceTable_ValidRoll_ReturnsKey()
    {
        Assert.True(HenchmanAdvanceTable.TryGetTextKey(10, out var key));
        Assert.Equal("AdvanceHenchman10", key);
    }

    [Fact]
    public void HenchmanAdvanceTable_RollDice_AlwaysBetween2And12()
    {
        for (var i = 0; i < 200; i++)
        {
            var roll = HenchmanAdvanceTable.RollDice();
            Assert.InRange(roll, 2, 12);
        }
    }

    // --- RecruitmentRules -----------------------------------------------------------------------

    [Fact]
    public void RecruitmentRules_CanRecruit_TrueWhenUnderAllCaps()
    {
        Assert.True(RecruitmentRules.CanRecruit(currentCountForType: 0, maxCountForType: 1,
            currentTotalWarriors: 3, maxWarriors: 15, isExistingWarband: false, remainingTreasury: 100, cost: 20));
    }

    [Fact]
    public void RecruitmentRules_CanRecruit_FalseAtPerTypeMax()
    {
        Assert.False(RecruitmentRules.CanRecruit(currentCountForType: 1, maxCountForType: 1,
            currentTotalWarriors: 3, maxWarriors: 15, isExistingWarband: false, remainingTreasury: 100, cost: 20));
    }

    [Fact]
    public void RecruitmentRules_CanRecruit_FalseAtRosterCap()
    {
        Assert.False(RecruitmentRules.CanRecruit(currentCountForType: 0, maxCountForType: null,
            currentTotalWarriors: 15, maxWarriors: 15, isExistingWarband: false, remainingTreasury: 100, cost: 20));
    }

    [Fact]
    public void RecruitmentRules_CanRecruit_FalseWhenTreasuryTooLowOnNewWarband()
    {
        Assert.False(RecruitmentRules.CanRecruit(currentCountForType: 0, maxCountForType: null,
            currentTotalWarriors: 3, maxWarriors: null, isExistingWarband: false, remainingTreasury: 10, cost: 20));
    }

    [Fact]
    public void RecruitmentRules_CanRecruit_IgnoresTreasuryOnExistingWarband()
    {
        // Editing an already-created warband's roster doesn't spend from a starting budget.
        Assert.True(RecruitmentRules.CanRecruit(currentCountForType: 0, maxCountForType: null,
            currentTotalWarriors: 3, maxWarriors: null, isExistingWarband: true, remainingTreasury: 0, cost: 20));
    }

    [Fact]
    public void RecruitmentRules_MeetsMinWarriors_TrueWhenNoMinimumSet()
    {
        Assert.True(RecruitmentRules.MeetsMinWarriors(0, null));
    }

    [Fact]
    public void RecruitmentRules_MeetsMinWarriors_FalseWhenBelowMinimum()
    {
        Assert.False(RecruitmentRules.MeetsMinWarriors(2, 3));
    }

    [Fact]
    public void RecruitmentRules_MeetsMinCount_TrueWhenNoMinimumSet()
    {
        Assert.True(RecruitmentRules.MeetsMinCount(0, null));
        Assert.True(RecruitmentRules.MeetsMinCount(0, 0));
    }

    [Fact]
    public void RecruitmentRules_MeetsMinCount_FalseWhenMandatoryLeaderMissing()
    {
        // e.g. the unique leader archetype (MinCount 1) with none recruited yet.
        Assert.False(RecruitmentRules.MeetsMinCount(0, 1));
        Assert.True(RecruitmentRules.MeetsMinCount(1, 1));
    }

    [Fact]
    public void RecruitmentRules_CalculateRemainingTreasury_NewWarband_SubtractsSpent()
    {
        Assert.Equal(400, RecruitmentRules.CalculateRemainingTreasury(startingTreasury: 500, totalSpent: 100,
            isExistingWarband: false, treasuryOverride: 0));
    }

    [Fact]
    public void RecruitmentRules_CalculateRemainingTreasury_ExistingWarband_IgnoresSpentUsesOverride()
    {
        Assert.Equal(250, RecruitmentRules.CalculateRemainingTreasury(startingTreasury: 500, totalSpent: 100,
            isExistingWarband: true, treasuryOverride: 250));
    }

    // --- EquipmentPricing -----------------------------------------------------------------------

    [Fact]
    public void EquipmentPricing_FirstDagger_IsEligibleWhenNoneCarriedYet()
    {
        Assert.True(EquipmentPricing.IsFreeDaggerEligible(isFreeDaggerItem: true, alreadyCarriesFreeDagger: false));
    }

    [Fact]
    public void EquipmentPricing_SecondDagger_NotEligible()
    {
        Assert.False(EquipmentPricing.IsFreeDaggerEligible(isFreeDaggerItem: true, alreadyCarriesFreeDagger: true));
    }

    [Fact]
    public void EquipmentPricing_NonDaggerItem_NeverEligible()
    {
        Assert.False(EquipmentPricing.IsFreeDaggerEligible(isFreeDaggerItem: false, alreadyCarriesFreeDagger: false));
    }

    [Fact]
    public void EquipmentPricing_CalculateCost_FreeIsAlwaysZero()
    {
        Assert.Equal(0, EquipmentPricing.CalculateCost(baseCost: 30, materialCostMultiplier: 11, isFree: true));
    }

    [Fact]
    public void EquipmentPricing_CalculateCost_NoMaterial_IsBaseCost()
    {
        Assert.Equal(30, EquipmentPricing.CalculateCost(baseCost: 30, materialCostMultiplier: null, isFree: false));
    }

    [Fact]
    public void EquipmentPricing_CalculateCost_Gromril_MultipliesBaseCost()
    {
        // Gromril = ×11 per CLAUDE.md (Gromril 11, Ithilmar 9).
        Assert.Equal(330, EquipmentPricing.CalculateCost(baseCost: 30, materialCostMultiplier: 11, isFree: false));
    }
}
