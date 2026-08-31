using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Post-battle sequence step 6, "Rare items" (p.145), split into TWO wizard steps (user request
/// 2026-08-28 - never an implicit auto-buy on a successful roll): (1) IsRareItemsStep - one optional 2D6
/// search attempt per living Hero not taken Out of Action this battle, each against a Rare item of the
/// player's choosing (RareItemSearchEntry.SelectedItem), or a common melee weapon forged in Gromril/
/// Ithilmar (SelectedMaterial) - purely determines IsSuccess, buys nothing. (2) IsRareItemPurchaseStep -
/// a separate card per successful find, "Acheter" checkbox (WantsToBuy) + a price-supplement roll for a
/// variable-cost item (PriceRoll/HasVariablePrice - "attention au prix variable !", missed in the first
/// pass of this feature), blocking Next if the checked total would exceed the warband's treasury. Only
/// entries actually bought (RareItemSearchEntry.IsPurchased) are committed at Save - see
/// WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync. Henchmen never search ("Whenever a HERO
/// wants to buy...") - entries are built once per Hero in the main constructor, this file only
/// orchestrates both steps.</summary>
public partial class EndOfGameDialogViewModel
{
    public ObservableCollection<RareItemSearchEntry> RareItemSearchEntries { get; }

    /// <summary>Drives whether the search step exists at all (Steps) - "Warriors taken out of action
    /// during the last battle may not look for rare items", so a Hero currently marked Hors de combat
    /// doesn't count even though their RareItemSearchEntries row still technically exists (hidden, not
    /// removed - see the constructor).</summary>
    public bool HasEligibleHeroesForRareItems => WarriorRows.Any(r => r.Warrior.IsHero && !r.IsOutOfAction);

    /// <summary>Gromril/Ithilmar - the only two materials with a Rarity (see SpecialRule.Rarity), which
    /// is exactly what makes a common melee weapon forged from them searchable here (see
    /// RareItemSearchEntry.EffectiveRarity) - filtered from the same full catalog dictionary already
    /// loaded for the Exploration step (_specialRulesByEnglishName), no separate query needed. Ornate
    /// Weapon/Blessed Weapon also have CostMultiplier but no Rarity, so they're naturally excluded - they
    /// don't change what's searchable, only price/effect.</summary>
    public List<SpecialRule> RareMaterialOptions => _specialRulesByEnglishName.Values
        .Where(r => r.CostMultiplier.HasValue && r.Rarity.HasValue)
        .DistinctBy(r => r.Id)
        .ToList();

    /// <summary>Exactly 2 pills hardcoded in XAML (no generic loop needed - only Gromril/Ithilmar ever
    /// qualify for RareMaterialOptions) - resolved by Abbreviation ("G"/"I", stable catalog fields) rather
    /// than by name, since name is localized and Abbreviation already exists for exactly this
    /// disambiguation purpose (see the carried-weapon chip, WarriorEquipment.NameDisplay). Null only if
    /// the catalog seed is ever missing one of them (shouldn't happen, degrades to a hidden pill).</summary>
    public SpecialRule? GromrilMaterial => RareMaterialOptions.FirstOrDefault(r => r.Abbreviation == "G");
    public SpecialRule? IthilmarMaterial => RareMaterialOptions.FirstOrDefault(r => r.Abbreviation == "I");

    /// <summary>Opens the Trading Post picker in SingleSelectMode (see IEquipmentPickerService) - the
    /// player is nominating exactly ONE item to attempt this Hero's single roll against ("You may also
    /// only make one roll for each Hero"), never a normal multi-item purchase (no budget/quantity UI
    /// shows either, since availableGold stays null here - see EquipmentItemView.xaml's ShowBudget gate).
    /// Scoped to the UNION of every equipment list this warband's recruitable WarriorArchetypes use
    /// (allowedEquipmentListItemIds, resolved fresh on each open rather than cached - a handful of
    /// GetEquipmentListItemIdsAsync calls, cheap) - NOT the searching Hero's own list, since "un héros
    /// peut chercher un équipement qui n'est pas forcément pour lui" (found item goes to the band's
    /// stash, not straight onto this Hero - user request 2026-08-28). Picking again replaces any previous
    /// choice and clears the roll/material/purchase state (see RareItemSearchEntry.OnSelectedItemChanged),
    /// same as ResolveExplorationResult resetting downstream state on a new die.</summary>
    [RelayCommand]
    private async Task SelectRareItem(RareItemSearchEntry entry)
    {
        var listIds = _warriorArchetypesByEnglishName.Values
            .Select(a => a.EquipmentListId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct();

        var allowedItemIds = new HashSet<int>();
        foreach (var listId in listIds)
            allowedItemIds.UnionWith(await _libraryService.GetEquipmentListItemIdsAsync(listId));

        var picked = await _equipmentPicker.PickEquipmentAsync(_warbandArchetypeId, singleSelect: true, rareSearchMode: true,
            allowedEquipmentListItemIds: allowedItemIds);
        if (picked.Count == 0) return;

        entry.SelectedItem = picked[0];
    }

    // Item, pas entry : ChipView.Command reçoit toujours Item comme CommandParameter (voir ChipView.xaml),
    // jamais le BindingContext englobant - suffit ici, la fiche détail ne dépend pas de quel Héros cherche.
    [RelayCommand]
    private Task ShowRareItemDetail(EquipmentItem item) => _detailDialogs.ShowEquipmentDetailDialogAsync(item);

    // Même raison : le RadioButton+ChipView du matériau reçoit la SpecialRule elle-même comme
    // CommandParameter (Item="{Binding .}" dans le DataTemplate de RareMaterialOptions).
    [RelayCommand]
    private Task ShowRareMaterialDetail(SpecialRule material) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(material);

    [RelayCommand]
    private void ClearRareItem(RareItemSearchEntry entry) => entry.SelectedItem = null;

    [RelayCommand]
    private void AutoRollRareItem(RareItemSearchEntry entry) => entry.Roll = (Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7)).ToString();

    /// <summary>Only a Hero with something actually searchable needs a roll - HasRarity is false for a
    /// common non-weapon item (nothing to roll against) or a common melee weapon with no material chosen
    /// yet, in both cases simply not a real search attempt (optional, per the book).</summary>
    private bool ValidateRareItemsStep()
    {
        var valid = true;
        foreach (var entry in RareItemSearchEntries.Where(e => !e.Hero.IsOutOfAction && e.HasRarity))
            valid &= CheckRoll(entry.TotalRoll is null, () => entry.RollError = Loc["EndOfGameRollRequired"]);
        return valid;
    }

    // --- Étape Achat (séparée, voir IsRareItemPurchaseStep) --------------------------------------

    /// <summary>Only the successful finds get a card on the Purchase step - a failed search has nothing
    /// to buy.</summary>
    public List<RareItemSearchEntry> RareItemsWithResults => RareItemSearchEntries.Where(e => e.IsSuccess).ToList();

    [RelayCommand]
    private void AutoRollRarePrice(RareItemSearchEntry entry) => entry.PriceRoll = Random.Shared.Next(1, 7).ToString();

    /// <summary>Same "gold already known this sequence" spirit as HiredSwordTreasuryAfter (Exploration
    /// gold), plus the Wyrdstone Sale step's proceeds (WyrdstoneSaleValue), since that step happens right
    /// before this one in the wizard order and its gold is just as real by the time the player decides
    /// what to buy. Purely a live preview - nothing is written to the real Warband until
    /// WarbandDetailViewModel.EndOfGame saves.</summary>
    private int RareItemBaselineTreasury => _currentTreasury
        + (ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold && int.TryParse(ExplorationGoldAmount, out var gold) ? gold : 0)
        + WyrdstoneSaleValue;

    /// <summary>Sum of EffectiveCost for every entry still checked "Acheter" - a variable-price item not
    /// yet rolled (EffectiveCost null) simply doesn't count yet, so the running total understates until
    /// every price is known (ValidateRareItemPurchaseStep blocks Next until then anyway).</summary>
    public int RareItemPurchaseTotalCost => RareItemsWithResults.Where(e => e.WantsToBuy).Sum(e => e.EffectiveCost ?? 0);

    public int RareItemPurchaseRemainingTreasury => RareItemBaselineTreasury - RareItemPurchaseTotalCost;

    /// <summary>Formatted display text - computed here rather than nesting {loc:Loc} inside a
    /// StringFormat attribute (not valid XAML, see WyrdstoneSaleValueDisplay).</summary>
    public string RareItemPurchaseRemainingTreasuryDisplay => string.Format(Loc["EndOfGameRareItemTreasuryFormat"], RareItemPurchaseRemainingTreasury);

    /// <summary>True once the checked total would push the warband into the red - blocks Next entirely
    /// (user request 2026-08-28: "bloquer le step si trésorerie négative") rather than silently skipping
    /// an unaffordable find like an earlier pass of this step did. The player resolves it themselves by
    /// unchecking something.</summary>
    public bool IsRareItemPurchaseBlocked => RareItemPurchaseRemainingTreasury < 0;

    /// <summary>Blocks Next until every checked variable-price find has its supplement rolled (a price
    /// can't be judged affordable while still unknown), then blocks again if the checked total exceeds
    /// the treasury (IsRareItemPurchaseBlocked).</summary>
    private bool ValidateRareItemPurchaseStep()
    {
        var valid = true;
        foreach (var entry in RareItemsWithResults.Where(e => e.WantsToBuy && e.HasVariablePrice))
            valid &= CheckRoll(entry.PriceSupplement is null, () => entry.PriceRollError = Loc["EndOfGameRollRequired"]);

        // Pas un champ précis à mettre en défaut (voir IsRareItemPurchaseBlocked) - un bandeau dédié en
        // XAML l'affiche directement, pas besoin de passer par CheckRoll/un message par entrée.
        return valid && !IsRareItemPurchaseBlocked;
    }
}
