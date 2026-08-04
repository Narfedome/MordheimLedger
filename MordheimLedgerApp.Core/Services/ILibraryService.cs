using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>The editable catalog side: warband/warrior archetypes and the equipment Trading Post.
/// Name/Description are resolved translations - every method takes the caller's current UI language
/// code (see MordheimLedgerApp.Services.LocalizationService.Language) so Core stays free of any MAUI
/// dependency while still being able to resolve/write per-language text.</summary>
public interface ILibraryService
{
    Task<List<WarbandArchetype>> GetWarbandArchetypesAsync(string languageCode);
    Task<WarbandArchetype?> GetWarbandArchetypeAsync(int id, string languageCode);
    Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(int warbandArchetypeId, string languageCode);
    Task<List<EquipmentItem>> GetEquipmentItemsAsync(string languageCode);
    Task<List<Skill>> GetSkillsAsync(string languageCode);
    Task<List<Injury>> GetInjuriesAsync(string languageCode);

    /// <summary>Every entry of every spell/prayer/ritual table (see Spell.SpellListName) - purely
    /// reference data, no in-app casting/rolling ("no rules engine V1").</summary>
    Task<List<Spell>> GetSpellsAsync(string languageCode);

    /// <summary>Inserts (Id == 0) or updates. Editing a row whose current Source is Official flips it
    /// to Modified. Name/Description are written as the translation value for languageCode - any other
    /// language's existing translation is left untouched.</summary>
    Task SaveWarbandArchetypeAsync(WarbandArchetype archetype, string languageCode);
    Task SaveWarriorArchetypeAsync(WarriorArchetype archetype, string languageCode);

    /// <summary>Also replaces the item's warband-restriction rows (see
    /// EquipmentItem.RestrictedToWarbandArchetypeIds) with the current list - simple replace-all, no
    /// diffing.</summary>
    Task SaveEquipmentItemAsync(EquipmentItem item, string languageCode);

    /// <summary>Also replaces the skill's warband-restriction rows (see
    /// Skill.RestrictedToWarbandArchetypeIds) with the current list.</summary>
    Task SaveSkillAsync(Skill skill, string languageCode);
    Task SaveInjuryAsync(Injury injury, string languageCode);
    Task SaveSpellAsync(Spell spell, string languageCode);

    Task DeleteWarbandArchetypeAsync(int warbandArchetypeId);
    Task DeleteWarriorArchetypeAsync(int warriorArchetypeId);
    Task DeleteEquipmentItemAsync(int equipmentItemId);
    Task DeleteSkillAsync(int skillId);
    Task DeleteInjuryAsync(int injuryId);
    Task DeleteSpellAsync(int spellId);
}
