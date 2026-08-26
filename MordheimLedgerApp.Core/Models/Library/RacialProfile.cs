namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A creature body type's characteristic maximums (Human, Dwarf, Skaven, Orc, Ghoul, Rat Ogre...) -
/// WarriorArchetype.RacialProfileId points to one. Distinct from Race (which classifies a whole
/// WarbandArchetype, one per band) because several warbands mix multiple creature bodies under one
/// roster (e.g. Undead: Vampire/Ghoul/Zombie/Dire Wolf each need their own maximums). Same
/// user-editable Library catalog shape as Race/MagicSchool (Official/Modified/Custom).
///
/// Field names deliberately match WarriorArchetype/Warrior's own stat names (WeaponSkill, not
/// MaxWeaponSkill) rather than being prefixed - within this catalog "the number" always means "the
/// maximum for this creature type", so the prefix would be redundant, and it lets StatRowView (which
/// binds weakly against WeaponSkill/BallisticSkill/.../MovementDisplay) render/edit this profile's grid
/// with zero changes, the same way it already serves WarriorArchetype/Warrior/Animal profiles.
/// </summary>
public class RacialProfile
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Non-null overrides the displayed Movement maximum with free text (e.g. "2D6" for Cave
    /// Squigs) - mirrors WarriorArchetype.MovementOverride. Movement itself stays a numeric fallback.</summary>
    public int Movement { get; set; }
    public string? MovementOverride { get; set; }
    public string MovementDisplay => MovementOverride ?? Movement.ToString();

    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }
}
