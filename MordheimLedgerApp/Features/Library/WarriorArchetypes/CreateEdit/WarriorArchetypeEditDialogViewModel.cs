using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

public partial class WarriorArchetypeEditDialogViewModel : DialogViewModel<bool>
{
    private const int StepCount = 3;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private WarriorArchetype item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int currentStep;

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool CanGoBack => CurrentStep > 0;
    public bool IsLastStep => CurrentStep == StepCount - 1;
    public string StepLabel => string.Format(Loc["LibStepLabel"], CurrentStep + 1, StepCount);

    public WarriorArchetypeEditDialogViewModel(WarriorArchetype item, string title)
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
