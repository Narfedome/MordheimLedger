using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class SpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public string? DescriptionKey { get; set; }
    public ContentSource Source { get; set; }
    public string? ImagePath { get; set; }
    public int? CostMultiplier { get; set; }
    public string? Abbreviation { get; set; }
    public int? Rarity { get; set; }
    public bool IsResaleUpgrade { get; set; }

    /// <summary>Comma-separated WarbandArchetype ids - see SpecialRule.HatredTargetWarbandArchetypeIds.
    /// A plain delimited column rather than a join table: unlike EquipmentItem/Skill restrictions
    /// (genuinely shared catalog rows restricted differently per consumer), a Hatred rule's target list
    /// is intrinsic to what that specific named rule means, and each Hatred-granting rule already has a
    /// distinct name per source (no two contexts share one catalog row with different targets).</summary>
    public string? HatredTargetWarbandArchetypeIds { get; set; }

    /// <summary>See SpecialRule.HatredTargetsSpellcasters.</summary>
    public bool HatredTargetsSpellcasters { get; set; }
}
