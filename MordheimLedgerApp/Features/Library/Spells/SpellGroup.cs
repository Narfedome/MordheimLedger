using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Spells;

/// <summary>Section of the Spells grid grouped by magic school ("All" filter shows a grouped
/// CollectionView) - Name is the displayed header (magic school name). Cf. DmTools' TrackGroup
/// (grouped by Category) - same idiom, ported for Spells/MagicSchool. Implements ICodexGroup for
/// CodexGroupedGridView.</summary>
public class SpellGroup : ObservableCollection<SpellRow>, ICodexGroup
{
    public string Name { get; }

    /// <summary>True for the first group in the list - trims CodexGroupHeaderStyle's top margin. Set
    /// by the ViewModel after building the list.</summary>
    public bool IsFirst { get; set; }

    public SpellGroup(string name) => Name = name;
}
