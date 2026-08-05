using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mounts.CreateEdit;

/// <summary>Same 2-step wizard pattern (identité / profil) as WarriorArchetypeEditDialogViewModel -
/// Rarity/RestrictedToWarbandArchetypeIds/SpecialRules stay seed-only for this pass (no picker UI here
/// yet, same precedent as EquipmentItem).</summary>
public partial class MountEditDialogViewModel : DialogViewModel<bool>
{
    private const int StepCount = 2;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Mount item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int currentStep;

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool CanGoBack => CurrentStep > 0;
    public bool IsLastStep => CurrentStep == StepCount - 1;
    public string StepLabel => string.Format(Loc["LibStepLabel"], CurrentStep + 1, StepCount);

    public MountEditDialogViewModel(Mount item, string title)
    {
        this.item = item;
        this.title = title;
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep < StepCount - 1) CurrentStep++;
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 0) CurrentStep--;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
