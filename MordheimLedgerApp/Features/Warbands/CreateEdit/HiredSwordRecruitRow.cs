using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>One eligible Hired Sword type in the "Mercenaires" step of WarbandEditDialog - much simpler
/// than WarriorRecruitRow (no equipment/skill picking, fixed gear; no Count/MaxCount, "only one of each
/// type" already enforced by the picker itself, see AddHiredSwordCommand) - see Models.Library.HiredSword.
/// IsRecruited is driven by the ChipListView on the Mercenaires tab (present in RecruitedHiredSwordRows =
/// added via AddHiredSwordCommand/removed via RemoveHiredSwordCommand); Name is edited separately, on the
/// Noms tab's 3rd section (see WarbandEditDialog.xaml). ExistingWarrior non-null means this type is
/// already recruited into the reopened warband (see
/// WarbandEditDialogViewModel.EnsureRecruitableArchetypesLoadedAsync) - IsRecruited starts true in that
/// case, and un-ticking it (removing its chip) during this edit session refunds his hire cost at Save()
/// (same "changed my mind while editing the roster" convention as removing an ordinary warrior via
/// DecrementWarrior/_pendingFullDeletions) - deliberately simpler than that flow (no confirmation dialog),
/// since this is a secondary entry point: the primary way to let a Hired Sword go is the End of Game
/// "Francs-Tireurs" step's unpaid-upkeep choice, which never refunds anything.</summary>
public partial class HiredSwordRecruitRow : ObservableObject
{
    public HiredSword HiredSword { get; }
    public Warrior? ExistingWarrior { get; }

    [ObservableProperty]
    private bool isRecruited;

    [ObservableProperty]
    private string name;

    public int Cost => HiredSword.HireCost;

    public HiredSwordRecruitRow(HiredSword hiredSword, Warrior? existingWarrior)
    {
        HiredSword = hiredSword;
        ExistingWarrior = existingWarrior;
        isRecruited = existingWarrior is not null;
        name = existingWarrior?.Name ?? hiredSword.Name;
    }
}
