using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One Hero's rare item search attempt (post-battle sequence step 6, "Rare items" - p.145):
/// "Whenever a Hero wants to buy a rare item, roll 2D6 and compare the result to the [Rarity] number
/// stated... You may also only make one roll for each Hero looking for rare items." Nominating an item
/// is optional (a Hero may simply not search) - see EndOfGameDialogViewModel.RareItems.cs for the
/// availability-roll step this backs (one entry per living Hero not taken Out of Action this battle) and
/// the separate Purchase step (WantsToBuy/PriceRoll below) that follows it - a distinct step rather than
/// an implicit auto-buy on success, per user request 2026-08-28: "1re étape : jet de dispo. Si roll
/// réussi, 2e étape (nouveau step) Achat : case à cocher, jet de valeur pour les prix variables, bloquer
/// le step si trésorerie négative."</summary>
public partial class RareItemSearchEntry : ObservableObject
{
    private readonly LocalizationService _loc;

    /// <summary>Snapshot of EquipmentItem ids the warband's UNASSIGNED inventory currently holds at least
    /// one of (see WarbandDetailViewModel.EndOfGame - the same Inventory collection the roster page
    /// already shows), used only to decide whether HasAlternativePaymentOption can offer itself at all
    /// (e.g. Johann/Crimson Shade - no point showing "pay with X" if the band owns none). Frozen at
    /// wizard-open time like every other snapshot in this class (Bonus, EffectiveRarity...) - equipment
    /// can't actually change mid-wizard.</summary>
    private readonly IReadOnlyCollection<int> _ownedEquipmentItemIds;

    public WarriorOutcomeRow Hero { get; }
    public string HeroName => Hero.Name;

    /// <summary>False = searching for a rare item (the original mode, everything below IsSearchingForCharacter
    /// unchanged). True = searching for a special character instead - "Heroes who are looking for a
    /// special character cannot look for rare items", so this is a genuine either/or switch per Hero, not
    /// two independent searches (user request 2026-08-28: "un switch Hero/Objet... si on change de puce
    /// hero/objet, on reset la sélection" - toggling always clears BOTH sides' state, see
    /// OnIsSearchingForCharacterChanged).</summary>
    [ObservableProperty]
    private bool isSearchingForCharacter;

    partial void OnIsSearchingForCharacterChanged(bool value)
    {
        SelectedItem = null; // cascade via OnSelectedItemChanged: Material/Roll/WantsToBuy/PriceRoll
        SelectedCharacter = null; // cascade via OnSelectedCharacterChanged: Roll/RollError
    }

    /// <summary>Backs the two-segment switch in XAML (Objet/Personnage) - takes the target mode directly
    /// (x:Boolean CommandParameter on each half) rather than toggling, since each half of the switch always
    /// sets a specific side regardless of current state.</summary>
    [RelayCommand]
    private void SetSearchMode(bool searchingForCharacter) => IsSearchingForCharacter = searchingForCharacter;

