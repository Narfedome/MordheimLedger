using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Skills;

/// <summary>Section of the Skills grid grouped by SkillCategory ("All" filter shows a grouped
/// CollectionView) - Name is the displayed header (localized category label). Cf. SpellGroup (grouped
/// by MagicSchool) - same idiom, ported for Skills/SkillCategory. Implements ICodexGroup for
/// CodexGroupedGridView.</summary>
public class SkillGroup : ObservableCollection<SkillRow>
{
    public string Name { get; }

    public SkillGroup(string name) => Name = name;
}
