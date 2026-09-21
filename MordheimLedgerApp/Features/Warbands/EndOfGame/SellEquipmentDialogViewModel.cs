using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Sélection multiple des lignes à vendre (réserve + équipement porté, voir
/// SellableEquipmentCandidate's own doc) - même idiome que MaterialPickerDialogViewModel : Close(true) =
/// Valider, l'appelant (EndOfGameDialogViewModel.Recruitment.AddSaleEquipment) relit directement
/// Candidates.Where(IsSelected) sur CETTE instance plutôt qu'un résultat renvoyé en TResult. Close(false)
/// = Annuler, rien n'est ajouté à PendingSales.</summary>
public partial class SellEquipmentDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    public ObservableCollection<SellableEquipmentCandidate> Candidates { get; }

    /// <summary>Total affiché en direct pendant la sélection - notifié manuellement (IsSelected vit sur
    /// SellableEquipmentCandidate, pas ce ViewModel) plutôt qu'un binding agrégé natif, même idiome que
    /// ExistingHenchmanTopUp.Breakdown "poussé" ailleurs dans ce wizard.</summary>
    public int TotalSellPrice => Candidates.Where(c => c.IsSelected).Sum(c => c.SellPrice);

    [RelayCommand]
    private void ToggleSelection(SellableEquipmentCandidate candidate)
    {
        candidate.IsSelected = !candidate.IsSelected;
        OnPropertyChanged(nameof(TotalSellPrice));
    }

    [RelayCommand]
    private void Confirm() => Close(true);

    public SellEquipmentDialogViewModel(IEnumerable<SellableEquipmentCandidate> candidates)
    {
        Candidates = new ObservableCollection<SellableEquipmentCandidate>(candidates);
    }
}
