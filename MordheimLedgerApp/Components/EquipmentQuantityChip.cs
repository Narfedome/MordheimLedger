using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Components;

/// <summary>Read-only catalog chip for a starting-gear list (DramatisPersona.StartingEquipmentIds,
/// HiredSword.StartingEquipmentIds) that collapses a repeated id into ONE chip with an "x2" suffix -
/// same NameDisplay idiom as Models.WarriorEquipment.NameDisplay, but for a catalog list that has no
/// real per-instance row/Quantity column of its own (it's just a List&lt;int&gt; of ids, duplicates
/// meaning "carries two"). Found 2026-09-01: DetailDialogService used to resolve these ids via
/// EquipmentItem.Where(ids.Contains(e.Id)), which silently collapsed a duplicate id to a single chip
/// (iterates the catalog, not the id list - same bug class as the recruitment-time fix for Bertha's
/// 2 Sigmarite Warhammers) - fixed by grouping the id list itself instead.</summary>
public class EquipmentQuantityChip
{
    public required EquipmentItem Item { get; init; }
    public required int Quantity { get; init; }

    public string Name => Quantity > 1 ? $"{Item.Name} x{Quantity}" : Item.Name;

    /// <summary>Passe-plat vers Item.Category - lu directement par le converter d'icône dans le
    /// DataTemplate (EquipmentCategoryIconConverter), même motif que WarriorEquipment.Name.</summary>
    public EquipmentCategory Category => Item.Category;

    /// <summary>Groupe une liste d'ids (avec doublons éventuels) contre le catalogue résolu, un chip par
    /// id distinct - l'ordre d'apparition dans ids est préservé (GroupBy conserve l'ordre de première
    /// occurrence).</summary>
    public static List<EquipmentQuantityChip> GroupFrom(IEnumerable<int> ids, IReadOnlyCollection<EquipmentItem> catalog) =>
        ids.GroupBy(id => id)
            .Select(g => new EquipmentQuantityChip { Item = catalog.First(e => e.Id == g.Key), Quantity = g.Count() })
            .ToList();
}
