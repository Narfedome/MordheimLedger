using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Un guerrier quitte la bande sans mourir (renvoyé, parti, dépendant sans dose…) : il reste sur la fiche parmi
/// les Mentions honorables, en lecture seule. Son équipement réel quitte la bande avec lui (le livre : il part avec ce qu'il
/// porte) ; la carte n'en garde que le rappel (Warrior.EquipmentAtDeparture). Aucune phrase d'Historique ici.</summary>
public sealed class WarriorDepartureService
{
    private readonly IWarbandService _warbands;

    public WarriorDepartureService(IWarbandService warbands) => _warbands = warbands;

    public async Task RetireAsync(Warrior warrior, OutOfPlayReason reason)
    {
        var carried = warrior.Equipment.Where(e => e.Id > 0).ToList();
        if (carried.Count > 0)
            warrior.EquipmentAtDeparture = string.Join(", ", carried.Select(e => e.NameDisplay));
        foreach (var equipment in carried)
            await _warbands.RemoveWarriorEquipmentAsync(equipment.Id);
        warrior.Equipment.RemoveAll(e => e.Id > 0);
        warrior.Status = WarriorStatus.Retired;
        warrior.OutReason = reason;
        await _warbands.SaveWarriorAsync(warrior);
    }
}
