using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

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

        var target = _candidates[index].Warrior;
        var carried = await _warbandService.EquipWarbandItemToWarriorAsync(item.Id, target.Id);
        if (carried is null)
        {
            // Groupe d'Hommes de main : un exemplaire par modèle (voir IWarbandService.EquipWarbandItemToWarriorAsync).
            var available = Inventory.Where(i => i.Item.Id == item.Item.Id && i.MaterialRule?.Id == item.MaterialRule?.Id
                && i.FoundValueOverride == item.FoundValueOverride).Sum(i => i.Quantity);
            await ShowInfoAsync(Loc["InventoryNotEnoughTitle"],
                string.Format(Loc["InventoryNotEnoughMessage"], target.HeadCount, item.NameDisplay, target.Name, available));
            return;
        }

        // Plusieurs lignes ont pu être entamées (un exemplaire par modèle d'un groupe) : relire la réserve.
        var refreshed = await _warbandService.GetWarbandEquipmentAsync(item.WarbandId, LocalizationService.Instance.Language);
        Inventory.Clear();
        foreach (var row in refreshed) Inventory.Add(row);

        if (Inventory.Count == 0) Close(true);
    }

    /// <summary>Uniquement visible dans le XAML pour un item.IsSellable (voir WarbandEquipment - vrai
    /// seulement si son MaterialRule a SpecialRule.IsResaleUpgrade, ex. "Arme Ornée" de Charrette
    /// Renversée) - le service refuse tout item dont le matériau n'a pas ce flag, cette garde ici est une
    /// défense en profondeur, pas le seul verrou.</summary>
    [RelayCommand]
    private async Task Sell(WarbandEquipment item)
    {
        if (!item.IsSellable) return;

        await _warbandService.SellWarbandItemAsync(item.Id);
        Inventory.Remove(item);

        if (Inventory.Count == 0) Close(true);
    }
}
