using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>Bonus lancé au début de partie (Ombre Cramoisie : Initiative +1D3) - saisie libre + bouton dé.</summary>
public partial class DiceModifierEntry : ObservableObject
{
    private readonly Action _changed;

    public GameStatModifier Modifier { get; }
    public string Label { get; }

    [ObservableProperty]
    private string roll = string.Empty;

    public DiceModifierEntry(GameStatModifier modifier, Action changed)
    {
        Modifier = modifier;
        _changed = changed;
        var loc = LocalizationService.Instance;
        Label = $"{loc[$"Stat{modifier.Field}Abbr"]} {(modifier.Value != 0 ? $"{modifier.Value:+0;-0} " : "+")}1D{modifier.DiceSides}";
    }

    /// <summary>Jet saisi, borné aux faces du dé ; vide = pas encore lancé (compte 0).</summary>
    public int Rolled => int.TryParse(Roll, out var value) ? Math.Clamp(value, 1, Modifier.DiceSides ?? 1) : 0;

    partial void OnRollChanged(string value) => _changed();
}

public enum BeforeBattleEntryKind
{
    Item,
    /// <summary>Un dépendant sans dose : coché = il quitte la bande.</summary>
    AddictionLeave,
}

/// <summary>Ligne de la section « Avant la bataille » du début de partie : un objet à usage unique proposé à un guerrier
/// (ou à toute la bande), ou un dépendant sans dose. L'appli propose, le joueur décide : rien n'est bloquant, une case
/// contraire à la règle affiche seulement un message ambre.</summary>
public partial class BeforeBattleItemEntry : ObservableObject
{
    public BeforeBattleEntryKind Kind { get; }
    public Warrior? Warrior { get; }
    public EquipmentItem? Item { get; }
    public int? WarriorEquipmentId { get; init; }
    public int? WarbandEquipmentId { get; init; }

    public string Title { get; }
    public string Subtitle { get; }

    /// <summary>Dure une partie (Ail, Torche) : coché d'office, non décochable - comme sur la maquette.</summary>
    public bool IsLocked { get; }
    public bool IsUnlocked => !IsLocked;

    /// <summary>Race du guerrier (ou de la bande) dans « Sans effet sur » : utilisé pour rien.</summary>
    public bool WithoutEffect { get; }

    public List<DiceModifierEntry> DiceModifiers { get; } = new();
    public List<string> WeaponOptions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Warning), nameof(HasWarning), nameof(ShowDice), nameof(ShowWeapons), nameof(ProfileText), nameof(HasProfile))]
    private bool isChecked;

    [ObservableProperty]
    private string? selectedWeapon;

    public BeforeBattleItemEntry(BeforeBattleEntryKind kind, Warrior? warrior, EquipmentItem? item, string title, string subtitle,
        bool isChecked, bool isLocked = false, bool withoutEffect = false, IEnumerable<string>? weapons = null)
    {
        Kind = kind;
        Warrior = warrior;
        Item = item;
        Title = title;
        Subtitle = subtitle;
        IsLocked = isLocked;
        WithoutEffect = withoutEffect;
        this.isChecked = isChecked || isLocked;
        if (item?.SingleUse is { } profile)
            foreach (var modifier in profile.StatModifiers.Where(m => m.DiceSides is > 0))
                DiceModifiers.Add(new DiceModifierEntry(modifier, () => { OnPropertyChanged(nameof(ProfileText)); OnPropertyChanged(nameof(HasProfile)); }));
        if (weapons is not null) WeaponOptions.AddRange(weapons);
        selectedWeapon = WeaponOptions.FirstOrDefault();
    }

    public bool ShowDice => IsChecked && DiceModifiers.Count > 0;
    public bool ShowWeapons => IsChecked && Item?.SingleUse?.Target == SingleUseTarget.Weapon;
    public bool HasNoWeapon => WeaponOptions.Count == 0;

    public string? Warning
    {
        get
        {
            var loc = LocalizationService.Instance;
            return Kind switch
            {
                BeforeBattleEntryKind.AddictionLeave when !IsChecked => loc["SingleUseAddictionKeptWarning"],
                BeforeBattleEntryKind.Item when IsChecked && WithoutEffect => loc["SingleUseWithoutEffectWarning"],
                BeforeBattleEntryKind.Item when ShowWeapons && HasNoWeapon => loc["SingleUseNoPoisonableWeaponWarning"],
                _ => null,
            };
        }
    }

    public bool HasWarning => Warning is not null;

    /// <summary>Bonus de la partie : fixes + dés lancés (0 tant que le dé n'est pas saisi). Rien si sans effet.</summary>
    public List<RolledStatModifier> RolledModifiers()
    {
        if (WithoutEffect || Item?.SingleUse is not { } profile) return new List<RolledStatModifier>();
        var rolls = new Queue<int>(DiceModifiers.Select(d => d.Rolled));
        return SingleUseRules.RollStatModifiers(profile, _ => rolls.Count > 0 ? rolls.Dequeue() : 0).ToList();
    }

    /// <summary>« Klaus pour cette partie : M 4 → 5, F 3 → 4, I 3 → 5 ».</summary>
    public string? ProfileText
    {
        get
        {
            if (!IsChecked || Warrior is null) return null;
            var modifiers = RolledModifiers();
            var warrior = Warrior;
            var parts = modifiers.Select(m => m.Field).Distinct()
                .Select(stat => (Stat: stat, Value: CharacteristicModifier.Read(warrior, stat)))
                .Where(x => x.Value is not null)
                .Select(x => $"{LocalizationService.Instance[$"Stat{x.Stat}Abbr"]} {x.Value} → {SingleUseRules.GameValue(x.Value!.Value, x.Stat, modifiers)}")
                .ToList();
            return parts.Count == 0 ? null : string.Format(LocalizationService.Instance["SingleUseEntryProfileFormat"], Warrior.Name, string.Join(", ", parts));
        }
    }

    public bool HasProfile => ProfileText is not null;

    public GameItemUse ToUse() => new()
    {
        WarriorId = Warrior?.Id,
        EquipmentItemId = Item!.Id,
        WarriorEquipmentId = WarriorEquipmentId,
        WarbandEquipmentId = WarbandEquipmentId,
        WeaponName = ShowWeapons ? SelectedWeapon : null,
        StatModifiers = RolledModifiers(),
        WithoutEffect = WithoutEffect,
    };
}
