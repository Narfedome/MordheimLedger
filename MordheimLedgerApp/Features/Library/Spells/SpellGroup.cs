using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Spells;

/// <summary>Section of the Spells grid grouped by magic school ("All" filter shows a grouped
/// CollectionView) - Name is the displayed header (magic school name). Cf. DmTools' TrackGroup
/// (grouped by Category) - same idiom, ported for Spells/MagicSchool. Implements ICodexGroup for
/// CodexGroupedGridView.</summary>
public class SpellGroup : ObservableCollection<SpellRow>
{
    public string Name { get; }
    
    public SpellGroup(string name) => Name = name;
}
