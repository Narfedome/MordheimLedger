using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>One selectable row within MaterialPickerDialog for a single weapon - "Normal" (Rule == null)
/// plus one row per eligible material SpecialRule (Gromril, Ithilmar...). IsSelected is toggled by
/// MaterialChoice.Select, exclusively within that one weapon's Options - same imperative-flag idiom as
/// EquipmentItemRow.IsSelected rather than a data-trigger comparison against the parent's current
/// selection.</summary>
public partial class MaterialOptionRow : ObservableObject
{
    /// <summary>The MaterialChoice this row belongs to - lets SelectMaterialCommand toggle exclusivity
    /// across this row's siblings without the caller having to track which weapon is currently shown.</summary>
    public MaterialChoice Owner { get; }

    /// <summary>Null = Normal (plain Cost, no material).</summary>
    public SpecialRule? Rule { get; }
    public string Name { get; }
    public string? Description { get; }
    public int Cost { get; }

    /// <summary>"Gratuit"/"Free" when Cost is 0 (the Normal option of an IsFreeDagger-eligible weapon -
    /// see MaterialChoice's isFreeEligible constructor param), the usual "{n} CO" otherwise. A material
    /// row's Cost is never 0 (a Gromril/Ithilmar dagger is a deliberate upgrade, not the free baseline -
    /// see MaterialChoice constructor), so this only ever reads "Gratuit" on the Normal row.</summary>
    public string CostDisplay => Cost == 0
        ? LocalizationService.Instance["LibFreePh"]
        : $"{Cost} {LocalizationService.Instance["LibGoldCrownsAbbr"]}";

    [ObservableProperty]
    private bool isSelected;

    public MaterialOptionRow(MaterialChoice owner, SpecialRule? rule, string name, string? description, int cost)
    {
        Owner = owner;
        Rule = rule;
        Name = name;
        Description = description;
        Cost = cost;
    }
}

/// <summary>One weapon awaiting a material choice within MaterialPickerDialog - one per eligible melee
/// weapon in the current purchase batch. WarbandEditDialogViewModel/WarriorEditDialogViewModel.
/// AddEquipment build one of these per melee item and show a single dialog with Précédent/Suivant to
/// navigate between them, instead of a separate ActionSheet closed and reopened per weapon (explicit
/// user request - see MaterialPickerDialogViewModel).</summary>
public partial class MaterialChoice : ObservableObject
{
    public EquipmentItem Item { get; }
    public ObservableCollection<MaterialOptionRow> Options { get; }

    public SpecialRule? SelectedMaterial => Options.FirstOrDefault(o => o.IsSelected)?.Rule;
    public int SelectedCost => Options.FirstOrDefault(o => o.IsSelected)?.Cost ?? Item.Cost;

    /// <param name="isFreeEligible">True when Item.IsFreeDagger and the target doesn't already carry one
    /// (same check as WarbandEditDialogViewModel/WarriorEditDialogViewModel.AddEquipment's IsFree logic) -
    /// zeroes only the Normal option's cost to "Gratuit". A Gromril/Ithilmar dagger is a deliberate
    /// upgrade, not the assumed baseline, so material rows always keep their full multiplied price
    /// regardless of this flag.</param>
    public MaterialChoice(EquipmentItem item, IReadOnlyList<SpecialRule> materialRules, string normalLabel, bool isFreeEligible = false)
    {
        Item = item;
        var normalCost = isFreeEligible ? 0 : item.Cost;
        var options = new List<MaterialOptionRow> { new(this, null, normalLabel, null, normalCost) };
        options.AddRange(materialRules.Select(r => new MaterialOptionRow(this, r, r.Name, r.Description, item.Cost * (r.CostMultiplier ?? 1))));
        Options = new ObservableCollection<MaterialOptionRow>(options);
        Options[0].IsSelected = true;
    }

    public void Select(MaterialOptionRow row)
    {
        foreach (var o in Options) o.IsSelected = ReferenceEquals(o, row);
        OnPropertyChanged(nameof(SelectedMaterial));
        OnPropertyChanged(nameof(SelectedCost));
    }
}
