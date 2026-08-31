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
    private DramatisPersona? selectedCharacter;

    partial void OnSelectedCharacterChanged(DramatisPersona? value)
    {
        CharacterRoll = string.Empty;
        CharacterRollError = null;
    }

    public bool HasSelectedCharacter => SelectedCharacter is not null;

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

    public RareItemSearchEntry(WarriorOutcomeRow hero, LocalizationService loc)
    {
        Hero = hero;
        _loc = loc;
    }
}
