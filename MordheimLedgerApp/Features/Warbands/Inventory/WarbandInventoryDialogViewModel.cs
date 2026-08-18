using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Features.Warbands.Inventory;

/// <summary>Inventaire de bande (objets trouvés, ex. étape Exploration du wizard Fin de Partie, pas
/// encore assignés à un guerrier) - déplacé du roster (section dépliable) vers ce dialog dédié ouvert
/// depuis un bouton en en-tête de WarbandDetailPage, sur demande explicite de l'utilisateur (2026-08-18).
/// Édition "live" comme WarriorEditDialog : chaque Équiper persiste immédiatement (pas de bouton
/// Enregistrer/Annuler), l'appelant recharge le roster à la fermeture quel que soit le mode de
/// fermeture. Se ferme tout seul si le dernier objet est équipé pendant que le dialog est ouvert -
/// rien de plus à afficher une fois la liste vide.</summary>
public partial class WarbandInventoryDialogViewModel : ReadOnlyDialogViewModel
{
    private readonly IWarbandService _warbandService;
    private readonly List<WarriorRow> _candidates;

    public ObservableCollection<WarbandEquipment> Inventory { get; }

    public WarbandInventoryDialogViewModel(IEnumerable<WarbandEquipment> inventory, IEnumerable<WarriorRow> candidates, IWarbandService warbandService)
    {
        Title = Loc["InventoryHeading"];
        Inventory = new ObservableCollection<WarbandEquipment>(inventory);
        _candidates = candidates.ToList();
        _warbandService = warbandService;
    }

    [RelayCommand]
    private async Task Equip(WarbandEquipment item)
    {
        if (_candidates.Count == 0) return;

        var index = await ShowActionSheetIndexAsync(Loc["InventoryEquipTitle"], _candidates.Select(r => r.Warrior.Name).ToArray());
        if (index < 0 || index >= _candidates.Count) return;

        await _warbandService.EquipWarbandItemToWarriorAsync(item.Id, _candidates[index].Warrior.Id);
        Inventory.Remove(item);

        if (Inventory.Count == 0) Close(true);
    }
}
