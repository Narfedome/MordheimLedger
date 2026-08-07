using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Skills;

/// <summary>Section of the Skills grid grouped by SkillCategory ("All" filter shows a grouped
/// CollectionView) - Name is the displayed header (localized category label). Cf. SpellGroup (grouped
/// by MagicSchool) - same idiom, ported for Skills/SkillCategory. Implements ICodexGroup for
/// CodexGroupedGridView.</summary>
public class SkillGroup : ObservableCollection<SkillRow>, ICodexGroup
{
    public string Name { get; }

    /// <summary>True for the first group in the list - trims the shared CodexGroupHeaderStyle's top
    /// margin so the very first header doesn't sit further from the category picker than a normal
    /// group-to-group gap. Set by the ViewModel after building the list (a group doesn't know its own
    /// position).</summary>
    public bool IsFirst { get; set; }

    public SkillGroup(string name) => Name = name;
}
