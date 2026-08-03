using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>The campaign side: a player's warbands and their rosters, as opposed to the editable Library catalogs.</summary>
public interface IWarbandService
{
    Task<List<Warband>> GetWarbandsAsync();
    Task<Warband?> GetWarbandAsync(int id);

    /// <summary>Pre-fills Treasury from archetype.StartingTreasury.</summary>
    Task<Warband> CreateWarbandAsync(string name, WarbandArchetype archetype);
    Task SaveWarbandAsync(Warband warband);
    Task DeleteWarbandAsync(int warbandId);

    Task<List<Warrior>> GetWarriorsAsync(int warbandId);

    /// <summary>Copies archetype.Cost/stats onto a new Warrior via WarriorArchetype.ToWarrior().</summary>
    Task<Warrior> RecruitWarriorAsync(int warbandId, WarriorArchetype archetype, string name);
    Task SaveWarriorAsync(Warrior warrior);
    Task DeleteWarriorAsync(int warriorId);
}
