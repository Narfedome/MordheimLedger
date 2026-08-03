using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// First-launch seed data. Deliberately small (one warband) to prove the recruit-with-pre-fill
/// pipeline end to end rather than transcribing the whole rulebook up front.
///
/// IMPORTANT: these stat lines/costs are from general Mordheim knowledge, not re-extracted from the
/// user's rulebook PDF (too large to read this session) or the Broheim/Grande Librairie sites (their
/// content renders as images, not extractable text) — verify against the rulebook and correct via the
/// Library screens if anything's off. That's exactly what the Official -> Modified flow is for.
/// </summary>
public static class OfficialContentSeed
{
    public static WarbandArchetype ReiklanderMercenaries => new()
    {
        Name = "Reiklander Mercenaries",
        Source = ContentSource.Official,
        StartingTreasury = 500,
        MaxWarriors = 15,
        Description = "The classic human mercenary warband of the Empire, recruited in Reikland."
    };

    public static List<WarriorArchetype> ReiklanderMercenariesWarriors(int warbandArchetypeId) => new()
    {
        new()
        {
            WarbandArchetypeId = warbandArchetypeId,
            Name = "Mercenary Captain",
            IsHero = true,
            Cost = 80,
            MaxCount = 1,
            Source = ContentSource.Official,
            Movement = 4, WeaponSkill = 4, BallisticSkill = 3, Strength = 3, Toughness = 3,
            Wounds = 1, Initiative = 4, Attacks = 1, Leadership = 8,
            Description = "May be given any Combat, Shooting, or Strength skill."
        },
        new()
        {
            WarbandArchetypeId = warbandArchetypeId,
            Name = "Champion",
            IsHero = true,
            Cost = 35,
            MaxCount = 2,
            Source = ContentSource.Official,
            Movement = 4, WeaponSkill = 4, BallisticSkill = 3, Strength = 3, Toughness = 3,
            Wounds = 1, Initiative = 3, Attacks = 1, Leadership = 7,
            Description = "May be given any Combat or Strength skill."
        },
        new()
        {
            WarbandArchetypeId = warbandArchetypeId,
            Name = "Youngblood",
            IsHero = true,
            Cost = 15,
            MaxCount = 2,
            Source = ContentSource.Official,
            Movement = 4, WeaponSkill = 2, BallisticSkill = 2, Strength = 3, Toughness = 3,
            Wounds = 1, Initiative = 3, Attacks = 1, Leadership = 7,
            Description = "Treated as a Henchman until it earns its first Advance."
        },
        new()
        {
            WarbandArchetypeId = warbandArchetypeId,
            Name = "Warrior",
            IsHero = false,
            Cost = 25,
            MaxCount = null,
            Source = ContentSource.Official,
            Movement = 4, WeaponSkill = 3, BallisticSkill = 3, Strength = 3, Toughness = 3,
            Wounds = 1, Initiative = 3, Attacks = 1, Leadership = 7,
            Description = "Henchman group."
        }
    };

    public static List<EquipmentItem> CoreEquipment => new()
    {
        new() { Name = "Dagger", Category = EquipmentCategory.MeleeWeapon, Cost = 0, Source = ContentSource.Official,
            Description = "Grants one extra Attack, always used in addition to another weapon." },
        new() { Name = "Sword", Category = EquipmentCategory.MeleeWeapon, Cost = 10, Source = ContentSource.Official },
        new() { Name = "Hammer", Category = EquipmentCategory.MeleeWeapon, Cost = 3, Source = ContentSource.Official },
        new() { Name = "Axe", Category = EquipmentCategory.MeleeWeapon, Cost = 5, Source = ContentSource.Official },
        new() { Name = "Bow", Category = EquipmentCategory.MissileWeapon, Cost = 10, Source = ContentSource.Official },
        new() { Name = "Light Armour", Category = EquipmentCategory.Armour, Cost = 20, Source = ContentSource.Official },
        new() { Name = "Buckler", Category = EquipmentCategory.Armour, Cost = 5, Source = ContentSource.Official }
    };
}
