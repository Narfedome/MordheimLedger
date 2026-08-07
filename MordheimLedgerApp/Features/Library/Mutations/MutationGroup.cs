using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Mutations;

/// <summary>Section of the Mutations grid grouped by "Common" vs. the warband(s) it's restricted to
/// ("All" filter shows a grouped CollectionView) - Mutation has no rulebook category of its own, so
/// RestrictedToWarbandArchetypeIds (already tracked for the picker) doubles as the grouping axis
/// instead of adding an unused field. Name is the displayed header. Cf. SpellGroup (grouped by
/// MagicSchool) - same idiom.</summary>
public class MutationGroup : ObservableCollection<MutationRow>
{
    public string Name { get; }
    
    public MutationGroup(string name) => Name = name;
}
