using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Comment un guerrier a accès à un objet - voir EquipmentEligibility.AccessFor.</summary>
public enum EquipmentAccessKind
{
    /// <summary>Catégorie qui ne dépend d'aucune liste d'équipement (Divers, Drogues, Montures...).</summary>
    NotListScoped,
    /// <summary>Dans la liste d'équipement du guerrier.</summary>
    InList,
    /// <summary>Hors liste, mais ouverte par une compétence (Entraînement aux Armes...).</summary>
    OpenedBySkill,
    /// <summary>Hors liste, rien ne l'ouvre.</summary>
    OutOfList,
}

public sealed record EquipmentAccess(EquipmentAccessKind Kind, string? SkillName = null)
{
    public bool CanEquip => Kind != EquipmentAccessKind.OutOfList;
}

/// <summary>Qui peut équiper quoi, d'après les listes d'équipement et les compétences qui ouvrent des
/// catégories (EffectMoment.EquipmentChoice + EffectKind.Allow). Sert au sélecteur d'achat pour la Réserve
/// (ligne « qui peut l'équiper » par tuile), au récap d'un objet et au message ambre de Réallouer. Les
/// sélecteurs liés à un guerrier n'en ont pas besoin pour filtrer : ils ne proposent déjà que l'autorisé,
/// ouvertures comprises (voir OpenedCategories).</summary>
public static class EquipmentEligibility
{
    /// <summary>Seules les armes et armures sont rattachées à une liste d'équipement - même règle que le
    /// sélecteur d'équipement (EquipmentItemViewModel.ApplyFilter).</summary>
    public static bool IsListScoped(EquipmentCategory category) => category is
        EquipmentCategory.MeleeWeapon or EquipmentCategory.MissileWeapon or EquipmentCategory.BlackPowderWeapon or EquipmentCategory.Armour;

    /// <summary>Catégories que les compétences du guerrier libèrent de sa liste, avec la compétence qui les
    /// ouvre (la première si plusieurs).</summary>
    public static IReadOnlyDictionary<EquipmentCategory, string> OpenedCategories(Warrior warrior)
    {
        var opened = new Dictionary<EquipmentCategory, string>();
        foreach (var warriorSkill in warrior.Skills)
            foreach (var effect in warriorSkill.Item.Effects)
            {
                if (effect.Moment != EffectMoment.EquipmentChoice || effect.Kind != EffectKind.Allow) continue;
                foreach (var target in effect.Targets)
                    if (Enum.TryParse<EquipmentCategory>(target, out var category))
                        opened.TryAdd(category, warriorSkill.Item.Name);
            }
        return opened;
    }

    /// <param name="equipmentListItemIds">Objets de la liste d'équipement du guerrier (Warrior.EquipmentListId,
    /// résolue par l'appelant) - null = aucune liste.</param>
    public static EquipmentAccess AccessFor(EquipmentItem item, Warrior warrior, IReadOnlySet<int>? equipmentListItemIds)
    {
        if (!IsListScoped(item.Category)) return new EquipmentAccess(EquipmentAccessKind.NotListScoped);

        var reservedForOthers = item.RestrictedToWarriorArchetypeIds.Count > 0
            && (warrior.WarriorArchetypeId is not { } archetypeId || !item.RestrictedToWarriorArchetypeIds.Contains(archetypeId));
        if (!reservedForOthers && equipmentListItemIds is not null && equipmentListItemIds.Contains(item.Id))
            return new EquipmentAccess(EquipmentAccessKind.InList);

        return OpenedCategories(warrior).TryGetValue(item.Category, out var skillName)
            ? new EquipmentAccess(EquipmentAccessKind.OpenedBySkill, skillName)
            : new EquipmentAccess(EquipmentAccessKind.OutOfList);
    }
}
