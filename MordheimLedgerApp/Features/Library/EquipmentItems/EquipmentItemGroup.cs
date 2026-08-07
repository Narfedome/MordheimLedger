using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

/// <summary>Section of the Market grid grouped by EquipmentCategory ("All" filter shows a grouped
/// CollectionView) - Name is the displayed header (localized category label). Cf. SpellGroup (grouped
/// by MagicSchool) - same idiom, ported for Market/EquipmentCategory. Implements ICodexGroup for
/// CodexGroupedGridView.</summary>
public class EquipmentItemGroup : ObservableCollection<EquipmentItemRow>
{
    public string Name { get; }

    public EquipmentItemGroup(string name) => Name = name;
}
