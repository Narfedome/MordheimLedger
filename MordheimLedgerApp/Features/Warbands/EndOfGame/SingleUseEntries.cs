using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Objet à usage unique porté qui a pu servir pendant la bataille (Herbes curatives, Eau bénite…) : coché = retiré
/// de la fiche à Terminer.</summary>
public partial class UsedInBattleEntry : ObservableObject
{
    public int WarriorEquipmentId { get; }
    public string Title { get; }
    public string Who { get; }

    [ObservableProperty]
    private bool isUsed;

    public UsedInBattleEntry(int warriorEquipmentId, string title, string who)
    {
        WarriorEquipmentId = warriorEquipmentId;
        Title = title;
        Who = who;
    }
}

/// <summary>Jet de la table de conséquences d'un objet pris avant la bataille (Ombre Cramoisie 2D6, Champignons 1D6…) :
/// saisie libre + bouton dé, résultat affiché sous le jet.</summary>
public partial class AfterBattleRollEntry : ObservableObject
{
    private readonly Func<AfterBattleOutcome, string?> _ruleName;

    public Warrior Warrior { get; }
    public EquipmentItem Item { get; }
    public int DiceCount { get; }
    public int DiceSides { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Outcome), nameof(ResultText), nameof(HasResult))]
    private string roll = string.Empty;

    public AfterBattleRollEntry(Warrior warrior, EquipmentItem item, int diceCount, int diceSides, Func<AfterBattleOutcome, string?> ruleName)
    {
        Warrior = warrior;
        Item = item;
        DiceCount = diceCount;
        DiceSides = diceSides;
        _ruleName = ruleName;
        Label = $"{item.Name} · {warrior.Name} : {diceCount}D{diceSides}";
    }

    public int? Total => int.TryParse(Roll, out var value) && value >= DiceCount && value <= DiceCount * DiceSides ? value : null;

    public AfterBattleOutcome? Outcome => Total is { } total ? SingleUseRules.ResolveAfterBattle(Item.SingleUse!, total) : null;

    public bool HasResult => Total is not null;

    public string ResultText
    {
        get
        {
            var loc = LocalizationService.Instance;
            if (Total is null) return string.Empty;
            return Outcome switch
            {
                null => loc["SingleUseOutcomeNone"],
                { Kind: AfterBattleOutcomeKind.Addiction } => loc["SingleUseOutcomeAddiction"],
                { Kind: AfterBattleOutcomeKind.CharacteristicChange, Field: { } field, Delta: { } delta } =>
                    string.Format(loc["SingleUseOutcomeCharacteristic"], loc[$"Stat{field}Abbr"], $"{delta:+0;-0}"),
                { Kind: AfterBattleOutcomeKind.GainSpecialRule } outcome =>
                    string.Format(loc["SingleUseOutcomeRule"], _ruleName(outcome) ?? "?"),
                _ => loc["SingleUseOutcomeNone"],
            };
        }
    }
}

/// <summary>Objet porté qui peut être perdu parce que son porteur a été mis hors de combat (Habits de soie du chef : 1D6,
/// 1-3 détruits) - saisie libre + bouton dé.</summary>
public partial class OutOfActionLossEntry : ObservableObject
{
    public Warrior Warrior { get; }
    public EquipmentItem Item { get; }
    public int WarriorEquipmentId { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLost), nameof(ResultText), nameof(HasResult))]
    private string roll = string.Empty;

    public OutOfActionLossEntry(Warrior warrior, EquipmentItem item, int warriorEquipmentId)
    {
        Warrior = warrior;
        Item = item;
        WarriorEquipmentId = warriorEquipmentId;
        Label = string.Format(LocalizationService.Instance["SingleUseOutOfActionLossFormat"], item.Name, warrior.Name, item.LostAfterOutOfActionOn);
    }

    private int? Value => int.TryParse(Roll, out var value) && value is >= 1 and <= 6 ? value : null;
    public bool HasResult => Value is not null;
    public bool IsLost => Value is { } value && SingleUseRules.IsLostAfterOutOfAction(Item, value);
    public string ResultText => !HasResult ? string.Empty
        : LocalizationService.Instance[IsLost ? "SingleUseOutOfActionLost" : "SingleUseOutOfActionKept"];
}
