using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;

namespace MordheimLedgerApp.Core.Exchange;

/// <summary>Une table de partie jouée telle que l'échange la transporte.</summary>
/// <param name="Parent">Table parente (null pour la bande elle-même), rattachée par ParentColumn.</param>
/// <param name="References">Colonnes qui visent une ligne du catalogue (0 ou null = aucune).</param>
/// <param name="Excluded">Colonnes propres à l'appareil, jamais transportées (remises à leur défaut à l'import).</param>
internal sealed record PlayedTable(Type Type, Type? Parent, string? ParentColumn,
    (string Column, Type Target)[] References, string[] Excluded);

/// <summary>Carte des tables de partie jouée pour l'échange (pendant d'OfficialContentSchema pour le catalogue).
/// À COMPLÉTER à chaque nouvelle table rattachée à une bande ou à un guerrier, ou nouvelle colonne de référence
/// au catalogue - sinon l'export l'oublie, ou recopie un Id local qui ne désigne rien sur l'autre appareil
/// (garde-fou : ExchangeServiceTests.PlayedSchema_CoversEveryIdColumn).</summary>
internal static class PlayedDataSchema
{
    public static readonly PlayedTable Warband = new(typeof(WarbandEntity), null, null,
        [("WarbandArchetypeId", typeof(WarbandArchetypeEntity))],
        // CampaignId vise une campagne de cet appareil (l'identité de la bande voyage dans PlayedRow.ShareId).
        ["CampaignId"]);

    public static readonly PlayedTable[] Tables =
    [
        Warband,
        new(typeof(WarriorEntity), typeof(WarbandEntity), "WarbandId",
        [
            ("WarriorArchetypeId", typeof(WarriorArchetypeEntity)),
            ("HiredSwordId", typeof(HiredSwordEntity)),
            ("DramatisPersonaId", typeof(DramatisPersonaEntity)),
            ("AnimalId", typeof(EquipmentItemEntity)),
            ("EquipmentListId", typeof(EquipmentListEntity)),
        ], []),
        new(typeof(WarbandEquipmentEntity), typeof(WarbandEntity), "WarbandId",
        [
            ("EquipmentItemId", typeof(EquipmentItemEntity)),
            ("MaterialSpecialRuleId", typeof(SpecialRuleEntity)),
        ], []),
        new(typeof(HistoryEntryEntity), typeof(WarbandEntity), "WarbandId", [], []),
        new(typeof(WarbandDramatisPersonaCooldownEntity), typeof(WarbandEntity), "WarbandId",
            [("DramatisPersonaId", typeof(DramatisPersonaEntity))], []),
        new(typeof(WarriorEquipmentEntity), typeof(WarriorEntity), "WarriorId",
        [
            ("EquipmentItemId", typeof(EquipmentItemEntity)),
            ("MaterialSpecialRuleId", typeof(SpecialRuleEntity)),
            ("BlessingSpecialRuleId", typeof(SpecialRuleEntity)),
        ], []),
        new(typeof(WarriorSkillEntity), typeof(WarriorEntity), "WarriorId", [("SkillId", typeof(SkillEntity))], []),
        new(typeof(WarriorInjuryEntity), typeof(WarriorEntity), "WarriorId", [("InjuryId", typeof(InjuryEntity))], []),
        new(typeof(WarriorHatredEntity), typeof(WarriorEntity), "WarriorId",
            [("TargetWarbandArchetypeId", typeof(WarbandArchetypeEntity))], []),
        new(typeof(WarriorSpellEntity), typeof(WarriorEntity), "WarriorId", [("SpellId", typeof(SpellEntity))], []),
        new(typeof(WarriorMutationEntity), typeof(WarriorEntity), "WarriorId", [("MutationId", typeof(MutationEntity))], []),
    ];

    public static IEnumerable<PlayedTable> ChildrenOf(Type parent) => Tables.Where(t => t.Parent == parent);

    public static PlayedTable ByName(string name) => Tables.FirstOrDefault(t => t.Type.Name == name)
        ?? throw new ExchangeFormatException($"Table inconnue : {name}");
}
