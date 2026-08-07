using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

/// <summary>Section of the Warbands grid grouped by Grade ("All" filter shows a grouped CollectionView)
/// - Name is the displayed header (localized Grade label). Cf. SpellGroup (grouped by MagicSchool) -
/// same idiom, ported for Warbands/WarbandGrade. Implements ICodexGroup for CodexGroupedGridView.</summary>
public class WarbandArchetypeGroup : ObservableCollection<WarbandArchetypeRow>, ICodexGroup
{
    public string Name { get; }

    /// <summary>True for the first group in the list - trims CodexGroupHeaderStyle's top margin. Set
    /// by the ViewModel after building the list.</summary>
    public bool IsFirst { get; set; }

    public WarbandArchetypeGroup(string name) => Name = name;
}