    /// <summary>Null = not currently searching for anything - a Hero isn't required to make an attempt.
    /// Picking a new item clears any previously chosen SelectedMaterial/roll/purchase state (see
    /// OnSelectedItemChanged) - nothing about the last item carries over to a different one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    [NotifyPropertyChangedFor(nameof(IsMaterialEligible))]
    [NotifyPropertyChangedFor(nameof(EffectiveRarity))]
    [NotifyPropertyChangedFor(nameof(RarityDisplay))]
    [NotifyPropertyChangedFor(nameof(HasRarity))]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(HasVariablePrice))]
    [NotifyPropertyChangedFor(nameof(CostDisplay))]
    [NotifyPropertyChangedFor(nameof(EffectiveCost))]
    [NotifyPropertyChangedFor(nameof(IsPurchased))]
    [NotifyPropertyChangedFor(nameof(ResultDisplay))]
    private EquipmentItem? selectedItem;

    partial void OnSelectedItemChanged(EquipmentItem? value)
    {
        SelectedMaterial = null;
        Roll = string.Empty;
        RollError = null;
        WantsToBuy = true;
        PriceRoll = string.Empty;
        PriceRollError = null;
    }

    /// <summary>Only meaningful when IsMaterialEligible - a common melee weapon (Rarity null) forged in
    /// Gromril/Ithilmar becomes a Rare find in its own right, at the MATERIAL's Rarity rather than the
    /// weapon's own (see EffectiveRarity) - user request 2026-08-28: "il manque la gestion pour les
    /// armes du gromril/ithilmar qui augmente la rareté d'une arme de corps à corps en commun". Attached
    /// to the purchased WarbandEquipment on success, same MaterialRule mechanism as a normal purchase.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveRarity))]
    [NotifyPropertyChangedFor(nameof(RarityDisplay))]
    [NotifyPropertyChangedFor(nameof(HasRarity))]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(CostDisplay))]
    [NotifyPropertyChangedFor(nameof(EffectiveCost))]
    [NotifyPropertyChangedFor(nameof(IsPurchased))]
    [NotifyPropertyChangedFor(nameof(ResultDisplay))]
    private SpecialRule? selectedMaterial;

    /// <summary>Tap-to-select on the pill itself (not a separate RadioButton, judged too plain visually -
    /// user request 2026-08-28) - tapping the already-selected material deselects it, same "tap toggles"
    /// idiom as EquipmentItemViewModel.Select. Compares by Id, never by reference: RareMaterialOptions is
    /// one catalog list SHARED across every Hero's entry, so a plain reference check would still work
    /// here, but Id comparison stays correct even if a future caller ever passes a differently-instanced
    /// copy of the same catalog row.</summary>
    [RelayCommand]
    private void SelectMaterial(SpecialRule material) => SelectedMaterial = SelectedMaterial?.Id == material.Id ? null : material;

    /// <summary>Free-typed 2D6 total, same "string Entry" idiom as every other roll in this wizard - the
    /// bonus below (Bonus) is added on top when comparing to EffectiveRarity, never baked into what the
    /// player types.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalRoll))]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(IsPurchased))]
    [NotifyPropertyChangedFor(nameof(ResultDisplay))]
    private string roll = string.Empty;

    [ObservableProperty]
    private string? rollError;

    partial void OnRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) RollError = null; }

    /// <summary>From whatever this Hero currently carries (e.g. the Jewelsmith's kept gems) - see
    /// Core.Rules.RareItemSearchBonus. Live (not cached): equipment can't actually change mid-wizard, but
    /// reading it fresh costs nothing and avoids a staleness trap.</summary>
    public int Bonus => RareItemSearchBonus.EffectiveBonus(Hero.Warrior);

    public bool HasBonus => Bonus != 0;

    public string BonusDisplay => string.Format(_loc["EndOfGameRareItemBonusFormat"], Bonus);

    public bool HasSelectedItem => SelectedItem is not null;

    /// <summary>Any melee weapon can be forged into Gromril/Ithilmar - common (Rarity null, becomes
    /// searchable at the material's Rarity, see EffectiveRarity) or already Rare (searched at its own
    /// Rarity either way, the material is just an optional extra attached on top - user request
    /// 2026-08-28: "toute arme de corps à corps peuvent être en gromril, même les armes rares"). Never
    /// offered for a non-weapon (no material role for it in the book).</summary>
    public bool IsMaterialEligible => SelectedItem?.Category == EquipmentCategory.MeleeWeapon;

    /// <summary>The higher of the item's own Rarity and the chosen material's (Gromril 11/Ithilmar 9) -
    /// user request 2026-08-28: forging an already-Rare weapon into Gromril/Ithilmar takes the harder of
    /// the two thresholds, not just the weapon's own. Either alone if only one is set; null for a common
    /// item with no material chosen, meaning there's nothing to roll against (it's simply available, no
    /// search needed).</summary>
    public int? EffectiveRarity => (SelectedItem?.Rarity, SelectedMaterial?.Rarity) switch
    {
        ({ } itemRarity, { } materialRarity) => Math.Max(itemRarity, materialRarity),
        ({ } itemRarity, null) => itemRarity,
        (null, { } materialRarity) => materialRarity,
        _ => null
    };

    public bool HasRarity => EffectiveRarity is not null;

    public string RarityDisplay => EffectiveRarity is { } r ? string.Format(_loc["EndOfGameRareItemRarityFormat"], r) : string.Empty;

    public int? TotalRoll => int.TryParse(Roll, out var r) ? r + Bonus : null;

    public bool HasResult => EffectiveRarity is not null && TotalRoll is not null;

    /// <summary>"If the roll is equal or greater, the item is available." Purely about the DICE - whether
    /// the Hero actually WALKS AWAY with the item is a separate question, decided on the following
    /// Purchase step (WantsToBuy) rather than automatically here. False (not null) once HasResult is true
    /// and the roll falls short - IsVisible bindings in XAML key off HasResult, not this, to tell "no
    /// result yet" apart from "found nothing".</summary>
    public bool IsSuccess => HasResult && TotalRoll >= EffectiveRarity;

    public string ResultDisplay => !HasResult
        ? string.Empty
        : string.Format(_loc[IsSuccess ? "EndOfGameRareItemSuccessFormat" : "EndOfGameRareItemFailureFormat"], TotalRoll);

    // --- Étape Achat (livre, suite de la même étape 6 - séparée en carte à part, voir
    // EndOfGameDialogViewModel.IsRareItemPurchaseStep) -------------------------------------------

    /// <summary>Null = fixed price (EquipmentItem.Cost alone). Non-null = a random supplement must be
    /// rolled on top before the real price is known (e.g. "10 gc + 1D6") - user request 2026-08-28,
    /// "attention au prix variable !", a gap the first pass of this step missed entirely.</summary>
    public bool HasVariablePrice => SelectedItem?.CostRandomMax is not null;

    /// <summary>The fixed part of the price, already Gromril/Ithilmar-adjusted if a material is attached
    /// (Core.Rules.EquipmentPricing.CalculateCost, same formula as a normal purchase) - the random
    /// supplement (PriceRoll) is always additive on top of this, never itself multiplied by the material.</summary>
    public int? BaseCost => SelectedItem is null ? null : EquipmentPricing.CalculateCost(SelectedItem.Cost, SelectedMaterial?.CostMultiplier, isFree: false);

    /// <summary>Fixed price: the real number straight away ("18 gc"). Variable price, not yet rolled: a
    /// prompt to roll it rather than a vague "18 gc + up to 18 gc" range (user request 2026-08-28: "on
    /// roll mais on ne sait pas exactement combien ça coûte" - the range was more confusing than helpful).
    /// Variable price, rolled: the real resolved total ("140 gc"), same as a fixed price from that point
    /// on - EffectiveCost, not BaseCost, once it's known.</summary>
    public string CostDisplay => !HasVariablePrice
        ? (BaseCost is { } fixedCost ? string.Format(_loc["EndOfGameRareItemFixedCostFormat"], fixedCost) : string.Empty)
        : EffectiveCost is { } resolvedCost
            ? string.Format(_loc["EndOfGameRareItemFixedCostFormat"], resolvedCost)
            : _loc["EndOfGameRareItemRollPricePrompt"];

    /// <summary>Free-typed roll for the random price supplement - only relevant/shown when
    /// HasVariablePrice is true. Blank (not 0) while unrolled, same "string Entry" idiom as every other
    /// roll in this wizard.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriceSupplement))]
    [NotifyPropertyChangedFor(nameof(EffectiveCost))]
    [NotifyPropertyChangedFor(nameof(CostDisplay))]
    [NotifyPropertyChangedFor(nameof(IsPurchased))]
    private string priceRoll = string.Empty;

    [ObservableProperty]
    private string? priceRollError;

    partial void OnPriceRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) PriceRollError = null; }

    /// <summary>0 for a fixed-price item (nothing to roll) - null for a variable-price item whose
    /// supplement hasn't been rolled yet, distinguishing "no extra cost" from "price not yet known".</summary>
    public int? PriceSupplement => !HasVariablePrice ? 0 : (int.TryParse(PriceRoll, out var r) ? r : null);

    /// <summary>The real total gc cost - null while the price isn't fully known yet (variable-price item,
    /// supplement not rolled). Never computed until BOTH BaseCost and PriceSupplement resolve, so a
    /// premature "affordable" read never happens on a half-rolled price.</summary>
    public int? EffectiveCost => BaseCost is { } b && PriceSupplement is { } s ? b + s : null;

    /// <summary>Whether the player wants this find bought at all - defaults true (finding it usually means
    /// wanting it), but purchase is never automatic/implicit: the player can still decline (e.g. to keep
    /// gold for something else), and the whole Purchase step blocks progression if the CHECKED total
    /// would exceed the warband's treasury (see EndOfGameDialogViewModel.RareItemPurchaseRemainingTreasury) -
    /// there's deliberately no per-entry "can't afford" auto-skip here, unlike an earlier pass of this
    /// step. Only meaningful when IsSuccess.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPurchased))]
    private bool wantsToBuy = true;

    /// <summary>The item is bought - treasury debited, added to the band's (unassigned) stash with its
    /// material attached - only if all three hold: the availability roll succeeded, the player left
    /// "Acheter" checked, and the price is fully known (fixed, or a variable supplement already rolled).
    /// Consumed by WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync.</summary>
    public bool IsPurchased => IsSuccess && WantsToBuy && EffectiveCost.HasValue;

    // --- Recherche de Personnage Spécial (livre, "Looking for special characters" p.146) - alternative
    // à la recherche d'objet rare ci-dessus, voir IsSearchingForCharacter -----------------------------

    /// <summary>Null = aucun personnage choisi - une recherche est facultative, comme pour SelectedItem.
    /// Choisi via DramatisPersonaPickerService (voir EndOfGameDialogViewModel.RareItems.cs), narrowé aux
    /// personnages éligibles à la bande du Héros qui cherche (RestrictedToWarbandArchetypeIds).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCharacter))]
    [NotifyPropertyChangedFor(nameof(HasCharacterResult))]
    [NotifyPropertyChangedFor(nameof(IsCharacterFound))]
    [NotifyPropertyChangedFor(nameof(CharacterResultDisplay))]
    [NotifyPropertyChangedFor(nameof(HasHireCost))]
    [NotifyPropertyChangedFor(nameof(HireCostDisplay))]
    [NotifyPropertyChangedFor(nameof(HasAlternativePaymentOption))]
    [NotifyPropertyChangedFor(nameof(AlternativePaymentItemLabel))]
    [NotifyPropertyChangedFor(nameof(IsPayingWithAlternativeItem))]
    [NotifyPropertyChangedFor(nameof(EffectiveHireCostForTreasury))]
    [NotifyPropertyChangedFor(nameof(HasWyrdstoneCost))]
    [NotifyPropertyChangedFor(nameof(EffectiveWyrdstoneCostForShards))]
    [NotifyPropertyChangedFor(nameof(HasEngagementCost))]
    [NotifyPropertyChangedFor(nameof(EngagementCostDisplay))]
    [NotifyPropertyChangedFor(nameof(SelectedCharacterDisplayName))]
    private DramatisPersona? selectedCharacter;

    partial void OnSelectedCharacterChanged(DramatisPersona? value)
    {
        CharacterRoll = string.Empty;
        CharacterRollError = null;
        WantsToPayWithAlternativeItem = false;
    }

    public bool HasSelectedCharacter => SelectedCharacter is not null;

    /// <summary>ChipView.NameOverride for the chosen character's chip (2026-09-01, Une Poignée d'Or) -
    /// null unless SelectedCharacter is one half of a pair, in which case Marquand alone (the only half
    /// ever actually chosen here, see HasHireCost's own doc) must read "Marquand Volker &amp; Ulli Leitpold"
    /// rather than his bare Item.Name, same reasoning/precedent as DramatisPersonaRow.DisplayName in the
    /// picker tile itself.</summary>
    public string? SelectedCharacterDisplayName => SelectedCharacter?.PairedWithDramatisPersona is { } partner
        ? $"{SelectedCharacter.Name} & {partner.Name}"
        : null;

    /// <summary>Free-typed 1D6 roll, same "string Entry" idiom as every other roll in this wizard - no
    /// Core.Rules.RareItemSearchBonus here, the book only ever compares this roll to Initiative, nothing
    /// else modifies it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterTotalRoll))]
    [NotifyPropertyChangedFor(nameof(HasCharacterResult))]
    [NotifyPropertyChangedFor(nameof(IsCharacterFound))]
    [NotifyPropertyChangedFor(nameof(CharacterResultDisplay))]
    private string characterRoll = string.Empty;

    [ObservableProperty]
    private string? characterRollError;

    partial void OnCharacterRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) CharacterRollError = null; }

    public int CharacterInitiative => Hero.Warrior.Initiative;

    /// <summary>Computed here rather than a nested {loc:Loc} inside a StringFormat attribute (not valid
    /// XAML, see CostDisplay above).</summary>
    public string CharacterInitiativeDisplay => string.Format(_loc["EndOfGameRareItemCharacterInitiativeFormat"], CharacterInitiative);

    public int? CharacterTotalRoll => int.TryParse(CharacterRoll, out var r) ? r : null;

    public bool HasCharacterResult => HasSelectedCharacter && CharacterTotalRoll is not null;

    /// <summary>"If any of the searchers rolls under his Initiative he has located the special character" -
    /// strictly UNDER, unlike the Rare Item search's "equal or greater" against Rarity above. Ne tient PAS
    /// compte d'un éventuel DramatisPersona.RequiresRatingDisadvantage (ex. Bertha) et ne le fera jamais
    /// ICI : ce jet compare l'écart de Valeur avec le PROCHAIN adversaire, qui n'est pas encore connu à la
    /// fin de la partie courante (voir DramatisPersona.RequiresRatingDisadvantage) - un futur flux "Début
    /// de partie" en sera responsable, pas ce wizard de Fin de Partie.</summary>
    public bool IsCharacterFound => HasCharacterResult && CharacterTotalRoll < CharacterInitiative;

    /// <summary>Same generic "Trouvé !"/"Introuvable" wording as ResultDisplay (Rare Item search) - the
    /// phrasing doesn't name what was being searched for either way, so it reads correctly for both.</summary>
    public string CharacterResultDisplay => !HasCharacterResult
        ? string.Empty
        : string.Format(_loc[IsCharacterFound ? "EndOfGameRareItemSuccessFormat" : "EndOfGameRareItemFailureFormat"], CharacterTotalRoll);

    [RelayCommand]
    private void AutoRollCharacter() => CharacterRoll = Random.Shared.Next(1, 7).ToString();

    /// <summary>Whether the player wants this found character actually recruited - defaults true, same
    /// "found usually means wanting it, but never automatic" spirit as WantsToBuy. No hire fee applied
    /// yet regardless (see Models.Warrior.DramatisPersonaId's own doc - deliberately deferred), so unlike
    /// WantsToBuy this never blocks the wizard on affordability.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecruited))]
    private bool wantsToRecruit = true;

    /// <summary>Consumed by WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync - a found character
    /// actually joins the roster only if the player left "Recruter" checked.</summary>
    public bool IsRecruited => IsCharacterFound && WantsToRecruit;

    /// <summary>Unified "this entry has a positive result" regardless of mode - IsSuccess (Objet) or
    /// IsCharacterFound (Personnage) depending on IsSearchingForCharacter. Feeds the shared Purchase step
    /// list (EndOfGameDialogViewModel.RareItems.cs.RareItemsWithResults), which now shows a card per
    /// successful search of EITHER kind.</summary>
    public bool IsFound => IsSearchingForCharacter ? IsCharacterFound : IsSuccess;

    // --- Frais d'engagement (2026-09-01, user request - "on va construire un vrai truc") -------------
    // Gold (Johann/Veskit/Marianna) ET Pair (Ulli & Marquand, 2026-09-01 - "30 Couronnes d'Or pour les
    // deux") partagent le même mécanisme (déduction directe de HireCost sur la trésorerie) : Pair n'est
    // qu'un FeeKind différent pour la MÊME devise, contrairement à Wyrdstone qui en est une vraie autre -
    // pas besoin d'un chemin séparé. Recruter le personnage "caché" du duo (Ulli, jamais son propre choix
    // dans le picker - voir DramatisPersona.IsHiddenFromSearchPicker) ne double jamais ce coût : seul
    // Marquand est un vrai RareItemSearchEntry.SelectedCharacter possible, Ulli est recrutée EN PLUS de
    // lui sans frais propre (voir WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync). None
    // (Bertha) ne prélève rien de toute façon, Wyrdstone (Nicodemus) reste séparé (vraie autre devise, son
    // propre bandeau "pierres magiques restantes" - voir EndOfGameDialogViewModel.RareItems.cs).
    public bool HasHireCost => SelectedCharacter is { HireCost: not null } and { FeeKind: DramatisPersonaHireFeeKind.Gold or DramatisPersonaHireFeeKind.Pair };

    /// <summary>"Gratuit" once IsPayingWithAlternativeItem is checked - reflects EffectiveHireCostForTreasury
    /// (what's ACTUALLY charged, 0 in that case), not the raw catalog HireCost, which stayed shown as "70"
    /// even after choosing the alternative item until this fix (2026-09-01, user report: "si on coche
    /// l'ombre cramoisie, le recrutement doit être gratuit... on voit toujours noté 70" - the treasury
    /// total was already correctly adjusted, only this label lagged behind).</summary>
    public string HireCostDisplay => !HasHireCost
        ? string.Empty
        : IsPayingWithAlternativeItem
            ? _loc["EndOfGameCharacterHireCostFree"]
            : string.Format(_loc["EndOfGameRareItemFixedCostFormat"], SelectedCharacter!.HireCost!.Value);

    /// <summary>Nicodemus only (FeeKind.Wyrdstone) - "he has no interest in gold... must be paid a
    /// wyrdstone shard when he joins the warband". Always exactly 1 shard, never stored as a number on
    /// DramatisPersona (unlike HireCost) since the book never varies it.</summary>
    public bool HasWyrdstoneCost => SelectedCharacter?.FeeKind == DramatisPersonaHireFeeKind.Wyrdstone;

    public int EffectiveWyrdstoneCostForShards => HasWyrdstoneCost ? 1 : 0;

    /// <summary>Unified "there's an engagement cost to show" regardless of currency - drives ONE shared
    /// XAML block (Grid + "Frais d'engagement" label) instead of a near-duplicate block per currency,
    /// mutually exclusive by construction (HasHireCost requires FeeKind.Gold, HasWyrdstoneCost requires
    /// FeeKind.Wyrdstone).</summary>
    public bool HasEngagementCost => HasHireCost || HasWyrdstoneCost;

    public string EngagementCostDisplay => HasWyrdstoneCost ? _loc["EndOfGameCharacterWyrdstoneCostValue"] : HireCostDisplay;

    /// <summary>True only when this character actually HAS an alternative payment item (DramatisPersona.
    /// AlternativePaymentItemId, e.g. Johann/Crimson Shade) AND the warband's inventory currently owns at
    /// least one unit of it - no point offering a choice the band can't actually make.</summary>
    public bool HasAlternativePaymentOption => HasHireCost
        && SelectedCharacter!.AlternativePaymentItemId is { } itemId
        && _ownedEquipmentItemIds.Contains(itemId);

    /// <summary>Computed here rather than a nested {loc:Loc} inside a StringFormat attribute (not valid
    /// XAML, same idiom as CostDisplay/CharacterInitiativeDisplay above).</summary>
    public string AlternativePaymentItemLabel => HasAlternativePaymentOption
        ? string.Format(_loc["EndOfGameCharacterAlternativePaymentFormat"], SelectedCharacter!.AlternativePaymentItem!.Name)
        : string.Empty;

    /// <summary>Player's choice when HasAlternativePaymentOption is true - defaults false (pay gold).
    /// Meaningless (ignored at Apply time) when HasAlternativePaymentOption is false, even if somehow left
    /// true from a previous SelectedCharacter (reset in OnSelectedCharacterChanged regardless).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPayingWithAlternativeItem))]
    [NotifyPropertyChangedFor(nameof(EffectiveHireCostForTreasury))]
    [NotifyPropertyChangedFor(nameof(HireCostDisplay))]
    [NotifyPropertyChangedFor(nameof(EngagementCostDisplay))]
    private bool wantsToPayWithAlternativeItem;

    public bool IsPayingWithAlternativeItem => HasAlternativePaymentOption && WantsToPayWithAlternativeItem;

    /// <summary>What actually gets deducted from the treasury for this entry - 0 whenever gold isn't the
    /// payment method (no hire cost at all, or paying with the alternative item instead). Feeds
    /// EndOfGameDialogViewModel.RareItems.cs's RareItemPurchaseTotalCost, the same running total/
    /// affordability block already used for rare item purchases - a character's gold fee competes for the
    /// same treasury, same "bloquer le step si trésorerie négative" rule.</summary>
    public int EffectiveHireCostForTreasury => HasHireCost && !IsPayingWithAlternativeItem ? SelectedCharacter!.HireCost!.Value : 0;

    public RareItemSearchEntry(WarriorOutcomeRow hero, LocalizationService loc, IReadOnlyCollection<int> ownedEquipmentItemIds)
    {
        Hero = hero;
        _loc = loc;
        _ownedEquipmentItemIds = ownedEquipmentItemIds;
    }
}
