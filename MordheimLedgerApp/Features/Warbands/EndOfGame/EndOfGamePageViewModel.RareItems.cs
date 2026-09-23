using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Post-battle sequence step 6, "Rare items" (p.145) - and, since 2026-08-31, step 7 "Looking
/// for special characters" (p.146) sharing the same two-step shape - split into TWO wizard steps (user
/// request 2026-08-28 - never an implicit auto-buy on a successful roll): (1) IsRareItemsStep - one
/// optional search attempt per living Hero not taken Out of Action this battle, EITHER against a Rare
/// item of the player's choosing (RareItemSearchEntry.SelectedItem, 2D6 vs Rarity - purely determines
/// IsSuccess, buys nothing) OR a Dramatis Persona (SelectedCharacter, 1D6 vs Initiative - determines
/// IsCharacterFound, recruits nothing), per RareItemSearchEntry.IsSearchingForCharacter. (2)
/// IsRareItemPurchaseStep - a separate card per successful find of EITHER kind (RareItemSearchEntry.
/// IsFound): "Acheter" (WantsToBuy) + a price-supplement roll for a variable-cost item (PriceRoll/
/// HasVariablePrice - "attention au prix variable !", missed in the first pass of this feature) for an
/// item, or "Recruter" (WantsToRecruit, no fee applied yet - see Models.Warrior.DramatisPersonaId's own
/// doc) for a character - blocking Next if the checked ITEM total would exceed the warband's treasury
/// (recruiting a character is currently free, doesn't affect this). Only entries actually bought/
/// recruited (IsPurchased/IsRecruited) are committed at Save - see WarbandDetailViewModel.EndOfGame.
/// ApplyRareItemSearchAsync. Henchmen never search ("Whenever a HERO wants to buy..."/"Heroes looking
/// for...") - entries are built once per Hero in the main constructor, this file only orchestrates both
/// steps. Dramatis Personae never search either (2026-09-03, user feedback) even though Warrior.IsHero
/// is true for them (same Serious Injury/XP flow as a real Hero) - excluded explicitly at the
/// RareItemSearchEntries construction site and in HasEligibleHeroesForRareItems below, same limitation
/// already fixed for SurvivingHeroCount (EndOfGamePageViewModel.Exploration.cs).</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>[ObservableProperty] plutôt qu'un simple { get; private set; } - même raison que
    /// EndOfGamePageViewModel.WarriorRows (voir sa doc) : peuplée dans InitializeAsync/
    /// WireUpChangeNotifications (async), pas le constructeur, donc a besoin de la notification
    /// PropertyChanged générée pour que les bindings XAML déjà posés apprennent la nouvelle instance.</summary>
    [ObservableProperty]
    private ObservableCollection<RareItemSearchEntry> rareItemSearchEntries = new();

    /// <summary>Drives whether the search step exists at all (Steps) - "Warriors taken out of action
    /// during the last battle may not look for rare items", so a Hero currently marked Hors de combat
    /// doesn't count even though their RareItemSearchEntries row still technically exists (hidden, not
    /// removed - see the constructor). Dramatis Personae excluded too (2026-09-03, user feedback - "les
    /// dramatis personae ne peuvent pas trouver des objets rares/personnages spéciaux, c'est que les
    /// héros") - same exclusion as RareItemSearchEntries itself (main constructor); kept as a separate
    /// check on WarriorRows rather than derived from RareItemSearchEntries so both conditions stay
    /// obviously in sync.</summary>
    public bool HasEligibleHeroesForRareItems => WarriorRows.Any(r => r.Warrior.IsHero && !r.IsOutOfAction && !r.Warrior.IsDramatisPersona);

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

    /// <summary>Opens the Trading Post picker via PickRareEquipmentAsync (IEquipmentPickerService) - the
    /// player is nominating exactly ONE item to attempt this Hero's single roll against ("You may also
    /// only make one roll for each Hero"), never a normal multi-item purchase. Melee weapon tiles show a
    /// Gromril/Ithilmar variant right alongside the plain one (2026-09-23, retour utilisateur - "on
    /// pourrait directement proposer les armes en ithilmar et les armes en gromril à l'achat plutôt que
    /// d'avoir un sélecteur neutre et devoir cliquer sur une chip") - picking one sets SelectedMaterial
    /// directly, no separate pill tap needed afterward (the pills on this step stay as a fallback/
    /// override, unchanged). availableGold IS passed for ShowBudget's informational display - this step
    /// commits nothing, the real cost/affordability is decided independently at the Achat step that
    /// follows (RareItemPurchaseTotalCost/IsRareItemPurchaseBlocked). Scoped to the UNION of every
    /// equipment list this warband's recruitable WarriorArchetypes use (allowedEquipmentListItemIds,
    /// resolved fresh on each open rather than cached - a handful of GetEquipmentListItemIdsAsync calls,
    /// cheap) - NOT the searching Hero's own list, since "un héros peut chercher un équipement qui n'est
    /// pas forcément pour lui" (found item goes to the band's stash, not straight onto this Hero - user
    /// request 2026-08-28). Picking again replaces any previous choice and clears the roll/material/
    /// purchase state (see RareItemSearchEntry.OnSelectedItemChanged - SelectedItem MUST be set before
    /// SelectedMaterial below, its changed-hook resets the latter to null), same as ResolveExplorationResult
    /// resetting downstream state on a new die.</summary>
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

        var picked = await _equipmentPicker.PickRareEquipmentAsync(_warbandArchetypeId, allowedItemIds, RareItemPurchaseRemainingTreasury);
        if (picked is not { } result) return;

        entry.SelectedItem = result.Item;
        entry.SelectedMaterial = result.MaterialRule;
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

    /// <summary>Narrowed to this warband via _warbandArchetypeId (same idiom as SelectRareItem/
    /// AllowedWarbandArchetypeId elsewhere) - a Dramatis Persona with a non-empty
    /// RestrictedToWarbandArchetypeIds only shows up in the picker when this warband is in that list
    /// (e.g. Veskit only for Skaven of Clan Eshin, Bertha only for Sisters of Sigmar). Also excludes
    /// anyone currently on cooldown for this warband (_cooldownDramatisPersonaIds, 2026-09-01 - Aenur/
    /// Ulli &amp; Marquand's "can't be sought again until the warband has fought at least one battle
    /// without them"). Picking again replaces any previous choice and clears the roll state (see
    /// RareItemSearchEntry.OnSelectedCharacterChanged). Single-select only (IDramatisPersonaPickerService.
    /// PickDramatisPersonaAsync) - "you may only make one roll for each Hero", same rule as the Objet
    /// side.</summary>
    [RelayCommand]
    private async Task SelectCharacter(RareItemSearchEntry entry)
    {
        var picked = await _dramatisPersonaPicker.PickDramatisPersonaAsync(_warbandArchetypeId, _cooldownDramatisPersonaIds);
        if (picked is null) return;

        entry.SelectedCharacter = picked;
    }

    /// <summary>Résumé de paire si le personnage recherché/trouvé en fait partie (2026-09-01, même
    /// logique que DramatisPersonaViewModel.ShowDetails en mode picker) - ce contexte (recherche/achat
    /// "Personnage spécial") est du recrutement, pas de la consultation Codex.</summary>
    [RelayCommand]
    private Task ShowCharacterDetail(DramatisPersona character) =>
        character.PairedWithDramatisPersona is not null
            ? _detailDialogs.ShowDramatisPersonaPairDetailDialogAsync(character)
            : _detailDialogs.ShowDramatisPersonaDetailDialogAsync(character);

    [RelayCommand]
    private void ClearCharacter(RareItemSearchEntry entry) => entry.SelectedCharacter = null;

    /// <summary>Only a Hero with something actually searchable needs a roll - HasRarity is false for a
    /// common non-weapon item (nothing to roll against) or a common melee weapon with no material chosen
    /// yet, in both cases simply not a real search attempt (optional, per the book). Same idea on the
    /// Personnage side: a character picked but no roll yet blocks Next, nobody picked (nobody sought)
    /// doesn't.</summary>
    private bool ValidateRareItemsStep()
    {
        var valid = true;
        foreach (var entry in RareItemSearchEntries.Where(e => !e.Hero.IsOutOfAction))
        {
            if (entry.IsSearchingForCharacter && entry.HasSelectedCharacter)
                valid &= CheckRoll(entry.CharacterTotalRoll is null, () => entry.CharacterRollError = Loc["EndOfGameRollRequired"]);
            else if (!entry.IsSearchingForCharacter && entry.HasRarity)
                valid &= CheckRoll(entry.TotalRoll is null, () => entry.RollError = Loc["EndOfGameRollRequired"]);
        }
        return valid;
    }

    // --- Étape Achat (séparée, voir IsRareItemPurchaseStep) --------------------------------------

    /// <summary>Only the successful finds get a card on this step - a failed search has nothing to
    /// buy/recruit. Covers BOTH modes (RareItemSearchEntry.IsFound: IsSuccess for Objet, IsCharacterFound
    /// for Personnage) since the step now shows a "Recruter ?" card alongside "Acheter ?" cards.</summary>
    public List<RareItemSearchEntry> RareItemsWithResults => RareItemSearchEntries.Where(e => e.IsFound).ToList();

    [RelayCommand]
    private void AutoRollRarePrice(RareItemSearchEntry entry) => entry.PriceRoll = Random.Shared.Next(1, 7).ToString();

    /// <summary>Base commune de trésorerie pour TOUT le wizard (pas seulement cette étape, malgré son nom
    /// - voir EndOfGamePageViewModel.EndOfGameTreasuryRemaining, seul appelant en dehors de ce fichier
    /// depuis le 2026-09-03) : la trésorerie de la bande avant cette Fin de Partie, plus ce que CETTE
    /// partie a rapporté (or d'Exploration + Vente de pierres magiques, WyrdstoneSaleValue - cette étape
    /// précède celle-ci dans l'ordre du wizard, son gain est donc déjà connu). Purely a live preview -
    /// nothing is written to the real Warband until WarbandDetailViewModel.EndOfGame saves.</summary>
    private int RareItemBaselineTreasury => _currentTreasury
        + (ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold && int.TryParse(ExplorationGoldAmount, out var gold) ? gold : 0)
        + WyrdstoneSaleValue;

    /// <summary>Sum of EffectiveCost for every entry still checked "Acheter", PLUS EffectiveHireCostForTreasury
    /// for every recruited Gold-fee character still paying in gold (2026-09-01, user request - a
    /// character's hire fee competes for the same treasury as a rare item purchase, same affordability
    /// block). A variable-price item not yet rolled (EffectiveCost null) simply doesn't count yet, so the
    /// running total understates until every price is known (ValidateRareItemPurchaseStep blocks Next
    /// until then anyway).</summary>
    public int RareItemPurchaseTotalCost => RareItemsWithResults.Where(e => e.WantsToBuy).Sum(e => e.EffectiveCost ?? 0)
        + RareItemsWithResults.Where(e => e.IsRecruited).Sum(e => e.EffectiveHireCostForTreasury);

    /// <summary>Alias de EndOfGameTreasuryRemaining (2026-09-03, retour utilisateur - "on n'utilise pas le
    /// même solde de trésorerie à la corruption et à l'achat d'objet rare... un seul solde qu'on ajuste au
    /// fur et à mesure des étapes") : cette étape n'a plus SA PROPRE version isolée du solde restant,
    /// juste un nom conservé pour l'affichage/la compatibilité de cette page (RareItemPurchaseRemainingTreasuryDisplay/
    /// IsRareItemPurchaseBlocked en aval).</summary>
    public int RareItemPurchaseRemainingTreasury => EndOfGameTreasuryRemaining;

    /// <summary>Formatted display text - computed here rather than nesting {loc:Loc} inside a
    /// StringFormat attribute (not valid XAML, see WyrdstoneSaleValueDisplay).</summary>
    public string RareItemPurchaseRemainingTreasuryDisplay => string.Format(Loc["EndOfGameRareItemTreasuryFormat"], RareItemPurchaseRemainingTreasury);

    /// <summary>True once the checked total would push the warband into the red - blocks Next entirely
    /// (user request 2026-08-28: "bloquer le step si trésorerie négative") rather than silently skipping
    /// an unaffordable find like an earlier pass of this step did. The player resolves it themselves by
    /// unchecking something.</summary>
    public bool IsRareItemPurchaseBlocked => RareItemPurchaseRemainingTreasury < 0;

    // --- Pierres magiques (Nicodemus, FeeKind.Wyrdstone - 2026-09-01, "on a qu'a brancher la wyrstone à
    // son paiement") : même principe que la trésorerie ci-dessus, mais pour le stock de pierres magiques
    // plutôt que l'or - Nicodemus n'a "aucun intérêt pour l'or", il n'existe aucune option de paiement en
    // or pour lui (contrairement à Johann/Veskit/Marianna). ---------------------------------------------

    /// <summary>True only when at least one entry this step involves a Wyrdstone-fee character - pilote
    /// la visibilité du bandeau "pierres magiques restantes" (pas affiché pour une Fin de Partie sans
    /// Nicodemus, même principe que HasHiredSwordUpkeep).</summary>
    public bool HasWyrdstoneCostEntries => RareItemsWithResults.Any(e => e.HasWyrdstoneCost);

    /// <summary>Stock déjà connu à ce stade du wizard : l'ancien stock MOINS ce que le joueur a choisi de
    /// vendre à l'étape Vente de pierres magiques (ShardsToSell/MaxShardsToSell, voir PostBattle.cs) -
    /// cette étape se déroule AVANT celle-ci dans l'ordre du wizard (Steps), son choix est donc déjà
    /// définitif. Même limite que RareItemBaselineTreasury/HiredSwordTreasuryAfter : ne tient pas compte
    /// d'une dépense concurrente dans une AUTRE étape (ex. Francs-Tireurs, qui ne consomme de toute façon
    /// jamais de pierres magiques) - pas un problème ici puisque cette étape est la seule à en dépenser.</summary>
    private int RareItemBaselineWyrdstoneShards => MaxShardsToSell - ShardsToSell;

    /// <summary>Sum of EffectiveWyrdstoneCostForShards for every recruited Wyrdstone-fee character (en
    /// pratique, au plus un seul Héros peut réellement recruter Nicodemus dans une même Fin de Partie -
    /// un seul exemplaire existe dans le catalogue).</summary>
    public int RareItemPurchaseTotalWyrdstoneCost => RareItemsWithResults.Where(e => e.IsRecruited).Sum(e => e.EffectiveWyrdstoneCostForShards);

    public int RareItemPurchaseRemainingWyrdstoneShards => RareItemBaselineWyrdstoneShards - RareItemPurchaseTotalWyrdstoneCost;

    /// <summary>Formatted display text - computed here rather than nesting {loc:Loc} inside a
    /// StringFormat attribute (not valid XAML, see RareItemPurchaseRemainingTreasuryDisplay).</summary>
    public string RareItemPurchaseRemainingWyrdstoneShardsDisplay => string.Format(Loc["EndOfGameWyrdstoneShardsRemainingFormat"], RareItemPurchaseRemainingWyrdstoneShards);

    /// <summary>Same "block Next entirely" spirit as IsRareItemPurchaseBlocked, for the shard stock
    /// instead of gold - "if the warband has no shard to give... he leaves and never returns", so
    /// recruiting Nicodemus without enough shards left simply can't be confirmed.</summary>
    public bool IsRareItemPurchaseWyrdstoneBlocked => RareItemPurchaseRemainingWyrdstoneShards < 0;

    /// <summary>Blocks Next until every checked variable-price find has its supplement rolled (a price
    /// can't be judged affordable while still unknown), then blocks again if the checked total exceeds
    /// the treasury (IsRareItemPurchaseBlocked) or the Wyrdstone shard stock (IsRareItemPurchaseWyrdstoneBlocked).</summary>
    private bool ValidateRareItemPurchaseStep()
    {
        var valid = true;
        foreach (var entry in RareItemsWithResults.Where(e => e.WantsToBuy && e.HasVariablePrice))
            valid &= CheckRoll(entry.PriceSupplement is null, () => entry.PriceRollError = Loc["EndOfGameRollRequired"]);

        // Pas un champ précis à mettre en défaut (voir IsRareItemPurchaseBlocked/IsRareItemPurchaseWyrdstoneBlocked) -
        // un bandeau dédié en XAML les affiche directement, pas besoin de passer par CheckRoll/un message
        // par entrée.
        return valid && !IsRareItemPurchaseBlocked && !IsRareItemPurchaseWyrdstoneBlocked;
    }
}
