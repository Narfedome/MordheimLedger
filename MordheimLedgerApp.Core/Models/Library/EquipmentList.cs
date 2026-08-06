namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A named starting-equipment list belonging to exactly one warband (e.g. "Skaven Heroes Equipment
/// list", "Marksman Equipment list") - what a WarriorArchetype's Weapons/Armour line actually
/// references in the rulebook, rather than per-item warband restriction. Unlike MagicSchool (which
/// can be granted to several warbands sharing the same tradition), lists aren't shared by name across
/// warbands, so ownership is a direct required WarbandArchetypeId rather than a join/grant.
/// </summary>
public class EquipmentList
{
    public int Id { get; set; }

    public int WarbandArchetypeId { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Translation slot backing Name - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Member EquipmentItem ids - edited via EquipmentListEditDialog's chip picker (reuses
    /// the existing multi-select EquipmentItemSelectorPage), persisted via the EquipmentListItemEntity
    /// join table (see LibraryService.SaveEquipmentListItemsAsync).</summary>
    public List<int> ItemIds { get; set; } = new();
}
