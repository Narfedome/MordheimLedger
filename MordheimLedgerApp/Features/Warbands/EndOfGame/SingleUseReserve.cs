using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Réserve vue par la Fin de partie : un exemplaire de moins par objet pris dans la réserve au début de partie.</summary>
internal static class SingleUseReserve
{
    public static List<WarbandEquipment> WithoutTaken(List<WarbandEquipment> reserve, IReadOnlyList<GameItemUse> uses)
    {
        var taken = uses.Where(u => u.WarbandEquipmentId is not null).GroupBy(u => u.WarbandEquipmentId!.Value).ToDictionary(g => g.Key, g => g.Count());
        if (taken.Count == 0) return reserve;
        foreach (var line in reserve)
            if (taken.TryGetValue(line.Id, out var count)) line.Quantity -= count;
        return reserve.Where(l => l.Quantity > 0).ToList();
    }
}
