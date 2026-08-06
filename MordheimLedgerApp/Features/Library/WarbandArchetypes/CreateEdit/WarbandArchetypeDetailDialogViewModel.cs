using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

/// <summary>Read-only recap of WarbandArchetypeEditDialog. Unlike the other 7 detail dialogs, needs no
/// restriction-id resolution from the caller - SpecialRules/MagicSchools are already resolved objects
/// on WarbandArchetype itself.</summary>
public partial class WarbandArchetypeDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public WarbandArchetype Item { get; }
    public string GradeLabel { get; }
    public string MaxWarriorsDisplay { get; }

    public WarbandArchetypeDetailDialogViewModel(WarbandArchetype item)
    {
        Item = item;
        Title = item.Name;
        GradeLabel = Loc[$"WarbandGrade{item.Grade}"];
        MaxWarriorsDisplay = item.MaxWarriors?.ToString() ?? "—";
    }

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private Task ShowMagicSchoolDetail(MagicSchool school) => ShowChipDetailAsync(school.Name, school.Description);
}
