using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Injuries;

/// <summary>Section of the Injuries grid grouped by InjuryCategory (Hero/Henchman) - same idiom as
/// SkillGroup, ported for Injuries/InjuryCategory. Implements ICodexGroup for CodexGroupedGridView.</summary>
public class InjuryGroup : ObservableCollection<InjuryRow>, ICodexGroup
{
    public string Name { get; }

    /// <summary>True for the first group in the list - trims CodexGroupHeaderStyle's top margin. Set
    /// by the ViewModel after building the list.</summary>
    public bool IsFirst { get; set; }

    public InjuryGroup(string name) => Name = name;
}
