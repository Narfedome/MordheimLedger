using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Core.Services;

/// <summary>
/// Bande d'exemple proposée à la fin de l'accueil (et depuis les Réglages) : des Mercenaires Reiklander
/// recrutés et équipés comme le ferait l'assistant de création en mode Coûts appliqués, pour que les
/// visites guidées aient une vraie fiche à montrer. Tout passe par les ids officiels (cf. OfficialIds) :
/// une bibliothèque où l'une de ces entrées a été supprimée renvoie simplement null plutôt qu'une bande
/// à moitié construite.
/// </summary>
public static class SampleWarband
{
    public const string WarbandOfficialId = "warband.reiklanders";

    /// <summary>Une recrue de l'exemple : héros seul (HeadCount 1) ou groupe d'hommes de main.</summary>
    private sealed record Recruit(string ArchetypeOfficialId, string Name, int HeadCount, string[] EquipmentOfficialIds);

    private static readonly Recruit[] Roster =
    [
        new("warrior.reiklanders.mercenary-captain", "Gunther Hassel", 1, ["equipment.dagger", "equipment.sword", "equipment.light-armour"]),
        new("warrior.reiklanders.champion", "Ulrike Brandt", 1, ["equipment.dagger", "equipment.double-handed-weapon"]),
        new("warrior.reiklanders.youngblood", "Klaus", 1, ["equipment.dagger", "equipment.spear"]),
        new("warrior.reiklanders.warrior", "", 2, ["equipment.dagger", "equipment.axe", "equipment.shield"]),
        new("warrior.reiklanders.marksman", "", 2, ["equipment.dagger", "equipment.bow"]),
    ];

    /// <summary>Crée la bande, ses recrues et leur équipement, et retranche le tout de sa trésorerie de
    /// départ. Retourne null si la bibliothèque n'a plus l'une des entrées officielles nécessaires.</summary>
    /// <param name="henchmanGroupNames">Noms localisés des deux groupes d'hommes de main (Guerriers,
    /// Tireurs), dans l'ordre du roster - les héros gardent leur nom propre quelle que soit la langue.</param>
    public static async Task<Warband?> CreateAsync(IWarbandService warbands, ILibraryService library,
        string languageCode, string warbandName, IReadOnlyList<string> henchmanGroupNames)
    {
        var archetype = (await library.GetWarbandArchetypesAsync(languageCode))
            .FirstOrDefault(a => a.OfficialId == WarbandOfficialId);
        if (archetype == null) return null;

        var warriorArchetypes = (await library.GetWarriorArchetypesAsync(archetype.Id, languageCode))
            .Where(a => a.OfficialId != null)
            .ToDictionary(a => a.OfficialId!);
        var items = (await library.GetEquipmentItemsAsync(languageCode))
            .Where(i => i.OfficialId != null)
            .GroupBy(i => i.OfficialId!)
            .ToDictionary(g => g.Key, g => g.First());

        if (Roster.Any(r => !warriorArchetypes.ContainsKey(r.ArchetypeOfficialId)
                            || r.EquipmentOfficialIds.Any(id => !items.ContainsKey(id))))
            return null;

        var warband = await warbands.CreateWarbandAsync(warbandName, archetype);
        var spent = 0;
        var groupIndex = 0;
        foreach (var recruit in Roster)
        {
            var name = recruit.HeadCount > 1 && groupIndex < henchmanGroupNames.Count
                ? henchmanGroupNames[groupIndex++]
                : recruit.Name;
            var warriorArchetype = warriorArchetypes[recruit.ArchetypeOfficialId];
            var warrior = await warbands.RecruitWarriorAsync(warband.Id, warriorArchetype, name, recruit.HeadCount);
            spent += warriorArchetype.Cost * recruit.HeadCount;

            var carriesFreeDagger = false;
            foreach (var itemId in recruit.EquipmentOfficialIds)
            {
                var item = items[itemId];
                var isFree = EquipmentPricing.IsFreeDaggerEligible(item.IsFreeDagger, carriesFreeDagger);
                carriesFreeDagger |= isFree;
                await warbands.AddWarriorEquipmentAsync(warrior.Id, item);
                spent += EquipmentPricing.CalculateCost(item.Cost, null, isFree) * recruit.HeadCount;
            }
        }

        warband.Treasury = RecruitmentRules.CalculateRemainingTreasury(archetype.StartingTreasury, spent, isExistingWarband: false, treasuryOverride: 0);
        await warbands.SaveWarbandAsync(warband);
        return warband;
    }
}
