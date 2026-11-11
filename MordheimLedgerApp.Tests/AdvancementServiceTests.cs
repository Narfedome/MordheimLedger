using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Progressions et promotion d'un Homme de main (« The lad's got talent ») appliquées en base -
/// non-régression des bugs trouvés en recette le 2026-11-11 (XP perdue, équipement perdu). Base fraîchement
/// seedée par test, comme WarbandMutationTests.</summary>
public class AdvancementServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly AdvancementService _advancement;

    public AdvancementServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _advancement = new AdvancementService(_warbands);
    }

    public void Dispose()
    {
        // Le seed démarre dès la création de la base : un test qui n'y touche pas (ex. photos) fermerait sinon
        // le fichier en plein seed (« utilisé par un autre processus » au moment de le supprimer).
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* le test l'a déjà signalé */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    /// <summary>Bande Reiklander avec un groupe de 3 Hommes de main armés d'une Hache et d'un Bouclier, qui a
    /// déjà 4 XP et vient d'en gagner 2 à cette partie (6 au moment de l'enregistrement).</summary>
    private async Task<(Warband Warband, Warrior Group)> GroupAsync(int headCount = 3)
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Reiklander Mercenaries");
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);
        var henchmanArchetype = (await _library.GetWarriorArchetypesAsync(archetype.Id, "en")).First(a => !a.IsHero);
        var group = await _warbands.RecruitWarriorAsync(warband.Id, henchmanArchetype, "Reik Blades", headCount);
        var items = await _library.GetEquipmentItemsAsync("en");
        await _warbands.AddWarriorEquipmentAsync(group.Id, items.First(i => i.Name == "Axe"));
        await _warbands.AddWarriorEquipmentAsync(group.Id, items.First(i => i.Name == "Shield"));
        group.Experience = 6;
        await _warbands.SaveWarriorAsync(group);
        return (warband, await ReloadAsync(warband, group.Id));
    }

    private async Task<Warrior> ReloadAsync(Warband warband, int warriorId) =>
        (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == warriorId);

    private static Warrior Draft(Warrior group, string name, params SkillCategory[] lists)
    {
        var draft = group.CloneAsPromotedHero(name);
        draft.AllowedSkillCategories = lists.ToList();
        return draft;
    }

    [Fact]
    public async Task Promotion_HeroKeepsTheGroupsCurrentExperience()
    {
        var (warband, group) = await GroupAsync();
        var draft = Draft(group, "Otto", SkillCategory.Combat, SkillCategory.Strength);
        group.Experience = 8; // XP ajoutée au groupe APRÈS la construction du brouillon (cas du bug)

        var hero = await _advancement.PromoteHenchmanAsync(group, draft, AdvanceResult.None, null);

        Assert.Equal(8, (await ReloadAsync(warband, hero.Id)).Experience);
    }

    [Fact]
    public async Task Promotion_HeroIsAHeroWithTheTwoChosenListsAndTheSameType()
    {
        var (warband, group) = await GroupAsync();

        var hero = await _advancement.PromoteHenchmanAsync(group, Draft(group, "Otto", SkillCategory.Combat, SkillCategory.Strength), AdvanceResult.None, null);

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.True(reloaded.IsHero);
        Assert.Equal(1, reloaded.HeadCount);
        Assert.Equal(group.WarriorArchetypeId, reloaded.WarriorArchetypeId); // une Goule reste une Goule
        Assert.Equal([SkillCategory.Combat, SkillCategory.Strength], reloaded.AllowedSkillCategories.OrderBy(c => c));
    }

    [Fact]
    public async Task Promotion_HeroGetsACopyOfTheGroupsEquipment_AndTheGroupKeepsIts()
    {
        var (warband, group) = await GroupAsync();

        var hero = await _advancement.PromoteHenchmanAsync(group, Draft(group, "Otto", SkillCategory.Combat, SkillCategory.Strength), AdvanceResult.None, null);
        await _warbands.SaveWarriorAsync(group);

        var heroGear = (await ReloadAsync(warband, hero.Id)).Equipment.Select(e => e.Item.Name).OrderBy(n => n);
        var groupGear = (await ReloadAsync(warband, group.Id)).Equipment.Select(e => e.Item.Name).OrderBy(n => n);
        Assert.Equal(["Axe", "Shield"], heroGear);
        Assert.Equal(["Axe", "Shield"], groupGear);
    }

    [Fact]
    public async Task Promotion_GroupLosesOneModel_AndAppliesTheRemainderRoll()
    {
        var (warband, group) = await GroupAsync(headCount: 3);
        var weaponSkill = group.WeaponSkill;

        await _advancement.PromoteHenchmanAsync(group, Draft(group, "Otto", SkillCategory.Combat, SkillCategory.Strength),
            AdvanceResult.None, new AdvanceResult([], null, CharacteristicField.WeaponSkill));
        await _warbands.SaveWarriorAsync(group);

        var reloaded = await ReloadAsync(warband, group.Id);
        Assert.Equal(2, reloaded.HeadCount);
        Assert.Equal(weaponSkill + 1, reloaded.WeaponSkill);
        Assert.Contains(CharacteristicField.WeaponSkill, reloaded.IncreasedCharacteristics);
    }

    [Fact]
    public async Task Promotion_HeroAdvance_SkillAndCharacteristicAreApplied()
    {
        var (warband, group) = await GroupAsync();
        var strikeToInjure = (await _library.GetSkillsAsync("en")).First(s => s.Category == SkillCategory.Combat);
        var ballisticSkill = group.BallisticSkill;

        var hero = await _advancement.PromoteHenchmanAsync(group, Draft(group, "Otto", SkillCategory.Combat, SkillCategory.Shooting),
            new AdvanceResult([strikeToInjure], null, CharacteristicField.BallisticSkill), null);

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal(strikeToInjure.Id, Assert.Single(reloaded.Skills).Item.Id);
        Assert.Equal(ballisticSkill + 1, reloaded.BallisticSkill);
        Assert.Empty(reloaded.IncreasedCharacteristics); // « jamais deux fois » ne concerne que les Hommes de main
    }

    [Fact]
    public async Task Promotion_OfTheLastModel_KeepsTheEquipmentOnTheHeroOnceTheGroupIsDeleted()
    {
        var (warband, group) = await GroupAsync(headCount: 1);

        var hero = await _advancement.PromoteHenchmanAsync(group, Draft(group, "Hans", SkillCategory.Shooting, SkillCategory.Speed), AdvanceResult.None, null);
        Assert.Equal(0, group.HeadCount);
        await _warbands.DeleteWarriorAsync(group.Id); // ce que fait le wizard quand le groupe est vide

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var reloaded = Assert.Single(roster);
        Assert.Equal(hero.Id, reloaded.Id);
        Assert.Equal(["Axe", "Shield"], reloaded.Equipment.Select(e => e.Item.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Advance_OnHenchmen_RemembersTheIncreasedCharacteristic()
    {
        var (warband, group) = await GroupAsync();

        await _advancement.ApplyAdvanceAsync(group, new AdvanceResult([], null, CharacteristicField.Initiative));
        await _warbands.SaveWarriorAsync(group);

        var reloaded = await ReloadAsync(warband, group.Id);
        Assert.Contains(CharacteristicField.Initiative, reloaded.IncreasedCharacteristics);
    }

    // --- Ajouts après le passage Stryker du 2026-11-11 (mutants survivants) ---

    [Theory]
    [InlineData(CharacteristicField.Movement)]
    [InlineData(CharacteristicField.WeaponSkill)]
    [InlineData(CharacteristicField.BallisticSkill)]
    [InlineData(CharacteristicField.Strength)]
    [InlineData(CharacteristicField.Toughness)]
    [InlineData(CharacteristicField.Wounds)]
    [InlineData(CharacteristicField.Initiative)]
    [InlineData(CharacteristicField.Attacks)]
    [InlineData(CharacteristicField.Leadership)]
    public void CharacteristicIncrease_AddsOneToTheRightField(CharacteristicField field)
    {
        var warrior = new Warrior { Movement = 4, WeaponSkill = 3, BallisticSkill = 3, Strength = 3, Toughness = 3, Wounds = 1, Initiative = 3, Attacks = 1, Leadership = 7 };
        int Read(Warrior w) => field switch
        {
            CharacteristicField.Movement => w.Movement, CharacteristicField.WeaponSkill => w.WeaponSkill,
            CharacteristicField.BallisticSkill => w.BallisticSkill, CharacteristicField.Strength => w.Strength,
            CharacteristicField.Toughness => w.Toughness, CharacteristicField.Wounds => w.Wounds,
            CharacteristicField.Initiative => w.Initiative, CharacteristicField.Attacks => w.Attacks,
            _ => w.Leadership,
        };
        var before = Read(warrior);
        var totalBefore = warrior.Movement + warrior.WeaponSkill + warrior.BallisticSkill + warrior.Strength + warrior.Toughness
            + warrior.Wounds + warrior.Initiative + warrior.Attacks + warrior.Leadership;

        AdvancementService.ApplyCharacteristicIncrease(warrior, field);

        Assert.Equal(before + 1, Read(warrior));
        Assert.Equal(totalBefore + 1, warrior.Movement + warrior.WeaponSkill + warrior.BallisticSkill + warrior.Strength + warrior.Toughness
            + warrior.Wounds + warrior.Initiative + warrior.Attacks + warrior.Leadership); // et rien d'autre ne bouge
    }

    [Fact]
    public async Task Promotion_CopiesTheBlessingOfTheGroupsWeapon()
    {
        var (warband, group) = await GroupAsync();
        var blessed = (await _library.GetSpecialRulesAsync("en")).Single(r => r.Name == "Blessed Weapon");
        await _warbands.SetWarriorEquipmentBlessingRuleAsync(group.Equipment.First(e => e.Item.Name == "Axe").Id, blessed.Id);
        group = await ReloadAsync(warband, group.Id);

        var hero = await _advancement.PromoteHenchmanAsync(group, Draft(group, "Otto", SkillCategory.Combat, SkillCategory.Strength), AdvanceResult.None, null);

        var axe = (await ReloadAsync(warband, hero.Id)).Equipment.Single(e => e.Item.Name == "Axe");
        Assert.Equal("Blessed Weapon", axe.BlessingRule?.Name);
    }
}
