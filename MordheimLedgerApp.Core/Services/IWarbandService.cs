using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>The campaign side: a player's warbands and their rosters, as opposed to the editable Library catalogs.</summary>
public interface IWarbandService
{
    Task<List<Warband>> GetWarbandsAsync();
    Task<Warband?> GetWarbandAsync(int id);
    Task<string> GetWarbandArchetypeNameAsync(int id, string languageCode);

    /// <summary>Pre-fills Treasury from archetype.StartingTreasury.</summary>
    Task<Warband> CreateWarbandAsync(string name, WarbandArchetype archetype);
    Task SaveWarbandAsync(Warband warband);
    Task DeleteWarbandAsync(int warbandId);

    Task<List<Warrior>> GetWarriorsAsync(int warbandId, string languageCode);

    /// <summary>Rulebook "calculate the warband rating" - sum over active (non-Dead) warriors of
    /// (IsLargeCreature ? 20 : 5) + Experience. Lightweight (WarriorEntity columns only, no
    /// equipment/skills/spells joins) so it's cheap to call once per row in a warband list.</summary>
    Task<int> GetWarbandRatingAsync(int warbandId);

    /// <summary>Copies archetype.Cost/stats onto a new Warrior via WarriorArchetype.ToWarrior().</summary>
    /// <param name="headCount">Always 1 for a Hero. For a Henchman group, how many models it starts with
    /// - see Models.Warrior.HeadCount.</param>
    Task<Warrior> RecruitWarriorAsync(int warbandId, WarriorArchetype archetype, string name, int headCount = 1);
    Task SaveWarriorAsync(Warrior warrior);
    Task DeleteWarriorAsync(int warriorId);

    /// <param name="materialRule">Optional material (e.g. "Gromril") chosen for this specific carried
    /// weapon - see WarriorEquipment.MaterialRule.</param>
    Task<WarriorEquipment> AddWarriorEquipmentAsync(int warriorId, EquipmentItem item, int quantity = 1, SpecialRule? materialRule = null);
    Task RemoveWarriorEquipmentAsync(int warriorEquipmentId);

    /// <summary>Equipment held by the warband itself, not yet assigned to a warrior - see
    /// Models.WarbandEquipment. Fed by the End of Game wizard's Exploration step (loot found this way
    /// isn't tied to a specific warrior at the table either) and drained by EquipWarbandItemToWarriorAsync
    /// once the player decides who carries it.</summary>
    Task<List<WarbandEquipment>> GetWarbandEquipmentAsync(int warbandId, string languageCode);

    /// <param name="sellMultiplier">See WarbandEquipment.SellMultiplier - null unless this find is
    /// explicitly called out by the rulebook as sellable at above-market value (e.g. Overturned Cart's
    /// ornate sword/dagger).</param>
    Task<WarbandEquipment> AddWarbandEquipmentAsync(int warbandId, EquipmentItem item, int quantity = 1, SpecialRule? materialRule = null, int? sellMultiplier = null);
    Task RemoveWarbandEquipmentAsync(int warbandEquipmentId);

    /// <summary>Moves an entire WarbandEquipment row (its full Quantity, never a partial split) onto a
    /// warrior - deletes the stash row, creates the equivalent WarriorEquipment row.</summary>
    Task<WarriorEquipment> EquipWarbandItemToWarriorAsync(int warbandEquipmentId, int warriorId);

    /// <summary>Sells an entire WarbandEquipment row (its full Quantity) back for
    /// Item.Cost * Quantity * SellMultiplier gc, credited to the warband's Treasury - only meaningful
    /// for a row with a non-null SellMultiplier (see WarbandEquipment.IsSellable); throws if called on
    /// one without, since the app never lets the player initiate this for an ordinary find (no generic
    /// "sell any equipment" mechanic). Returns the gold amount credited, for the caller's History
    /// sentence/UI feedback.</summary>
    Task<int> SellWarbandItemAsync(int warbandEquipmentId);

    Task<WarriorSkill> AddWarriorSkillAsync(int warriorId, Skill skill);
    Task RemoveWarriorSkillAsync(int warriorSkillId);

    Task<WarriorInjury> AddWarriorInjuryAsync(int warriorId, Injury injury);
    Task RemoveWarriorInjuryAsync(int warriorInjuryId);

    Task<WarriorSpell> AddWarriorSpellAsync(int warriorId, Spell spell);
    Task RemoveWarriorSpellAsync(int warriorSpellId);

    Task<WarriorMutation> AddWarriorMutationAsync(int warriorId, Mutation mutation);
    Task RemoveWarriorMutationAsync(int warriorMutationId);

    /// <summary>Most recent first.</summary>
    Task<List<HistoryEntry>> GetHistoryEntriesAsync(int warbandId);

    /// <summary>Stamps Date = DateTime.Now.</summary>
    Task AddHistoryEntryAsync(int warbandId, string text);
}
