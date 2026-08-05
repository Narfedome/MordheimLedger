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
    /// ids (e.g. "Hache Naine" - Dwarf warbands only). Editable via EquipmentItemEditDialog - not
    /// enforced in the equipment picker though (still shows the full catalog), "no rules engine V1".</summary>
    public List<int> RestrictedToWarbandArchetypeIds { get; set; } = new();
}
