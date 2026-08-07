using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Animals;

/// <summary>Section of the Animals grid grouped by "Common" vs. the warband(s) it's restricted to
/// ("All" filter shows a grouped CollectionView) - Animal has no rulebook category of its own, so
/// RestrictedToWarbandArchetypeIds (already tracked for the picker) doubles as the grouping axis
/// instead of adding an unused field. Name is the displayed header. Cf. MutationGroup - same idiom.
/// Implements ICodexGroup for CodexGroupedGridView.</summary>
public class AnimalGroup : ObservableCollection<AnimalRow>, ICodexGroup
{
    public string Name { get; }

    /// <summary>True for the first group in the list - trims CodexGroupHeaderStyle's top margin. Set
    /// by the ViewModel after building the list.</summary>
    public bool IsFirst { get; set; }

    public AnimalGroup(string name) => Name = name;
}
