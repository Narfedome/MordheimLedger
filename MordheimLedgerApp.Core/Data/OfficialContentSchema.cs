using MordheimLedgerApp.Core.Data.Entities.Library;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Une table du catalogue synchronisée par OfficialId (voir AppDatabase.SyncOfficialContentAsync).
/// Les colonnes qui ne sont pas listées ici sont des valeurs simples, recopiées telles quelles.</summary>
/// <param name="TranslationColumns">Clés de TranslationEntity (Name/Description...).</param>
/// <param name="ForeignKeys">Id d'une autre ligne du catalogue (0 ou null = aucune).</param>
/// <param name="CsvForeignKeys">Liste d'id séparés par des virgules.</param>
internal sealed record CatalogTable(Type Type, string[] TranslationColumns,
    (string Column, Type Target)[] ForeignKeys, (string Column, Type Target)[] CsvForeignKeys);

/// <summary>Table de jointure entre deux lignes du catalogue, rattachée à son PROPRIÉTAIRE : la ligne dont
/// l'éditeur réécrit la jointure à la sauvegarde (LibraryService.SaveXxxAsync) - donc celle qui passe en
/// Modifié si le joueur y touche. La synchro ne réécrit la jointure que d'un propriétaire resté Officiel.</summary>
internal sealed record OwnedJoin(Type Type, string OwnerColumn, Type Owner, string OtherColumn, Type Other);

/// <summary>Carte du contenu officiel synchronisable. L'Exploration n'y figure pas : elle est reconstruite
/// en entier depuis le JSON à chaque lancement (AppDatabase.ResyncExplorationResultsAsync).
/// À COMPLÉTER à chaque nouvelle table ou colonne de référence du catalogue - sinon la synchro recopie un
/// id de la seed.db3 tel quel, sans le traduire vers l'id local.</summary>
internal static class OfficialContentSchema
{
    private static readonly string[] NameAndDescription = ["NameKey", "DescriptionKey"];

    public static readonly CatalogTable[] Tables =
    [
        new(typeof(RaceEntity), NameAndDescription, [], []),
        new(typeof(RacialProfileEntity), NameAndDescription, [], []),
        new(typeof(WarbandArchetypeEntity), NameAndDescription, [("RaceId", typeof(RaceEntity))], []),
        new(typeof(MagicSchoolEntity), NameAndDescription, [], []),
        new(typeof(SpellEntity), NameAndDescription, [("MagicSchoolId", typeof(MagicSchoolEntity))], []),
        new(typeof(SpecialRuleEntity), NameAndDescription, [], [("HatredTargetWarbandArchetypeIds", typeof(WarbandArchetypeEntity))]),
        new(typeof(SkillEntity), NameAndDescription, [], [("HatredTargetWarbandArchetypeIds", typeof(WarbandArchetypeEntity))]),
        new(typeof(MutationEntity), NameAndDescription, [], []),
        new(typeof(EquipmentItemEntity), NameAndDescription, [], []),
        new(typeof(EquipmentListEntity), ["NameKey"], [("WarbandArchetypeId", typeof(WarbandArchetypeEntity))], []),
        new(typeof(WarriorArchetypeEntity), NameAndDescription,
        [
            ("WarbandArchetypeId", typeof(WarbandArchetypeEntity)),
            ("EquipmentListId", typeof(EquipmentListEntity)),
            ("RacialProfileId", typeof(RacialProfileEntity)),
        ], []),
        new(typeof(InjuryEntity), NameAndDescription, [], []),
        new(typeof(HiredSwordEntity), NameAndDescription, [("MagicSchoolId", typeof(MagicSchoolEntity))], []),
        new(typeof(DramatisPersonaEntity), ["NameKey", "DescriptionKey", "PairDescriptionKey"],
        [
            ("PairedWithDramatisPersonaId", typeof(DramatisPersonaEntity)),
            ("MagicSchoolId", typeof(MagicSchoolEntity)),
            ("AlternativePaymentItemId", typeof(EquipmentItemEntity)),
        ], []),
    ];

    public static readonly OwnedJoin[] Joins =
    [
        new(typeof(WarbandArchetypeSpecialRuleEntity), "WarbandArchetypeId", typeof(WarbandArchetypeEntity), "SpecialRuleId", typeof(SpecialRuleEntity)),
        new(typeof(WarbandArchetypeMagicSchoolEntity), "WarbandArchetypeId", typeof(WarbandArchetypeEntity), "MagicSchoolId", typeof(MagicSchoolEntity)),
        new(typeof(WarriorArchetypeSpecialRuleEntity), "WarriorArchetypeId", typeof(WarriorArchetypeEntity), "SpecialRuleId", typeof(SpecialRuleEntity)),
        new(typeof(EquipmentItemSpecialRuleEntity), "EquipmentItemId", typeof(EquipmentItemEntity), "SpecialRuleId", typeof(SpecialRuleEntity)),
        new(typeof(WarbandArchetypeEquipmentEntity), "EquipmentItemId", typeof(EquipmentItemEntity), "WarbandArchetypeId", typeof(WarbandArchetypeEntity)),
        new(typeof(WarriorArchetypeEquipmentEntity), "EquipmentItemId", typeof(EquipmentItemEntity), "WarriorArchetypeId", typeof(WarriorArchetypeEntity)),
        new(typeof(EquipmentListItemEntity), "EquipmentListId", typeof(EquipmentListEntity), "EquipmentItemId", typeof(EquipmentItemEntity)),
        new(typeof(WarbandArchetypeSkillEntity), "SkillId", typeof(SkillEntity), "WarbandArchetypeId", typeof(WarbandArchetypeEntity)),
        new(typeof(WarriorArchetypeSkillEntity), "SkillId", typeof(SkillEntity), "WarriorArchetypeId", typeof(WarriorArchetypeEntity)),
        new(typeof(WarbandArchetypeMutationEntity), "MutationId", typeof(MutationEntity), "WarbandArchetypeId", typeof(WarbandArchetypeEntity)),
        new(typeof(InjurySpecialRuleEntity), "InjuryId", typeof(InjuryEntity), "SpecialRuleId", typeof(SpecialRuleEntity)),
        new(typeof(WarbandArchetypeHiredSwordEntity), "HiredSwordId", typeof(HiredSwordEntity), "WarbandArchetypeId", typeof(WarbandArchetypeEntity)),
        new(typeof(HiredSwordEquipmentEntity), "HiredSwordId", typeof(HiredSwordEntity), "EquipmentItemId", typeof(EquipmentItemEntity)),
        new(typeof(HiredSwordSpecialRuleEntity), "HiredSwordId", typeof(HiredSwordEntity), "SpecialRuleId", typeof(SpecialRuleEntity)),
        new(typeof(WarbandArchetypeDramatisPersonaEntity), "DramatisPersonaId", typeof(DramatisPersonaEntity), "WarbandArchetypeId", typeof(WarbandArchetypeEntity)),
        new(typeof(DramatisPersonaSpecialRuleEntity), "DramatisPersonaId", typeof(DramatisPersonaEntity), "SpecialRuleId", typeof(SpecialRuleEntity)),
        new(typeof(DramatisPersonaEquipmentEntity), "DramatisPersonaId", typeof(DramatisPersonaEntity), "EquipmentItemId", typeof(EquipmentItemEntity)),
        new(typeof(DramatisPersonaSkillEntity), "DramatisPersonaId", typeof(DramatisPersonaEntity), "SkillId", typeof(SkillEntity)),
    ];
}
