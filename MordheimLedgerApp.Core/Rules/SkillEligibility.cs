using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Which SkillCategory lists a Warrior can currently pick from at an Advance roll - normally
/// just their static Warrior.AllowedSkillCategories (set at recruitment from the archetype), but some
/// found items permanently expand this while carried (e.g. the Alchemist's Notebook - Laboratoire de
/// l'Alchimiste - grants Academic on top of a Hero's usual lists, see EquipmentItem.
/// GrantsSkillCategory). Computed live from currently-carried equipment rather than baked into
/// AllowedSkillCategories at find time: no "unequip a passive" UI exists yet, but if one ever does, this
/// stays correct automatically instead of needing its own revert logic.</summary>
public static class SkillEligibility
{
    public static List<SkillCategory> EffectiveAllowedCategories(Warrior warrior) =>
        warrior.AllowedSkillCategories
            .Concat(warrior.Equipment
                .Where(e => e.Item.GrantsSkillCategory.HasValue)
                .Select(e => e.Item.GrantsSkillCategory!.Value))
            .Distinct()
            .ToList();

    /// <summary>English Names of specific Skills unlocked by carried equipment regardless of category
    /// (e.g. Merchant's House's Order of Freetraders symbol - Maison du Marchand - unlocks Haggle
    /// specifically, see EquipmentItem.GrantsSpecificSkillName) - narrower than EffectiveAllowedCategories,
    /// which opens a whole list. Resolved to actual Skill ids by the caller (the picker's catalog is
    /// language-specific, this stays locale-agnostic like the rest of Core).</summary>
    public static List<string> EffectiveExtraSkillNames(Warrior warrior) =>
        warrior.Equipment
            .Where(e => e.Item.GrantsSpecificSkillName is not null)
            .Select(e => e.Item.GrantsSpecificSkillName!)
            .Distinct()
            .ToList();
}
