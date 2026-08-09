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
    Task<WarriorArchetype?> GetWarriorArchetypeAsync(int id, string languageCode);

    /// <summary>Warriors of ANY of these warbands - used by the "restricted to warrior" picker, which
    /// scopes to whichever warband(s) a Skill (etc.) is already restricted to. Empty input = empty
    /// result, no call.</summary>
    Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(IEnumerable<int> warbandArchetypeIds, string languageCode);

    /// <summary>Id+Name only, no SpecialRules/Description resolution - for chip lists (e.g.
    /// WarbandArchetypeDetailDialog's Guerriers tab) that only display a name and fetch the full
    /// WarriorArchetype on tap via GetWarriorArchetypeAsync(id, ...).</summary>
    Task<List<NamedRef>> GetWarriorArchetypeNamesAsync(int warbandArchetypeId, string languageCode);
    Task<List<EquipmentItem>> GetEquipmentItemsAsync(string languageCode);

    /// <summary>Same resolution as GetEquipmentItemsAsync(languageCode), filtered to specific ids at the
    /// SQL level - for resolving a known subset (e.g. one EquipmentList's members) without paying for
    /// the whole Trading Post catalog's translations. Empty input = empty result, no call.</summary>
    Task<List<EquipmentItem>> GetEquipmentItemsAsync(IEnumerable<int> ids, string languageCode);

    /// <summary>This warband's own named starting-equipment lists (see Models.Library.EquipmentList) -
    /// what a WarriorArchetype.EquipmentListId actually points at.</summary>
    Task<List<EquipmentList>> GetEquipmentListsAsync(int warbandArchetypeId, string languageCode);

    /// <summary>Id+Name only - same rationale as GetWarriorArchetypeNamesAsync, for
    /// WarbandArchetypeDetailDialog's Équipement tab (member items resolved lazily on tap, see
    /// GetEquipmentListItemIdsAsync).</summary>
    Task<List<NamedRef>> GetEquipmentListNamesAsync(int warbandArchetypeId, string languageCode);

    /// <summary>Member item ids of one EquipmentList - the starting-list channel consumed by the
    /// roster equipment picker (see EquipmentPickerService), distinct from
    /// EquipmentItem.RestrictedToWarbandArchetypeIds (the Rare/Trading-Post channel).</summary>
    Task<HashSet<int>> GetEquipmentListItemIdsAsync(int equipmentListId);
    Task<List<Skill>> GetSkillsAsync(string languageCode);
    Task<List<Injury>> GetInjuriesAsync(string languageCode);

    /// <summary>Every entry of every spell/prayer/ritual table (see Spell.MagicSchool) - purely
    /// reference data, no in-app casting/rolling ("no rules engine V1").</summary>
    Task<List<Spell>> GetSpellsAsync(string languageCode);

    /// <summary>The shared SpecialRule catalog (e.g. "Chef", "Provoque la Peur") - attached to
    /// WarbandArchetype/WarriorArchetype via join tables rather than duplicated as free text, see
    /// WarbandArchetype.SpecialRules/WarriorArchetype.SpecialRules.</summary>
    Task<List<SpecialRule>> GetSpecialRulesAsync(string languageCode);

    /// <summary>Ids of SpecialRules attached to each of the 4 join tables (WarbandArchetype/
    /// WarriorArchetype/Animal/EquipmentItem) - see LibraryService for details. Kept as 4 separate sets
    /// (not merged) so the Codex can group by actual attachment type and the Warband/Warrior pickers can
    /// filter by it - see SpecialRuleViewModel.</summary>
    Task<(HashSet<int> WarbandRuleIds, HashSet<int> WarriorRuleIds, HashSet<int> AnimalRuleIds, HashSet<int> ItemRuleIds)> GetSpecialRuleAttachmentsAsync();

    /// <summary>The flat global Mutation catalog (see Models.Library.Mutation) - not restricted per
    /// warband, shared verbatim across every Chaos-adjacent band.</summary>
    Task<List<Mutation>> GetMutationsAsync(string languageCode);

    /// <summary>The Animal catalog (see Models.Library.Animal) - has its own stat profile, separate from
    /// EquipmentItem.</summary>
    Task<List<Animal>> GetAnimalsAsync(string languageCode);

    /// <summary>The MagicSchool catalog (e.g. "Nécromancie") - see Models.Library.MagicSchool.</summary>
    Task<List<MagicSchool>> GetMagicSchoolsAsync(string languageCode);

    /// <summary>Inserts (Id == 0) or updates. Editing a row whose current Source is Official flips it
    /// to Modified. Name/Description are written as the translation value for languageCode - any other
    /// language's existing translation is left untouched. Also replaces the band's SpecialRule and
    /// MagicSchool attachment rows (see WarbandArchetype.SpecialRules/MagicSchools) with the current
    /// list - simple replace-all, no diffing.</summary>
    Task SaveWarbandArchetypeAsync(WarbandArchetype archetype, string languageCode);

    /// <summary>Also replaces the archetype's SpecialRule attachment rows (see
    /// WarriorArchetype.SpecialRules) with the current list.</summary>
    Task SaveWarriorArchetypeAsync(WarriorArchetype archetype, string languageCode);

    /// <summary>Also replaces the item's warband-restriction and warrior-restriction rows (see
    /// EquipmentItem.RestrictedToWarbandArchetypeIds/RestrictedToWarriorArchetypeIds) with the current
    /// lists - simple replace-all, no diffing.</summary>
    Task SaveEquipmentItemAsync(EquipmentItem item, string languageCode);

    /// <summary>Also replaces the list's member-item rows (see EquipmentList.ItemIds) with the current
    /// list.</summary>
    Task SaveEquipmentListAsync(EquipmentList list, string languageCode);

    /// <summary>Also replaces the skill's warband-restriction rows (see
    /// Skill.RestrictedToWarbandArchetypeIds) with the current list.</summary>
    Task SaveSkillAsync(Skill skill, string languageCode);
    Task SaveInjuryAsync(Injury injury, string languageCode);
    Task SaveSpellAsync(Spell spell, string languageCode);
    Task SaveSpecialRuleAsync(SpecialRule rule, string languageCode);
    Task SaveMutationAsync(Mutation mutation, string languageCode);
    Task SaveAnimalAsync(Animal animal, string languageCode);
    Task SaveMagicSchoolAsync(MagicSchool school, string languageCode);

    Task DeleteWarbandArchetypeAsync(int warbandArchetypeId);
    Task DeleteWarriorArchetypeAsync(int warriorArchetypeId);
    Task DeleteEquipmentItemAsync(int equipmentItemId);
    Task DeleteEquipmentListAsync(int equipmentListId);
    Task DeleteSkillAsync(int skillId);
    Task DeleteInjuryAsync(int injuryId);
    Task DeleteSpellAsync(int spellId);
    Task DeleteSpecialRuleAsync(int specialRuleId);
    Task DeleteMutationAsync(int mutationId);
    Task DeleteAnimalAsync(int animalId);
    Task DeleteMagicSchoolAsync(int magicSchoolId);
}
