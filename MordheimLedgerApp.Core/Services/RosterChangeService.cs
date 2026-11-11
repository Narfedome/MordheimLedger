using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Une recrue (Héros, HeadCount 1) ou un nouveau groupe d'Hommes de main, déjà décidée par le wizard.
/// Equipment : ce que porte CHAQUE modèle.</summary>
public sealed record RecruitDraft(
    WarriorArchetype Archetype,
    string Name,
    int HeadCount,
    IReadOnlyList<(EquipmentItem Item, SpecialRule? Material)> Equipment,
    IReadOnlyList<Mutation> Mutations);

/// <summary>Recrutement, renforts, renvois, ventes et réallocation de la Fin de partie (sortis du wizard le
/// 2026-11-11, même principe qu'AdvancementService) : le wizard décide et calcule les coûts, ce service écrit.
///
/// Convention (confirmée par l'utilisateur le 2026-09-21) : WarriorEquipment.Quantity d'un groupe
/// d'Hommes de main est une quantité PAR MODÈLE - un groupe de 3 armés d'une épée porte « Épée x1 ».</summary>
public sealed class RosterChangeService
{
    private readonly IWarbandService _warbands;

    public RosterChangeService(IWarbandService warbands) => _warbands = warbands;

    /// <summary>Crée la recrue ou le groupe avec son équipement (un exemplaire par modèle) et ses mutations.
    /// Avant le 2026-11-11, un nouveau groupe recevait une quantité égale à son effectif (« Épée x3 »).</summary>
    public async Task<Warrior> RecruitAsync(int warbandId, RecruitDraft draft)
    {
        var warrior = await _warbands.RecruitWarriorAsync(warbandId, draft.Archetype, draft.Name, headCount: Math.Max(1, draft.HeadCount));
        foreach (var (item, material) in draft.Equipment)
            await _warbands.AddWarriorEquipmentAsync(warrior.Id, item, materialRule: material);
        foreach (var mutation in draft.Mutations)
            await _warbands.AddWarriorMutationAsync(warrior.Id, mutation);
        return warrior;
    }

    /// <summary>Vétérans qui rejoignent un groupe existant : ils portent l'équipement du groupe (payé ou pris
    /// dans la réserve par le wizard), donc seul l'effectif change. Avant le 2026-11-11, une ligne
    /// d'équipement supplémentaire était ajoutée au groupe.</summary>
    public async Task TopUpAsync(Warrior group, int addCount)
    {
        group.HeadCount += addCount;
        await _warbands.SaveWarriorAsync(group);
    }

    /// <summary>Renvoi : partiel = l'effectif baisse ; total (ou plus que l'effectif) = l'équipement porté est
    /// retiré (il part dans la réserve, calculée par le wizard) et le guerrier passe Retraité, jamais supprimé
    /// (son historique reste). Renvoie false si rien n'a été renvoyé.</summary>
    public async Task<bool> DismissAsync(Warrior warrior, int count)
    {
        var dismissCount = Math.Min(count, warrior.HeadCount);
        if (dismissCount <= 0) return false;

        if (dismissCount >= warrior.HeadCount)
        {
            foreach (var equipment in warrior.Equipment.Where(e => e.Id > 0).ToList())
                await _warbands.RemoveWarriorEquipmentAsync(equipment.Id);
            warrior.Status = WarriorStatus.Retired;
        }
        else
        {
            warrior.HeadCount -= dismissCount;
        }
        await _warbands.SaveWarriorAsync(warrior);
        return true;
    }

    /// <summary>Vente d'une partie (ou de la totalité) d'une ligne portée : la ligne est recréée au reliquat,
    /// bénédiction comprise.</summary>
    public async Task SellCarriedAsync(WarriorEquipment carried, int soldQuantity)
    {
        await _warbands.RemoveWarriorEquipmentAsync(carried.Id);
        var leftover = carried.Quantity - soldQuantity;
        if (leftover <= 0) return;
        var rest = await _warbands.AddWarriorEquipmentAsync(carried.WarriorId, carried.Item, leftover, carried.MaterialRule, carried.FoundValueOverride);
        if (carried.BlessingRule is { } blessing)
            await _warbands.SetWarriorEquipmentBlessingRuleAsync(rest.Id, blessing.Id);
    }

    /// <summary>Réallocation : warrior.Equipment a été modifié en mémoire (lignes d'origine retirées/réduites,
    /// lignes arrivées d'ailleurs avec un id négatif) - écrit l'écart avec l'état d'origine. Renvoie les lignes
    /// arrivées, pour l'Historique.</summary>
    public async Task<IReadOnlyList<WarriorEquipment>> ApplyEquipmentChangesAsync(Warrior warrior, IReadOnlyDictionary<int, int> originalQuantities)
    {
        foreach (var (id, originalQuantity) in originalQuantities)
        {
            var current = warrior.Equipment.FirstOrDefault(we => we.Id == id);
            if (current is not null && current.Quantity == originalQuantity) continue;

            await _warbands.RemoveWarriorEquipmentAsync(id);
            if (current is { Quantity: > 0 })
                await AddCopyAsync(warrior.Id, current);
        }

        var arrived = warrior.Equipment.Where(we => we.Id < 0).ToList();
        foreach (var line in arrived)
            await AddCopyAsync(warrior.Id, line);
        return arrived;
    }

    /// <summary>Débit (montant négatif) ou crédit de la trésorerie.</summary>
    public async Task AdjustTreasuryAsync(Warband warband, int delta)
    {
        if (delta == 0) return;
        warband.Treasury += delta;
        await _warbands.SaveWarbandAsync(warband);
    }

    private async Task AddCopyAsync(int warriorId, WarriorEquipment line)
    {
        var created = await _warbands.AddWarriorEquipmentAsync(warriorId, line.Item, line.Quantity, line.MaterialRule, line.FoundValueOverride);
        if (line.BlessingRule is { } blessing)
            await _warbands.SetWarriorEquipmentBlessingRuleAsync(created.Id, blessing.Id);
    }
}
