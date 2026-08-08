using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.Injuries;

/// <summary>Section of the Injuries grid grouped by InjuryCategory (Hero/Henchman) - same idiom as
/// SkillGroup, ported for Injuries/InjuryCategory.</summary>
public class InjuryGroup : ObservableCollection<InjuryRow>
{
    public string Name { get; }

    public InjuryGroup(string name) => Name = name;
}
