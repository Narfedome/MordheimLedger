namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A Trading Post catalog entry. Seeded from the rulebook but fully editable/creatable by the
/// player (see roadmap: no closed content system) — IsCustom just flags the badge shown in the UI.
/// </summary>
public class EquipmentItem
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync/SetTranslationAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public EquipmentCategory Category { get; set; }
    public int Cost { get; set; }

    /// <summary>Null = Common (always available). Otherwise the rarity value (2D6 roll needed to find it).</summary>
    public int? Rarity { get; set; }

    public string? Description { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Empty = common to every warband. Non-empty = canon-restricted to these WarbandArchetype
    /// ids (e.g. "Hache Naine" - Dwarf warbands only). This is the Rare/Trading-Post acquisition
    /// channel (bought any time, not tied to a starting EquipmentList) - see EquipmentList for the
    /// other, starting-list channel. Editable via EquipmentItemEditDialog.</summary>
    public List<int> RestrictedToWarbandArchetypeIds { get; set; } = new();

    /// <summary>Empty = every warrior of the restricted warband(s) (or every member of whichever
    /// EquipmentList(s) this item belongs to) can pick it. Non-empty = further restricted to specific
    /// WarriorArchetype ids (e.g. Averlanders' "Long bow" - Bergjaeger only, despite being in the
    /// shared Scout list; Middenheim's "Wolfcloak" - Middenheim Heroes only). Seed-only for now, same
    /// precedent as Skill.RestrictedToWarriorArchetypeIds - no EquipmentItemEditDialog UI.</summary>
    public List<int> RestrictedToWarriorArchetypeIds { get; set; } = new();
}
