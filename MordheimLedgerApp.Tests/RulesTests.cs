using MordheimLedgerApp.Core.Models;
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

    [Theory]
    [InlineData(16)]
    [InlineData(21)]
    public void SeriousInjuryTable_16And21_IsMultipleInjuries(int roll)
    {
        Assert.True(SeriousInjuryTable.IsMultipleInjuries(roll));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(61)]
    [InlineData(66)]
    public void SeriousInjuryTable_OtherResults_IsNotMultipleInjuries(int roll)
    {
        Assert.False(SeriousInjuryTable.IsMultipleInjuries(roll));
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

    // --- SpellRules -------------------------------------------------------------------------------

    [Fact]
    public void SpellRules_RollDice_AlwaysBetween1And6()
    {
        for (var i = 0; i < 200; i++)
        {
            var roll = SpellRules.RollDice();
            Assert.InRange(roll, 1, 6);
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

    // --- DiceFormula -----------------------------------------------------------------------------

    [Fact]
    public void DiceFormula_FlatInteger_ReturnsItself()
    {
        Assert.Equal(100, DiceFormula.Roll("100"));
    }

    [Fact]
    public void DiceFormula_D6_IsWithinRange()
    {
        for (var i = 0; i < 50; i++)
        {
            var roll = DiceFormula.Roll("D6");
            Assert.InRange(roll, 1, 6);
        }
    }

    [Fact]
    public void DiceFormula_2D6_IsWithinRange()
    {
        for (var i = 0; i < 50; i++)
        {
            var roll = DiceFormula.Roll("2D6");
            Assert.InRange(roll, 2, 12);
        }
    }

    [Fact]
    public void DiceFormula_D3_IsWithinRange()
    {
        for (var i = 0; i < 50; i++)
        {
            var roll = DiceFormula.Roll("D3");
            Assert.InRange(roll, 1, 3);
        }
    }

    [Fact]
    public void DiceFormula_D6PlusFlat_AddsAfterSum()
    {
        for (var i = 0; i < 50; i++)
        {
            var roll = DiceFormula.Roll("D6+1");
            Assert.InRange(roll, 2, 7);
        }
    }

    [Fact]
    public void DiceFormula_D6TimesFlat_MultipliesSum()
    {
        for (var i = 0; i < 50; i++)
        {
            var roll = DiceFormula.Roll("D6x10");
            Assert.InRange(roll, 10, 60);
            Assert.Equal(0, roll % 10);
        }
    }

    [Fact]
    public void DiceFormula_2D6TimesFlat_MultipliesSum()
    {
        for (var i = 0; i < 50; i++)
        {
            var roll = DiceFormula.Roll("2D6x5");
            Assert.InRange(roll, 10, 60);
            Assert.Equal(0, roll % 5);
        }
    }

    [Fact]
    public void DiceFormula_InvalidFormula_Throws()
    {
        Assert.Throws<FormatException>(() => DiceFormula.Roll("banana"));
    }

    [Fact]
    public void DiceFormula_Apply_2D6x5_MultipliesGivenSum()
    {
        Assert.Equal(30, DiceFormula.Apply("2D6x5", [3, 3]));
    }

    [Fact]
    public void DiceFormula_Apply_D6Plus1_AddsAfterGivenSum()
    {
        Assert.Equal(5, DiceFormula.Apply("D6+1", [4]));
    }

    [Fact]
    public void DiceFormula_Apply_FlatInteger_IgnoresGivenDice()
    {
        Assert.Equal(100, DiceFormula.Apply("100", [3, 3]));
    }

    // --- ExplorationChart --------------------------------------------------------------------------

    [Theory]
    [InlineData(3, false, 0, 3)]
    [InlineData(3, true, 0, 4)]
    [InlineData(4, true, 1, 6)]
    [InlineData(6, true, 2, 6)] // hard-capped at 6 even though sources would allow 9
    public void ExplorationChart_ComputeDiceCount_CapsAtSix(int heroes, bool won, int bonus, int expected)
    {
        Assert.Equal(expected, ExplorationChart.ComputeDiceCount(heroes, won, bonus));
    }

    [Fact]
    public void ExplorationChart_DetectMultiples_NoRepeat_ReturnsNull()
    {
        Assert.Null(ExplorationChart.DetectMultiples([1, 2, 3, 4]));
    }

    [Fact]
    public void ExplorationChart_DetectMultiples_SimpleDouble()
    {
        Assert.Equal((2, 3), ExplorationChart.DetectMultiples([3, 3, 1, 2]));
    }

    [Fact]
    public void ExplorationChart_DetectMultiples_TripleBeatsDouble_RegardlessOfFace()
    {
        // Rulebook example: double 3 and triple 5 -> only the triple 5 counts.
        Assert.Equal((3, 5), ExplorationChart.DetectMultiples([3, 3, 5, 5, 5]));
    }

    [Fact]
    public void ExplorationChart_DetectMultiples_TieOnCount_HighestFaceWins()
    {
        // Rulebook example: double 1 and double 3 -> the double 3 counts.
        Assert.Equal((2, 3), ExplorationChart.DetectMultiples([1, 1, 3, 3]));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(11, 2)]
    [InlineData(12, 3)]
    [InlineData(17, 3)]
    [InlineData(18, 4)]
    [InlineData(24, 4)]
    [InlineData(25, 5)]
    [InlineData(30, 5)]
    [InlineData(31, 6)]
    [InlineData(35, 6)]
    [InlineData(36, 7)]
    [InlineData(50, 7)]
    public void ExplorationChart_ShardsFound_MatchesTable(int diceSum, int expectedShards)
    {
        Assert.Equal(expectedShards, ExplorationChart.ShardsFound(diceSum));
    }

    [Fact]
    public void ExplorationChart_RollStatTest_Toughness_IsWithinD6Range()
    {
        for (var i = 0; i < 50; i++)
            Assert.InRange(ExplorationChart.RollStatTest(ExplorationStatField.Toughness), 1, 6);
    }

    [Fact]
    public void ExplorationChart_RollStatTest_Leadership_IsWithin2D6Range()
    {
        for (var i = 0; i < 50; i++)
            Assert.InRange(ExplorationChart.RollStatTest(ExplorationStatField.Leadership), 2, 12);
    }

    [Fact]
    public void ExplorationChart_PassesStatTest_RollOfSix_AlwaysFailsRegardlessOfStat()
    {
        // RulesReference "Tests de caractéristiques": a roll of 6 is always a failure, even if the
        // tested stat is 6 or higher (e.g. a Toughness of 6 would otherwise pass 6 <= 6).
        Assert.False(ExplorationChart.PassesStatTest(ExplorationStatField.Toughness, roll: 6, statValue: 6));
        Assert.False(ExplorationChart.PassesStatTest(ExplorationStatField.Toughness, roll: 6, statValue: 10));
    }

    [Fact]
    public void ExplorationChart_PassesStatTest_Leadership_RollOfSix_ComparesNormally()
    {
        // The roll-of-6 auto-fail is specific to the general 1D6 rule - Commandement tests (2D6) have no
        // such stated exception, so a 6 (a perfectly ordinary 2D6 sum) is just compared to Cd as usual.
        Assert.True(ExplorationChart.PassesStatTest(ExplorationStatField.Leadership, roll: 6, statValue: 6));
        Assert.False(ExplorationChart.PassesStatTest(ExplorationStatField.Leadership, roll: 6, statValue: 5));
    }

    [Fact]
    public void ExplorationChart_PassesStatTest_NormalRoll_ComparesToStat()
    {
        Assert.True(ExplorationChart.PassesStatTest(ExplorationStatField.Toughness, roll: 3, statValue: 3));
        Assert.False(ExplorationChart.PassesStatTest(ExplorationStatField.Toughness, roll: 4, statValue: 3));
    }

    // --- ExplorationOutcomeResolver -----------------------------------------------------------------
    //
    // Fixtures mirror the real seed shapes (see Data/SeedData/ExplorationResults.json) rather than
    // minimal synthetic ones, so a fixture drifting out of sync with the actual JSON would be obvious.

    private static ExplorationOutcome Outcome(ExplorationOutcomeKind kind, int? subRollMin = null, int? subRollMax = null, bool? statTestPass = null,
        bool requiresDoubleRoll = false, List<string>? restrictedToWarbandArchetypeNames = null, bool grantsNextExplorationBonusDie = false) =>
        new()
        {
            Kind = kind, SubRollMin = subRollMin, SubRollMax = subRollMax, StatTestPass = statTestPass, RequiresDoubleRoll = requiresDoubleRoll,
            RestrictedToWarbandArchetypeNames = restrictedToWarbandArchetypeNames ?? [],
            GrantsNextExplorationBonusDie = grantsNextExplorationBonusDie
        };

    // Straggler (2,4): Groupe B "conditionné par la bande" - une branche par bande spécifique, une
    // branche catch-all (sans restriction) pour toutes les autres.
    private static ExplorationResult Straggler() => new()
    {
        DiceCount = 2, Value = 4, RollsIndependently = true,
        Outcomes =
        [
            Outcome(ExplorationOutcomeKind.Gold, restrictedToWarbandArchetypeNames: ["Skaven of Clan Eshin"]),
            Outcome(ExplorationOutcomeKind.None, restrictedToWarbandArchetypeNames: ["Cult of the Possessed"]),
            Outcome(ExplorationOutcomeKind.None, restrictedToWarbandArchetypeNames: ["Undead"]),
            Outcome(ExplorationOutcomeKind.None, grantsNextExplorationBonusDie: true)
        ]
    };

    // Corpse (2,3): five mutually exclusive sub-roll branches, no Auto branch at all.
    private static ExplorationResult Corpse() => new()
    {
        DiceCount = 2, Value = 3,
        Outcomes =
        [
            Outcome(ExplorationOutcomeKind.Gold, 1, 2),
            Outcome(ExplorationOutcomeKind.Item, 3, 3),
            Outcome(ExplorationOutcomeKind.Item, 4, 4),
            Outcome(ExplorationOutcomeKind.Item, 5, 5),
            Outcome(ExplorationOutcomeKind.Item, 6, 6)
        ]
    };

    // Shop (2,2): Auto gold branch + a sub-roll-gated bonus item on the SAME die (roll of 1).
    private static ExplorationResult Shop() => new()
    {
        DiceCount = 2, Value = 2,
        Outcomes = [Outcome(ExplorationOutcomeKind.Gold), Outcome(ExplorationOutcomeKind.Item, 1, 1)]
    };

    // Well (2,1): stat test, Pass = wyrdstone, Fail = sickness (no sub-roll at all).
    private static ExplorationResult Well() => new()
    {
        DiceCount = 2, Value = 1, StatTestField = ExplorationStatField.Toughness,
        Outcomes = [Outcome(ExplorationOutcomeKind.Wyrdstone, statTestPass: true), Outcome(ExplorationOutcomeKind.None, statTestPass: false)]
    };

    // Merchant's House (5,4): paired 2D6 double check, normal roll = gold, a double = the item instead.
    private static ExplorationResult MerchantsHouse() => new()
    {
        DiceCount = 5, Value = 4, RequiresDoubleRoll = true,
        Outcomes = [Outcome(ExplorationOutcomeKind.Gold), Outcome(ExplorationOutcomeKind.Item, requiresDoubleRoll: true)]
    };

    // Shattered Building (5,5): Auto wyrdstone (always found) + an ADDITIONAL Leadership test for a
    // bonus item (Wardog) - only a Pass branch exists, Fail resolves to nothing.
    private static ExplorationResult ShatteredBuilding() => new()
    {
        DiceCount = 5, Value = 5, BonusStatTestField = ExplorationStatField.Leadership,
        Outcomes = [Outcome(ExplorationOutcomeKind.Wyrdstone), Outcome(ExplorationOutcomeKind.Item, statTestPass: true)]
    };

    [Fact]
    public void ExplorationOutcomeResolver_ResolveAutoOutcome_SingleFlatBranch()
    {
        var ruinedHovels = new ExplorationResult { Outcomes = [Outcome(ExplorationOutcomeKind.Gold)] };
        Assert.Equal(ExplorationOutcomeKind.Gold, ExplorationOutcomeResolver.ResolveAutoOutcome(ruinedHovels)?.Kind);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveAutoOutcome_NullForSubRollOnlyResult()
    {
        Assert.Null(ExplorationOutcomeResolver.ResolveAutoOutcome(Corpse()));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveAutoOutcome_NullWhenStatTestGated()
    {
        // Well's branches are Auto-shaped (no sub-roll) but must never resolve before the stat test.
        Assert.Null(ExplorationOutcomeResolver.ResolveAutoOutcome(Well()));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveSubRollOutcome_PicksMatchingRange()
    {
        var outcome = ExplorationOutcomeResolver.ResolveSubRollOutcome(Corpse(), 4);
        Assert.Equal(ExplorationOutcomeKind.Item, outcome?.Kind);
        Assert.Equal(4, outcome?.SubRollMin);
    }

    [Theory]
    [InlineData(3, true)]  // roll == stat: still a pass ("equal to or lower than")
    [InlineData(4, false)] // roll one above stat: fails
    public void ExplorationOutcomeResolver_ResolveStatTestOutcome_ComparesRollToStat(int roll, bool expectedPass)
    {
        var outcome = ExplorationOutcomeResolver.ResolveStatTestOutcome(Well(), roll, statValue: 3);
        Assert.Equal(expectedPass, outcome?.StatTestPass);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveStatTestOutcome_RollOfSix_FailsEvenIfStatIsHighEnough()
    {
        var outcome = ExplorationOutcomeResolver.ResolveStatTestOutcome(Well(), roll: 6, statValue: 6);
        Assert.Equal(false, outcome?.StatTestPass);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveBonusItemOutcome_Shop_MatchesOnSameDie()
    {
        var shop = Shop();
        var goldOutcome = ExplorationOutcomeResolver.ResolveAutoOutcome(shop);
        var bonus = ExplorationOutcomeResolver.ResolveBonusItemOutcome(shop, goldOutcome, roll: 1);
        Assert.Equal(ExplorationOutcomeKind.Item, bonus?.Kind);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveBonusItemOutcome_Shop_NoBonusOutsideRange()
    {
        var shop = Shop();
        var goldOutcome = ExplorationOutcomeResolver.ResolveAutoOutcome(shop);
        Assert.Null(ExplorationOutcomeResolver.ResolveBonusItemOutcome(shop, goldOutcome, roll: 4));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveBonusItemOutcome_Regression_CorpseGoldNeverTriggersBonus()
    {
        // Bug found 2026-08-18: a Corpse gold roll of "4" spuriously matched Corpse's own sub-roll-4
        // Axe branch, because the original check never verified the resolved Gold outcome was itself
        // an Auto branch. Corpse's Gold branch is sub-roll-selected (1-2), never Auto - so no bonus
        // should ever resolve for it, regardless of the roll value passed in.
        var corpse = Corpse();
        var goldOutcome = ExplorationOutcomeResolver.ResolveSubRollOutcome(corpse, 1); // picks the 1-2 Gold branch
        Assert.Equal(ExplorationOutcomeKind.Gold, goldOutcome?.Kind);
        Assert.Null(ExplorationOutcomeResolver.ResolveBonusItemOutcome(corpse, goldOutcome, roll: 4));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveAutoOutcome_NullForDoubleRollGatedResult()
    {
        Assert.Null(ExplorationOutcomeResolver.ResolveAutoOutcome(MerchantsHouse()));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveAutoOutcome_NullForRollsIndependentlyResult()
    {
        // Regression (2026-08-20): every Straggler Outcome has SubRollMin null (they're picked by
        // warband identity, not a sub-roll) - without the RollsIndependently guard, ResolveAutoOutcome
        // would silently grab the FIRST one (Skaven gold) regardless of who's actually playing.
        Assert.Null(ExplorationOutcomeResolver.ResolveAutoOutcome(Straggler()));
    }

    [Theory]
    [InlineData("Skaven of Clan Eshin", ExplorationOutcomeKind.Gold)]
    [InlineData("Cult of the Possessed", ExplorationOutcomeKind.None)]
    [InlineData("Undead", ExplorationOutcomeKind.None)]
    public void ExplorationOutcomeResolver_ResolveWarbandOutcome_MatchesRestrictedBranch(string warbandName, ExplorationOutcomeKind expectedKind)
    {
        var outcome = ExplorationOutcomeResolver.ResolveWarbandOutcome(Straggler(), warbandName);
        Assert.Equal(expectedKind, outcome?.Kind);
        Assert.Contains(warbandName, outcome!.RestrictedToWarbandArchetypeNames);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveWarbandOutcome_FallsBackToCatchAllForUnlistedWarband()
    {
        var outcome = ExplorationOutcomeResolver.ResolveWarbandOutcome(Straggler(), "Witch Hunters");
        Assert.NotNull(outcome);
        Assert.Empty(outcome!.RestrictedToWarbandArchetypeNames);
        Assert.True(outcome.GrantsNextExplorationBonusDie);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveWarbandOutcome_NullWhenResultNotThisShape()
    {
        // Corpse is a normal sub-roll result, not a "conditioned by warband identity" one - no Outcome
        // carries a restriction, so ResolveWarbandOutcome must not claim it (even though Corpse isn't
        // RollsIndependently either, both guards matter independently).
        Assert.Null(ExplorationOutcomeResolver.ResolveWarbandOutcome(Corpse(), "Skaven of Clan Eshin"));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveDoubleRollOutcome_NoDouble_ReturnsGold()
    {
        Assert.Equal(ExplorationOutcomeKind.Gold, ExplorationOutcomeResolver.ResolveDoubleRollOutcome(MerchantsHouse(), 2, 5)?.Kind);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveDoubleRollOutcome_Double_ReturnsItem()
    {
        Assert.Equal(ExplorationOutcomeKind.Item, ExplorationOutcomeResolver.ResolveDoubleRollOutcome(MerchantsHouse(), 3, 3)?.Kind);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveDoubleRollOutcome_NullWhenResultNotGated()
    {
        Assert.Null(ExplorationOutcomeResolver.ResolveDoubleRollOutcome(Corpse(), 3, 3));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveAutoOutcome_ResolvesEvenWithBonusStatTestField()
    {
        // Contrast StatTestField (gates everything, ResolveAutoOutcome returns null) - BonusStatTestField
        // is additive, the Auto wyrdstone branch must resolve immediately regardless.
        Assert.Equal(ExplorationOutcomeKind.Wyrdstone, ExplorationOutcomeResolver.ResolveAutoOutcome(ShatteredBuilding())?.Kind);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveBonusStatTestOutcome_Pass_ReturnsItem()
    {
        Assert.Equal(ExplorationOutcomeKind.Item, ExplorationOutcomeResolver.ResolveBonusStatTestOutcome(ShatteredBuilding(), roll: 3, statValue: 8)?.Kind);
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveBonusStatTestOutcome_Fail_ReturnsNull()
    {
        Assert.Null(ExplorationOutcomeResolver.ResolveBonusStatTestOutcome(ShatteredBuilding(), roll: 9, statValue: 8));
    }

    [Fact]
    public void ExplorationOutcomeResolver_ResolveBonusStatTestOutcome_NullWhenResultHasNoBonusTest()
    {
        Assert.Null(ExplorationOutcomeResolver.ResolveBonusStatTestOutcome(Corpse(), roll: 1, statValue: 8));
    }

    // --- SkillEligibility -----------------------------------------------------------------------

    private static Warrior WarriorWith(List<SkillCategory> allowedCategories, params EquipmentItem[] carriedItems) =>
        new()
        {
            AllowedSkillCategories = allowedCategories,
            Equipment = carriedItems.Select(item => new WarriorEquipment { Item = item }).ToList()
        };

    [Fact]
    public void SkillEligibility_NoGrantingItem_ReturnsWarriorsOwnCategoriesOnly()
    {
        var warrior = WarriorWith([SkillCategory.Combat, SkillCategory.Strength], new EquipmentItem { Id = 1 });
        Assert.Equal([SkillCategory.Combat, SkillCategory.Strength], SkillEligibility.EffectiveAllowedCategories(warrior));
    }

    [Fact]
    public void SkillEligibility_CarriedNotebook_AddsAcademic()
    {
        var notebook = new EquipmentItem { Id = 1, GrantsSkillCategory = SkillCategory.Academic };
        var warrior = WarriorWith([SkillCategory.Combat], notebook);
        Assert.Equal([SkillCategory.Combat, SkillCategory.Academic], SkillEligibility.EffectiveAllowedCategories(warrior));
    }

    [Fact]
    public void SkillEligibility_AlreadyHasGrantedCategory_NoDuplicate()
    {
        var notebook = new EquipmentItem { Id = 1, GrantsSkillCategory = SkillCategory.Academic };
        var warrior = WarriorWith([SkillCategory.Academic], notebook);
        Assert.Equal([SkillCategory.Academic], SkillEligibility.EffectiveAllowedCategories(warrior));
    }

    [Fact]
    public void SkillEligibility_NoGrantingItem_NoExtraSkillNames()
    {
        var warrior = WarriorWith([], new EquipmentItem { Id = 1 });
        Assert.Empty(SkillEligibility.EffectiveExtraSkillNames(warrior));
    }

    [Fact]
    public void SkillEligibility_CarriedSymbol_GrantsSpecificSkillName()
    {
        var symbol = new EquipmentItem { Id = 1, GrantsSpecificSkillName = "Haggle" };
        var warrior = WarriorWith([SkillCategory.Combat], symbol);
        Assert.Equal(["Haggle"], SkillEligibility.EffectiveExtraSkillNames(warrior));
    }

    // --- RareItemSearchBonus -------------------------------------------------------------------

    [Fact]
    public void RareItemSearchBonus_NoGrantingItem_IsZero()
    {
        var warrior = WarriorWith([], new EquipmentItem { Id = 1 });
        Assert.Equal(0, RareItemSearchBonus.EffectiveBonus(warrior));
    }

    [Fact]
    public void RareItemSearchBonus_OneGrantingItem_ReturnsItsBonus()
    {
        var ruby = new EquipmentItem { Id = 1, GrantsRareItemSearchBonus = 1 };
        var warrior = WarriorWith([], new EquipmentItem { Id = 2 }, ruby);
        Assert.Equal(1, RareItemSearchBonus.EffectiveBonus(warrior));
    }

    [Fact]
    public void RareItemSearchBonus_MultipleGrantingItems_Sums()
    {
        var ruby = new EquipmentItem { Id = 1, GrantsRareItemSearchBonus = 1 };
        var necklace = new EquipmentItem { Id = 2, GrantsRareItemSearchBonus = 1 };
        var warrior = WarriorWith([], ruby, necklace);
        Assert.Equal(2, RareItemSearchBonus.EffectiveBonus(warrior));
    }

    // --- ExplorationDiceBonus --------------------------------------------------------------------

    [Fact]
    public void ExplorationDiceBonus_NoGrantingItem_IsZero()
    {
        var warrior = WarriorWith([], new EquipmentItem { Id = 1 });
        Assert.Equal(0, ExplorationDiceBonus.EffectiveBonusDice([warrior]));
    }

    [Fact]
    public void ExplorationDiceBonus_CarriedEyeOfNumas_ReturnsItsBonus()
    {
        var eye = new EquipmentItem { Id = 1, GrantsBonusExplorationDice = 1 };
        var warrior = WarriorWith([], eye);
        Assert.Equal(1, ExplorationDiceBonus.EffectiveBonusDice([warrior]));
    }

    [Fact]
    public void ExplorationDiceBonus_SumsAcrossMultipleWarriors()
    {
        var eye = new EquipmentItem { Id = 1, GrantsBonusExplorationDice = 1 };
        var bearer = WarriorWith([], eye);
        var other = WarriorWith([], new EquipmentItem { Id = 2 });
        Assert.Equal(1, ExplorationDiceBonus.EffectiveBonusDice([bearer, other]));
    }

    // --- MagicalArtefactTable ---------------------------------------------------------------------

    [Theory]
    [InlineData(1, "Boots and Rope of Pieter")]
    [InlineData(2, "The Count of Ventimiglia's Misericordia")]
    [InlineData(3, "Att'la's Plate Mail")]
    [InlineData(4, "Bow of Seeking")]
    [InlineData(5, "Executioner's Hood")]
    [InlineData(6, "All-seeing Eye of Numas")]
    public void MagicalArtefactTable_RollForItemName_MatchesRulebookTable(int roll, string expectedName)
    {
        Assert.Equal(expectedName, MagicalArtefactTable.RollForItemName(roll));
    }

    [Fact]
    public void MagicalArtefactTable_RollForItemName_OutOfRange_ReturnsNull()
    {
        Assert.Null(MagicalArtefactTable.RollForItemName(0));
        Assert.Null(MagicalArtefactTable.RollForItemName(7));
    }
}
