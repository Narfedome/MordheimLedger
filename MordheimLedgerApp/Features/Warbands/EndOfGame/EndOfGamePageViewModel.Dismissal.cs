using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Renvoyer" (livre des règles - "Disbanding a Warband" + FAQ officielle citée par
/// l'utilisateur 2026-09-22 - "you are allowed to dismiss any warrior at any time during the post-battle
/// sequence... you can first transfer the wounded warrior's weapons and gear to your stash and then
/// dismiss him") - toujours présente, juste AVANT Achat/Vente (voir EndOfGamePageViewModel.cs's Steps) :
/// un guerrier renvoyé restitue tout son équipement à la réserve plutôt que de le perdre, réutilisable
/// ensuite au choix (vendu à l'étape Achat/Vente ou réattribué gratuitement à une nouvelle recrue à
/// l'étape Recrutement - voir BuildStashPool/BuildSellableCandidates).
///
/// Héros (HeadCount toujours 1) : case à cocher (WarriorOutcomeRow.IsDismissed). Groupe d'Hommes de main
/// (HeadCount potentiellement &gt; 1) : stepper -/+ "combien de figurines renvoyer" (0..HeadCount, même
/// mécanique que le stepper de Recrutement des vétérans) - renvoi partiel supporté, chaque figurine
/// renvoyée restitue sa propre part d'équipement (WarriorEquipment.Quantity est une quantité PAR MODÈLE,
/// voir ExistingHenchmanTopUp.cs's GetTopUpBreakdown - la ligne d'équipement elle-même ne change pas pour
/// un renvoi partiel, seul Warrior.HeadCount diminue à Terminer, voir WarbandDetailViewModel.EndOfGame.
/// ApplyDismissalsAsync).</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Guerriers éligibles au renvoi - jamais un Franc-Tireur/Dramatis Persona (déjà leur propre
    /// mécanisme Payer/Renvoyer) ni un guerrier déjà mort ce tour.</summary>
    public IEnumerable<WarriorOutcomeRow> DismissibleWarriorRows => WarriorRows.Where(r => r.CanBeDismissed);

    // Le rafraîchissement de l'éligibilité de Recrutement (TotalWarriorCountAfterRecruitment retranche
    // DismissCount, retour utilisateur - "dans les recrutement, il faut soustraire les effectifs") et du
    // récap "Équipement récupéré" se fait uniformément via l'abonnement row.PropertyChanged sur
    // DismissCount dans EndOfGamePageViewModel.cs, pas ici - il couvre aussi bien ce stepper que la case
    // à cocher d'un Héros (binding direct, jamais une commande).
    [RelayCommand]
    private void IncrementDismiss(WarriorOutcomeRow row) => row.DismissCount = Math.Min(row.HeadCount, row.DismissCount + 1);

    [RelayCommand]
    private void DecrementDismiss(WarriorOutcomeRow row) => row.DismissCount = Math.Max(0, row.DismissCount - 1);

    /// <summary>Équipement que le renvoi EN COURS (DismissCount &gt; 0 sur un ou plusieurs guerriers) va
    /// restituer à la réserve - même forme que PendingExplorationStashItems, consommé par BuildStashPool
    /// (Recrutement) et BuildSellableCandidates (Vente). Quantity déjà multipliée par le nombre de
    /// figurines renvoyées (voir la doc de classe - Quantity par modèle).</summary>
    public IEnumerable<(EquipmentItem Item, SpecialRule? MaterialRule, int Quantity)> PendingDismissedEquipment() =>
        WarriorRows.Where(r => r.DismissCount > 0)
            .SelectMany(r => r.Warrior.Equipment.Select(e => (e.Item, e.MaterialRule, Quantity: e.Quantity * r.DismissCount)));

    /// <summary>Récap "Équipement récupéré" affiché en direct sur cette étape (retour utilisateur
    /// 2026-09-22 - "quand on clique sur next, on affiche la liste des équipements récupérés après le
    /// renvoi... en format chip comme tout le reste") - en pratique mis à jour à chaque coche/stepper
    /// plutôt que gated derrière Next, même idiome que tous les autres totaux "en direct" de ce wizard.
    /// Groupé par (Item, MaterialRule) : un Héros et un groupe d'Hommes de main renvoyés en même temps
    /// peuvent restituer le même type d'objet, affiché en une seule chip.</summary>
    public IEnumerable<DismissedEquipmentChip> RecoveredDismissedEquipmentChips =>
        PendingDismissedEquipment()
            .GroupBy(x => (x.Item.Id, MaterialRuleId: x.MaterialRule?.Id))
            .Select(g => new DismissedEquipmentChip(g.First().Item, g.First().MaterialRule, g.Sum(x => x.Quantity)));

    [RelayCommand]
    private Task ShowDismissedEquipmentDetail(DismissedEquipmentChip chip) => _detailDialogs.ShowEquipmentDetailDialogAsync(chip.Item, chip.MaterialRule);
}

/// <summary>Une ligne du récap "Équipement récupéré" de l'étape Renvoyer - voir
/// EndOfGamePageViewModel.RecoveredDismissedEquipmentChips. Name suffixé "xN" comme
/// WarriorEquipment.NameDisplay quand la quantité restituée dépasse 1.</summary>
public sealed class DismissedEquipmentChip
{
    public EquipmentItem Item { get; }
    public SpecialRule? MaterialRule { get; }
    public string Name { get; }

    public DismissedEquipmentChip(EquipmentItem item, SpecialRule? materialRule, int quantity)
    {
        Item = item;
        MaterialRule = materialRule;
        var name = materialRule?.Abbreviation is { Length: > 0 } abbr ? $"{item.Name} ({abbr})" : item.Name;
        Name = quantity > 1 ? $"{name} x{quantity}" : name;
    }
}
