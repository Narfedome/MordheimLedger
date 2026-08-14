using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Un seul dialog pour toutes les armes de corps à corps éligibles à un matériau dans un même
/// lot d'achat, avec Précédent/Suivant pour naviguer entre elles - plutôt qu'une ActionSheet fermée puis
/// rouverte pour chaque arme (demande explicite de l'utilisateur, la version précédente affichait un
/// mini-dialog par arme dans WarbandEditDialogViewModel/WarriorEditDialogViewModel.AddEquipment). Se
/// dégrade naturellement à une seule arme sans pagination (HasMultiple masque Précédent/Suivant).
/// Close(true) = Enregistrer, l'appelant lit MaterialChoice.SelectedMaterial de chaque Choices ; Close
/// (false) = Annuler, l'appelant traite tout comme Normal (aucun matériau).</summary>
public partial class MaterialPickerDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    public ObservableCollection<MaterialChoice> Choices { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Current))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    private int currentIndex;

    public MaterialChoice Current => Choices[CurrentIndex];
    public bool CanGoBack => CurrentIndex > 0;
    public bool CanGoNext => CurrentIndex < Choices.Count - 1;
    public bool HasMultiple => Choices.Count > 1;
    public string StepLabel => string.Format(Loc["WarriorsMaterialStepLabel"], CurrentIndex + 1, Choices.Count);

    public MaterialPickerDialogViewModel(IReadOnlyList<MaterialChoice> choices)
    {
        Choices = new ObservableCollection<MaterialChoice>(choices);
    }

    [RelayCommand]
    private void Next()
    {
        if (CanGoNext) CurrentIndex++;
    }

    [RelayCommand]
    private void Back()
    {
        if (CanGoBack) CurrentIndex--;
    }

    [RelayCommand]
    private void SelectMaterial(MaterialOptionRow row) => row.Owner.Select(row);

    [RelayCommand]
    private void Save() => Close(true);
}
