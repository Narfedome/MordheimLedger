using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

/// <summary>Section of the Market grid grouped by EquipmentCategory ("All" filter shows a grouped
/// CollectionView) - Name is the displayed header (localized category label). Cf. SpellGroup (grouped
/// by MagicSchool) - same idiom, ported for Market/EquipmentCategory. Implements ICodexGroup for
/// CodexGroupedGridView.</summary>
public class EquipmentItemGroup : ObservableCollection<EquipmentItemRow>, ICodexGroup
{
    public string Name { get; }

    /// <summary>True for the first group in the list - trims CodexGroupHeaderStyle's top margin. Set
    /// by the ViewModel after building the list.</summary>
    public bool IsFirst { get; set; }

    public EquipmentItemGroup(string name) => Name = name;
}
