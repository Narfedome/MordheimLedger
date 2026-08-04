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
    /// <summary>Name/Description pair, used by the *Fr fields below to carry the French translation
    /// alongside the primary (English) object initializers - kept separate rather than woven into
    /// those initializers so the English seed data stays as easy to scan/edit as before.</summary>
    public readonly record struct Localized(string Name, string? Description);

    public static WarbandArchetype ReiklanderMercenaries => new()
    {
        Name = "Reiklander Mercenaries",
        Source = ContentSource.Official,
        StartingTreasury = 500,
        MaxWarriors = 15,
        Description = "The classic human mercenary warband of the Empire, recruited in Reikland."
    };

    public static Localized ReiklanderMercenariesFr => new(
        "Mercenaires Reiklander",
        "La bande de mercenaires humains classique de l'Empire, recrutée en Reikland.");

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

    /// <summary>French text for ReiklanderMercenariesWarriors(), matched by array position (Captain,
    /// Champion, Youngblood, Warrior - same order as the method below).</summary>
    public static Localized[] ReiklanderMercenariesWarriorsFr =>
    [
        new("Capitaine Mercenaire", "Peut recevoir n'importe quelle compétence de Combat, Tir ou Force."),
        new("Champion", "Peut recevoir n'importe quelle compétence de Combat ou Force."),
        new("Jeune Loup", "Traité comme un Homme de main jusqu'à sa première Avancée."),
        new("Guerrier", "Groupe d'Hommes de main.")
    ];

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

    /// <summary>French text for CoreEquipment, matched by array position (Dagger, Sword, Hammer, Axe,
    /// Bow, Light Armour, Buckler - same order as the list above).</summary>
    public static Localized[] CoreEquipmentFr =>
    [
        new("Dague", "Octroie une Attaque supplémentaire, toujours utilisée en plus d'une autre arme."),
        new("Épée", null),
        new("Marteau", null),
        new("Hache", null),
        new("Arc", null),
        new("Armure Légère", null),
        new("Rondache", null)
    ];
}
