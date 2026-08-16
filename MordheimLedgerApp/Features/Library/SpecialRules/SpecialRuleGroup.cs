using System.Collections.ObjectModel;
using MordheimLedgerApp.Components;

namespace MordheimLedgerApp.Features.Library.SpecialRules;

/// <summary>Section of the Special Rules grid, grouped by whether the rule is attached to
/// Warband/Warrior entries ("Guerriers &amp; Bandes") or to EquipmentItem entries ("Objets" - includes
/// Animal-category items, a mount is just an EquipmentItem) - derived from the join tables rather than a
/// stored field, since a rule could in principle belong to both. Cf. MutationGroup - same idiom.</summary>
public class SpecialRuleGroup : ObservableCollection<SpecialRuleRow>
{
    public string Name { get; }

    public SpecialRuleGroup(string name) => Name = name;
}
