using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

/// <summary>Section of the Market grid grouped by EquipmentCategory ("All" filter shows a grouped
/// CollectionView) - Name is the displayed header (localized category label). Cf. SpellGroup (grouped
/// by MagicSchool) - same idiom, ported for Market/EquipmentCategory.</summary>
public class EquipmentItemGroup : ObservableCollection<EquipmentItemRow>
{
    public string Name { get; }

    /// <summary>True for the "Réserve" pseudo-category (EquipmentItemViewModel.ReserveQuantities) - its
    /// header stays visible even when a single category filter is active (EquipmentItemViewModel.
    /// ShowGroupHeaders hides ordinary category headers then, since the filter button already names the
    /// category - that reasoning doesn't apply here, "Réserve" is never named anywhere else on screen).
    /// Snapshotted at construction (EquipmentItemViewModel.ApplyFilter), not re-evaluated live - a full
    /// rebuild already happens on every filter change.</summary>
    public bool ShowHeader { get; init; } = true;

    public EquipmentItemGroup(string name) => Name = name;
}
