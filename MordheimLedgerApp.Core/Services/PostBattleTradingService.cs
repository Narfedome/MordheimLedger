using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Une ligne de la réserve de bande telle que le wizard la calcule. SourceId = id de la ligne
/// WarbandEquipment d'origine, null pour un apport de cette partie (Exploration, scénario, achat…).</summary>
public sealed record StashLine(int? SourceId, EquipmentItem Item, SpecialRule? MaterialRule, int Quantity, int? FoundValueOverride = null);

/// <summary>Comment un personnage spécial trouvé à la recherche d'objets rares est payé.</summary>
public enum PersonaPayment
{
    /// <summary>Sans frais (Bertha) ou payé en remettant un objet de la réserve (déjà retiré par le calcul de la réserve).</summary>
    None,
    Gold,
    Wyrdstone,
}

public sealed record PersonaRecruitment(DramatisPersona Persona, PersonaPayment Payment, int Amount);

/// <summary>Écritures de la réserve et de la recherche d'objets rares en fin de partie (sorties du wizard le
/// 2026-11-11, même principe qu'AdvancementService) : le wizard décide, ce service applique.</summary>
public sealed class PostBattleTradingService
{
    private readonly IWarbandService _warbands;

    public PostBattleTradingService(IWarbandService warbands) => _warbands = warbands;

    /// <summary>Seule écriture de la réserve de la Fin de partie. Ligne d'origine : inchangée = rien, réduite =
    /// recréée au reliquat, disparue = supprimée. Apports de la partie : fusionnés par (objet, matériau, valeur
    /// trouvée) entre eux ET avec une ligne d'origine identique restée dans la réserve (une seule ligne
    /// « Épée ×2 » plutôt que deux lignes « Épée », recette du 2026-11-11) - l'origine ne sert que pendant le
    /// wizard.</summary>
    public async Task ApplyReserveAsync(int warbandId, IReadOnlyList<StashLine> original, IReadOnlyList<StashLine> final)
    {
        static (int, int?, int?) KeyOf(StashLine l) => (l.Item.Id, l.MaterialRule?.Id, l.FoundValueOverride);

        var added = final.Where(l => l.SourceId is null).GroupBy(KeyOf).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var line in original)
        {
            var finalQuantity = final.FirstOrDefault(l => l.SourceId == line.SourceId)?.Quantity ?? 0;
            var mergeWith = finalQuantity > 0 && added.Remove(KeyOf(line), out var gained) ? gained : null;
            if (mergeWith is null && finalQuantity == line.Quantity) continue;

            var total = finalQuantity + (mergeWith?.Sum(l => l.Quantity) ?? 0);
            await _warbands.RemoveWarbandEquipmentAsync(line.SourceId!.Value);
            if (total > 0)
                await _warbands.AddWarbandEquipmentAsync(warbandId, line.Item, total, line.MaterialRule, line.FoundValueOverride);
        }

        foreach (var group in added.Values)
        {
            var first = group[0];
            await _warbands.AddWarbandEquipmentAsync(warbandId, first.Item, group.Sum(l => l.Quantity), first.MaterialRule, first.FoundValueOverride);
        }
    }

    /// <summary>Objets rares achetés (le prix, marchandage déjà déduit, est débité ; l'objet entre dans la réserve
    /// avec les autres gains) et personnages spéciaux recrutés avec leur équipement de départ - le partenaire
    /// d'une paire (Ulli et Marquand) est recruté avec lui, sans frais de plus.</summary>
    public async Task ApplyRareItemSearchAsync(Warband warband, IEnumerable<int> itemCosts, IEnumerable<PersonaRecruitment> recruitments,
        IReadOnlyList<EquipmentItem> catalog)
    {
        warband.Treasury -= itemCosts.Sum();
        var list = recruitments.ToList();
        foreach (var recruitment in list)
        {
            if (recruitment.Payment == PersonaPayment.Gold) warband.Treasury -= recruitment.Amount;
            else if (recruitment.Payment == PersonaPayment.Wyrdstone) warband.WyrdstoneShards -= recruitment.Amount;
        }
        await _warbands.SaveWarbandAsync(warband);

        foreach (var recruitment in list)
        {
            await RecruitAsync(warband.Id, recruitment.Persona, catalog);
            if (recruitment.Persona.PairedWithDramatisPersona is { } partner)
                await RecruitAsync(warband.Id, partner, catalog);
        }
    }

    /// <summary>Équipement de départ résolu id par id : un objet présent deux fois (Bertha, deux marteaux) reste
    /// en double, regroupé ensuite en une ligne de quantité 2 par RecruitDramatisPersonaAsync.</summary>
    private async Task RecruitAsync(int warbandId, DramatisPersona persona, IReadOnlyList<EquipmentItem> catalog)
    {
        var equipment = persona.StartingEquipmentIds
            .Select(id => catalog.FirstOrDefault(e => e.Id == id))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
        await _warbands.RecruitDramatisPersonaAsync(warbandId, persona, persona.Name, equipment, persona.Skills);
    }
}
