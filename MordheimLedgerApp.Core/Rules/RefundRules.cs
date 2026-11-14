using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Ce que rend la trésorerie quand on corrige le roster (guerrier supprimé, groupe réduit, achat retiré) :
/// exactement ce qui a été payé. Seule règle pour la fiche de bande (suppression depuis le formulaire de guerrier) et
/// le formulaire de bande, qui avaient chacun leur calcul et oubliaient l'effectif d'un groupe (un groupe de 3 ne
/// rendait le prix que d'un modèle) - corrigé le 2026-11-14.
///
/// Un groupe d'Hommes de main paie chaque achat une fois par modèle (prix × effectif, voir
/// WarriorEditDialogViewModel.AddEquipment) ; un Héros ou un Franc-Tireur une seule fois.</summary>
public static class RefundRules
{
    /// <summary>Nombre de modèles qui ont payé chaque achat du guerrier.</summary>
    public static int PayingModels(Warrior warrior) => warrior.IsHero ? 1 : Math.Max(1, warrior.HeadCount);

    /// <summary>Prix payé par UN modèle pour les lignes de fullEquipment retenues par shouldRefund (toutes par
    /// défaut). fullEquipment doit être l'équipement COMPLET du guerrier : la dague gratuite n'est pas mémorisée à
    /// l'achat, on la retrouve en rejouant les achats dans leur ordre (Id croissant) avec la même règle qu'à l'achat
    /// - une dague sans matériau est gratuite si aucune dague n'était déjà portée.</summary>
    public static int EquipmentCostPerModel(IEnumerable<WarriorEquipment> fullEquipment, Func<WarriorEquipment, bool>? shouldRefund = null)
    {
        var total = 0;
        var carriesDagger = false;
        foreach (var carried in fullEquipment.OrderBy(e => e.Id))
        {
            var isFree = carried.MaterialRule is null && EquipmentPricing.IsFreeDaggerEligible(carried.Item.IsFreeDagger, carriesDagger);
            carriesDagger |= carried.Item.IsFreeDagger;
            if (shouldRefund?.Invoke(carried) ?? true)
                total += EquipmentPricing.CalculateCost(carried.Item.Cost, carried.MaterialRule?.CostMultiplier, isFree) * carried.Quantity;
        }
        return total;
    }

    /// <summary>Prix payé par UN modèle : recrutement, équipement et mutations achetées.</summary>
    public static int CostPerModel(Warrior warrior, IEnumerable<WarriorEquipment> equipment, IEnumerable<Mutation> paidMutations) =>
        warrior.Cost + EquipmentCostPerModel(equipment) + paidMutations.Sum(m => m.Cost);

    /// <summary>Suppression complète d'un guerrier ou d'un groupe : tout ce qu'il a payé, pour chacun de ses modèles.</summary>
    public static int DeletionRefund(Warrior warrior, IEnumerable<WarriorEquipment> equipment, IEnumerable<Mutation> paidMutations) =>
        CostPerModel(warrior, equipment, paidMutations) * PayingModels(warrior);
}
