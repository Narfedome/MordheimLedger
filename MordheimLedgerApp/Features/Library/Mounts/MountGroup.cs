using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Library.Mounts;

/// <summary>Section of the Mounts grid grouped by "Common" vs. the warband(s) it's restricted to
/// ("All" filter shows a grouped CollectionView) - Mount has no rulebook category of its own, so
/// RestrictedToWarbandArchetypeIds (already tracked for the picker) doubles as the grouping axis
/// instead of adding an unused field. Name is the displayed header. Cf. MutationGroup - same idiom.</summary>
public class MountGroup : ObservableCollection<MountRow>
{
    public string Name { get; }

    public MountGroup(string name) => Name = name;
}
