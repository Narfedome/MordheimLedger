using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

/// <summary>Section of the Warbands grid grouped by Grade ("All" filter shows a grouped CollectionView)
/// - Name is the displayed header (localized Grade label). Cf. SpellGroup (grouped by MagicSchool) -
/// same idiom, ported for Warbands/WarbandGrade.</summary>
public class WarbandArchetypeGroup : ObservableCollection<WarbandArchetypeRow>
{
    public string Name { get; }

    public WarbandArchetypeGroup(string name) => Name = name;
}
