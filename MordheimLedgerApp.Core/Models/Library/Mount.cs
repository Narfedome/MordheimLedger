namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A mount a Warrior can ride (e.g. "Cheval", "Sanglier de guerre") - has its own stat profile,
/// separate from and not merged with its rider's (matches the tabletop rule: a mounted model is a
/// distinct unit with its own Wounds/save). Rarity/restriction/SpecialRules editing is data-only for
/// this pass (settable via JSON seed, shown for reference) - same precedent as EquipmentItem/Skill,
/// not exposed in the edit dialog UI yet.
/// </summary>
public class Mount
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Cost { get; set; }

    /// <summary>Null = Common (always available). Otherwise the rarity value (2D6 roll needed to find it).</summary>
    public int? Rarity { get; set; }

    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Empty = common to every warband. Non-empty = canon-restricted to these WarbandArchetype
    /// ids (e.g. "Sanglier de guerre" - Orques only). Data-only: shown as reference, not enforced in the
    /// picker - same as EquipmentItem.RestrictedToWarbandArchetypeIds.</summary>
    public List<int> RestrictedToWarbandArchetypeIds { get; set; } = new();

    /// <summary>Mount-specific special rules (e.g. "Charge Furieuse", "Peau Épaisse") - same shared
    /// SpecialRule catalog as WarbandArchetype/WarriorArchetype, seed-only for this pass (no picker UI
    /// on the edit dialog yet).</summary>
    public List<SpecialRule> SpecialRules { get; set; } = new();
}
