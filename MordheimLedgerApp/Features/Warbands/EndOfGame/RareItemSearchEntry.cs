using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One Hero's rare item search attempt (post-battle sequence step 6, "Rare items" - p.145):
/// "Whenever a Hero wants to buy a rare item, roll 2D6 and compare the result to the [Rarity] number
/// stated... You may also only make one roll for each Hero looking for rare items." Nominating an item
/// is optional (a Hero may simply not search) - see EndOfGameDialogViewModel.RareItems.cs for the step
/// this backs, one entry per living Hero not taken Out of Action this battle.</summary>
public partial class RareItemSearchEntry : ObservableObject
{
    private readonly LocalizationService _loc;

    public WarriorOutcomeRow Hero { get; }
    public string HeroName => Hero.Name;

    /// <summary>Null = not currently searching for anything - a Hero isn't required to make an attempt.
    /// Picking a new item clears any previously chosen SelectedMaterial (see OnSelectedItemChanged) - a
    /// material choice made for the last item never carries over to a different one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    [NotifyPropertyChangedFor(nameof(IsMaterialEligible))]
    [NotifyPropertyChangedFor(nameof(EffectiveRarity))]
    [NotifyPropertyChangedFor(nameof(RarityDisplay))]
    [NotifyPropertyChangedFor(nameof(HasRarity))]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(ResultDisplay))]
    private EquipmentItem? selectedItem;

    partial void OnSelectedItemChanged(EquipmentItem? value)
    {
        SelectedMaterial = null;
        Roll = string.Empty;
        RollError = null;
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

    /// <summary>"If the roll is equal or greater, the item is available." False (not null) once HasResult
    /// is true and the roll falls short - IsVisible bindings in XAML key off HasResult, not this, to tell
    /// "no result yet" apart from "found nothing".</summary>
    public bool IsSuccess => HasResult && TotalRoll >= EffectiveRarity;

    public string ResultDisplay => !HasResult
        ? string.Empty
        : string.Format(_loc[IsSuccess ? "EndOfGameRareItemSuccessFormat" : "EndOfGameRareItemFailureFormat"], TotalRoll);

    public RareItemSearchEntry(WarriorOutcomeRow hero, LocalizationService loc)
    {
        Hero = hero;
        _loc = loc;
    }
}
