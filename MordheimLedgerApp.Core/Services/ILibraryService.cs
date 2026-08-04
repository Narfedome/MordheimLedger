using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>The editable catalog side: warband/warrior archetypes and the equipment Trading Post.</summary>
public interface ILibraryService
{
    Task<List<WarbandArchetype>> GetWarbandArchetypesAsync();
    Task<WarbandArchetype?> GetWarbandArchetypeAsync(int id);
    Task<List<WarriorArchetype>> GetWarriorArchetypesAsync(int warbandArchetypeId);
    Task<List<EquipmentItem>> GetEquipmentItemsAsync();
    Task<List<Skill>> GetSkillsAsync();

    /// <summary>Inserts (Id == 0) or updates. Editing a row whose current Source is Official flips it to Modified.</summary>
    Task SaveWarbandArchetypeAsync(WarbandArchetype archetype);
    Task SaveWarriorArchetypeAsync(WarriorArchetype archetype);
    Task SaveEquipmentItemAsync(EquipmentItem item);
    Task SaveSkillAsync(Skill skill);

    Task DeleteWarbandArchetypeAsync(int warbandArchetypeId);
    Task DeleteWarriorArchetypeAsync(int warriorArchetypeId);
    Task DeleteEquipmentItemAsync(int equipmentItemId);
    Task DeleteSkillAsync(int skillId);
}
