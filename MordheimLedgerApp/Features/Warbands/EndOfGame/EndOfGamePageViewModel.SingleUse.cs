using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape « Objets à usage unique » : rappelle ce qui a été pris avant la bataille (retiré de la fiche à Terminer),
/// fait cocher les objets utilisés pendant (Herbes curatives, Eau bénite…) et fait lancer les tables de conséquences
/// (Ombre Cramoisie, Racine de Mandragore, Champignons). Règles : Core.Rules.SingleUseRules ; écritures :
/// Core.Services.SingleUseService, en dernier à Terminer.</summary>
public partial class EndOfGamePageViewModel
{
    private List<GameItemUse> _gameItemUses = new();

    /// <summary>« Ombre Cramoisie · Klaus », « Bière de Bugman · la bande ».</summary>
    public List<string> TakenItemLines { get; private set; } = new();
    public bool HasTakenItems => TakenItemLines.Count > 0;

    public List<UsedInBattleEntry> UsedInBattleEntries { get; private set; } = new();
    public bool HasUsedInBattleEntries => UsedInBattleEntries.Count > 0;

    public ObservableCollection<AfterBattleRollEntry> AfterBattleRolls { get; } = new();
    public bool HasAfterBattleRolls => AfterBattleRolls.Count > 0;

    private bool HasSingleUseStep => _gameItemUses.Count > 0 || UsedInBattleEntries.Count > 0 || OutOfActionLossCandidates().Any();

    /// <summary>Objets perdus si leur porteur a été mis hors de combat (Habits de soie du chef) : l'étape s'ajoute dès qu'un
    /// tel porteur est coché hors de combat à l'étape précédente.</summary>
    public ObservableCollection<OutOfActionLossEntry> OutOfActionLosses { get; } = new();
    public bool HasOutOfActionLosses => OutOfActionLosses.Count > 0;

    private IEnumerable<(Warrior Warrior, WarriorEquipment Carried, EquipmentItem Item)> OutOfActionLossCandidates()
    {
        var catalog = _localizedEquipmentCatalog.ToDictionary(i => i.Id);
        return WarriorRows
            .Where(r => r.IsOutOfAction && !IsDeadThisBattle(r.Warrior))
            .SelectMany(r => r.Warrior.Equipment.Select(e => (r.Warrior, Carried: e)))
            .Where(x => catalog.TryGetValue(x.Carried.Item.Id, out var item) && SingleUseRules.IsAtRiskAfterOutOfAction(item, x.Warrior.IsLeader))
            .Select(x => (x.Warrior, x.Carried, catalog[x.Carried.Item.Id]));
    }

    private string ItemName(int itemId) => _localizedEquipmentCatalog.FirstOrDefault(i => i.Id == itemId)?.Name ?? "?";

    private void LoadSingleUses(List<GameItemUse> uses)
    {
        _gameItemUses = uses;
        var names = _allActiveWarriorRows.ToDictionary(r => r.Warrior.Id, r => r.Warrior.Name);
        TakenItemLines = uses.Select(u => $"{ItemName(u.EquipmentItemId)} · {(u.WarriorId is { } id && names.TryGetValue(id, out var name) ? name : Loc["SingleUseTheWarband"])}").ToList();

        var catalog = _localizedEquipmentCatalog.ToDictionary(i => i.Id);
        UsedInBattleEntries = _allActiveWarriorRows
            .Where(r => r.Warrior.Status == WarriorStatus.Active)
            .SelectMany(r => r.Warrior.Equipment.Select(e => (r.Warrior, Carried: e)))
            .Where(x => catalog.TryGetValue(x.Carried.Item.Id, out var item) && SingleUseRules.IsAskedAfterBattle(item))
            .Select(x => new UsedInBattleEntry(x.Carried.Id, x.Carried.NameDisplay, x.Warrior.Name))
            .ToList();
    }

    /// <summary>Un jet par objet pris par un guerrier encore vivant, avec une table de conséquences et pas utilisé pour rien.
    /// Reconstruit à chaque entrée dans l'étape (un guerrier tué depuis disparaît) ; les jets déjà saisis sont gardés.</summary>
    private void BuildAfterBattleRolls()
    {
        var catalog = _localizedEquipmentCatalog.ToDictionary(i => i.Id);
        var warriors = _allActiveWarriorRows.Select(r => r.Warrior).Where(w => !IsDeadThisBattle(w)).ToDictionary(w => w.Id);
        var previous = AfterBattleRolls.ToDictionary(e => (e.Warrior.Id, e.Item.Id), e => e.Roll);
        AfterBattleRolls.Clear();
        foreach (var use in _gameItemUses.Where(u => !u.WithoutEffect))
        {
            if (use.WarriorId is not { } warriorId || !warriors.TryGetValue(warriorId, out var warrior)) continue;
            if (!catalog.TryGetValue(use.EquipmentItemId, out var item) || item.SingleUse is null) continue;
            if (SingleUseRules.AfterBattleDice(item.SingleUse) is not { } dice) continue;
            var entry = new AfterBattleRollEntry(warrior, item, dice.Count, dice.Sides,
                id => _specialRulesByOfficialId.TryGetValue(id, out var rule) ? rule.Name : null);
            if (previous.TryGetValue((warrior.Id, item.Id), out var roll)) entry.Roll = roll;
            AfterBattleRolls.Add(entry);
        }
        OnPropertyChanged(nameof(HasAfterBattleRolls));

        var previousLosses = OutOfActionLosses.ToDictionary(e => e.WarriorEquipmentId, e => e.Roll);
        OutOfActionLosses.Clear();
        foreach (var (warrior, carried, item) in OutOfActionLossCandidates())
        {
            var entry = new OutOfActionLossEntry(warrior, item, carried.Id);
            if (previousLosses.TryGetValue(carried.Id, out var roll)) entry.Roll = roll;
            OutOfActionLosses.Add(entry);
        }
        OnPropertyChanged(nameof(HasOutOfActionLosses));
    }

    [RelayCommand]
    private void AutoRollOutOfActionLoss(OutOfActionLossEntry entry) => entry.Roll = Random.Shared.Next(1, 7).ToString();

    [RelayCommand]
    private void AutoRollAfterBattle(AfterBattleRollEntry entry) =>
        entry.Roll = Enumerable.Range(0, entry.DiceCount).Sum(_ => Random.Shared.Next(1, entry.DiceSides + 1)).ToString();

    private async Task ApplySingleUseAsync(string language, List<string> sentences)
    {
        if (!HasSingleUseStep) return;
        var results = AfterBattleRolls.Where(e => e.Outcome is not null)
            .Select(e => new AfterBattleResult(e.Warrior.Id, e.Item.Id, e.Outcome!)).ToList();
        foreach (var entry in AfterBattleRolls.Where(e => e.Outcome is not null))
            sentences.Add($"{entry.Warrior.Name} ({entry.Item.Name}) : {entry.ResultText}");

        foreach (var loss in OutOfActionLosses.Where(e => e.IsLost))
            sentences.Add(string.Format(Loc["HistoryOutOfActionLossSentence"], loss.Item.Name, loss.Warrior.Name));

        await new SingleUseService(_warbandService, _libraryService).FinishGameAsync(Warband!.Id,
            _allActiveWarriorRows.Select(r => r.Warrior).ToList(),
            UsedInBattleEntries.Where(e => e.IsUsed).Select(e => new ConsumedItem(e.WarriorEquipmentId, null))
                .Concat(OutOfActionLosses.Where(e => e.IsLost).Select(e => new ConsumedItem(e.WarriorEquipmentId, null))).ToList(),
            results, language);
    }
}
