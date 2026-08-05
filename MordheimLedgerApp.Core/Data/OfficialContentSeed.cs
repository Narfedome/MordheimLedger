using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// First-launch hand-written seed data - just the small common equipment pool shared by every warband
/// (see AppDatabase.SeedOfficialContentAsync). Actual warbands are authored as JSON files under
/// Data/SeedData/*.json (see WarbandSeedData) instead of C# object initializers here, which don't scale
/// past a handful of entries.
///
/// IMPORTANT: this equipment list is from general Mordheim knowledge, not re-extracted from the user's
/// rulebook PDF (too large to read this session) - verify against the rulebook and correct via the
/// Library screens if anything's off. That's exactly what the Official -> Modified flow is for.
/// </summary>
public static class OfficialContentSeed
{
    /// <summary>Name/Description pair, used by the *Fr fields below to carry the French translation
    /// alongside the primary (English) object initializers - kept separate rather than woven into
    /// those initializers so the English seed data stays as easy to scan/edit as before.</summary>
    public readonly record struct Localized(string Name, string? Description);

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
