using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.SpecialRules;

/// <summary>Section of the Special Rules grid, grouped by whether the rule is attached to
/// Warband/Warrior/Animal entries ("Guerriers &amp; Bandes") or to EquipmentItem entries ("Objets") -
/// derived from the join tables rather than a stored field, since a rule could in principle belong to
/// both. Cf. MutationGroup - same idiom. Implements ICodexGroup for CodexGroupedGridView.</summary>
public class SpecialRuleGroup : ObservableCollection<SpecialRuleRow>, ICodexGroup
{
    public string Name { get; }

    /// <summary>True for the first group in the list - trims CodexGroupHeaderStyle's top margin. Set
    /// by the ViewModel after building the list.</summary>
    public bool IsFirst { get; set; }

    public SpecialRuleGroup(string name) => Name = name;
}
