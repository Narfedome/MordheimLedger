using System.Collections.ObjectModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Features.Warbands.EndOfGame.Effects;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Effets de compétences et d'objets dans le wizard (canevas « Effets de compétences ») : l'app détecte
/// et propose, le joueur décide. Tout ce qui relève d'une règle vient de Core (RuleEffectResolver pour qui a quel
/// effet et si sa condition est remplie, EffectValues pour lire une valeur ou réduire un prix,
/// OncePerPostBattleLedger pour l'usage unique de Marchandage) ; ce fichier ne fait que construire les lignes
/// affichées à l'entrée de chaque étape et additionner ce que le joueur a laissé coché.
///
/// - Objets rares : bonus au jet (Débrouillard, gemmes...) pré-cochés + modificateur libre, par Héros.
/// - Marchandage : une réduction par Héros qui l'a, à placer sur n'importe quel achat (objet rare trouvé,
///   Achat pour la réserve, équipement d'une recrue) ; la déplacer la retire de l'achat précédent.
/// - Exploration : dés supplémentaires pré-cochés, relance / ajustement d'un dé, rappels.
/// - Blessure grave : relance du jet (Increvable).</summary>
public partial class EndOfGamePageViewModel : IHaggleHost
{
    // --- Sujets : un guerrier tel que Core le voit à cet instant du wizard -------------------------------

    private EffectSubject SubjectFor(WarriorOutcomeRow row) => new(
        row.Warrior,
        row.IsSpellcaster,
        row.IsOutOfAction,
        row.IsDead,
        string.IsNullOrEmpty(_warbandArchetypeOfficialId) ? null : _warbandArchetypeOfficialId,
        _warriorArchetypesByOfficialId.FirstOrDefault(kv => kv.Value.Id == row.Warrior.WarriorArchetypeId).Key);

    /// <summary>Guerriers dont les effets comptent encore : un guerrier tué n'en propose plus aucun (un hors de
    /// combat, si : sa condition dira s'il en profite).</summary>
    private IEnumerable<WarriorOutcomeRow> LivingRows => WarriorRows.Where(r => !r.IsDead && !r.IsFullyDismissed);

    /// <summary>État des guerriers dont dépendent les conditions (hors de combat, tué) : revenir en arrière et le
    /// changer reconstruit les lignes d'une étape déjà vue, sinon elles garderaient l'ancienne condition.</summary>
    private string SubjectStateSignature => string.Join(",", WarriorRows.Select(r => $"{r.Warrior.Id}:{r.IsOutOfAction}:{r.IsDead}:{r.IsFullyDismissed}"));

    private static string ExtraDieText(int count, Services.LocalizationService loc) =>
        count == 1 ? loc["EffectExtraDieOne"] : string.Format(loc["EffectExtraDieMany"], count);

    /// <summary>Appelée à chaque changement d'étape : construit les lignes de l'étape qui s'ouvre, une seule
    /// fois (revenir en arrière garde ce que le joueur a coché ou utilisé).</summary>
    private void PrepareEffectsForCurrentStep()
    {
        switch (Current.Kind)
        {
            case StepKind.Injury when CurrentInjuryWarrior is { } row:
                PrepareInjuryEffects(row);
                break;
            case StepKind.ExplorationRoll:
                PrepareExplorationEffects();
                break;
            case StepKind.RareItems:
                foreach (var entry in RareItemSearchEntries)
                    entry.SetBonusEffects(RuleEffectResolver.For(EffectMoment.RareItemSearch, SubjectFor(entry.Hero))
                        .Where(e => e.Effect.Kind == EffectKind.Modifier));
                break;
            case StepKind.RareItemPurchase or StepKind.EquipmentTrading or StepKind.RecruitHeroDetail or StepKind.RecruitHenchmenEquipment:
                EnsureHaggleUsages();
                break;
            case StepKind.EquipmentReallocation:
                _ = RefreshReallocationAfterListsLoadAsync();
                break;
        }
    }

    // --- Listes d'équipement : qui peut porter quoi (achat pour la Réserve, Réallouer) ----------------------

    private readonly Dictionary<int, IReadOnlySet<int>> _equipmentListItemIds = new();
    private bool _equipmentListsLoaded;

    /// <summary>Charge une fois les listes d'équipement des types de guerriers de la bande (une poignée de
    /// lectures). Tant qu'elles ne sont pas là, rien n'est signalé hors liste.</summary>
    private async Task EnsureEquipmentListsAsync()
    {
        if (_equipmentListsLoaded) return;
        var listIds = _warriorArchetypesByOfficialId.Values.Select(a => a.EquipmentListId)
            .Concat(WarriorRows.Select(r => r.Warrior.EquipmentListId))
            .OfType<int>().Distinct().ToList();
        foreach (var listId in listIds.Where(id => !_equipmentListItemIds.ContainsKey(id)))
            _equipmentListItemIds[listId] = await _libraryService.GetEquipmentListItemIdsAsync(listId);
        _equipmentListsLoaded = true;
    }

    /// <summary>Liste du guerrier, sinon celle de son type (même repli que le recrutement).</summary>
    private IReadOnlySet<int>? EquipmentListItemIdsFor(Warrior warrior)
    {
        var listId = warrior.EquipmentListId
            ?? _warriorArchetypesByOfficialId.Values.FirstOrDefault(a => a.Id == warrior.WarriorArchetypeId)?.EquipmentListId;
        return listId is { } id ? _equipmentListItemIds.GetValueOrDefault(id) : null;
    }

    /// <summary>La bande telle qu'elle est à l'achat : guerriers encore là, avec leurs compétences.</summary>
    private List<EquipmentCarrier> RosterCarriers() =>
        LivingRows.Select(r => new EquipmentCarrier(r.Warrior, EquipmentListItemIdsFor(r.Warrior))).ToList();

    private async Task RefreshReallocationAfterListsLoadAsync()
    {
        await EnsureEquipmentListsAsync();
        OnPropertyChanged(nameof(ReallocationCarriers));
    }

    /// <summary>Message ambre de Réallouer (maquette « Réallouer l'équipement ») : les objets qu'un Héros porte
    /// hors de sa liste d'équipement, compétences comprises (Core.Rules.EquipmentEligibility). Jamais bloquant.</summary>
    private string? OutOfListWarning(ReallocationCarrier carrier)
    {
        if (!_equipmentListsLoaded || carrier.Kind == ReallocationCarrierKind.Reserve) return null;
        var warrior = carrier.ExistingHeroRow?.Warrior ?? new Warrior
        {
            Name = carrier.Name,
            WarriorArchetypeId = carrier.NewRecruitSlot!.Row.Archetype.Id,
            EquipmentListId = carrier.NewRecruitSlot.Row.Archetype.EquipmentListId,
        };
        var listIds = EquipmentListItemIdsFor(warrior);
        var outOfList = carrier.Items
            .Where(i => EquipmentEligibility.AccessFor(i.Item, warrior, listIds).Kind == EquipmentAccessKind.OutOfList)
            .Select(i => i.Name).Distinct().ToList();
        return outOfList.Count == 0 ? null : string.Format(Loc["EquipmentOutOfListWarning"], string.Join(", ", outOfList), carrier.Name);
    }

    // --- Blessure grave : relance (Increvable) ------------------------------------------------------------

    private void PrepareInjuryEffects(WarriorOutcomeRow row)
    {
        if (row.HasPreparedInjuryEffects || !row.Warrior.IsHero) return;
        row.HasPreparedInjuryEffects = true;
        foreach (var effect in RuleEffectResolver.For(EffectMoment.SeriousInjuryRoll, SubjectFor(row)).Where(e => e.Effect.Kind == EffectKind.Reroll))
            row.InjuryEffectActions.Add(new EffectActionRow(effect, Loc["EffectRerollRollDescription"], Loc["EffectRerollAction"],
                Loc["EffectUndoReroll"], Loc, act => RerollInjuryAsync(row, act), undo => UndoInjuryReroll(row, undo)));
        row.NotifyInjuryEffectsChanged();
    }

    private async Task RerollInjuryAsync(WarriorOutcomeRow row, EffectActionRow action)
    {
        if (!int.TryParse(row.ManualRoll, out _))
        {
            await ShowInfoAsync(action.Title, Loc["EffectRollFirst"]);
            return;
        }
        action.Previous = row.ManualRoll;
        var reroll = $"{Random.Shared.Next(1, 7)}{Random.Shared.Next(1, 7)}";
        row.ManualRoll = reroll;
        action.DoneText = string.Format(Loc["EffectRerollDoneFormat"], action.Previous, reroll);
        action.IsDone = true;
    }

    private static void UndoInjuryReroll(WarriorOutcomeRow row, EffectActionRow action)
    {
        if (action.Previous is string previous) row.ManualRoll = previous;
        action.Previous = null;
        action.IsDone = false;
    }

    // --- Exploration : dés supplémentaires, relance / ajustement d'un dé, rappels ------------------------

    public ObservableCollection<EffectToggle> ExplorationDiceToggles { get; } = new();
    public ObservableCollection<EffectActionRow> ExplorationDieActions { get; } = new();
    public ObservableCollection<EffectReminder> ExplorationReminders { get; } = new();

    public bool HasExplorationDiceToggles => ExplorationDiceToggles.Count > 0;
    public bool HasExplorationDieActions => ExplorationDieActions.Count > 0;
    public bool HasExplorationReminders => ExplorationReminders.Count > 0;

    private bool _explorationEffectsPrepared;
    private string? _explorationEffectsSignature;

    /// <summary>Dés en plus cochés par le joueur - avant la première ouverture de l'étape, ce que les objets
    /// portés accordent (même calcul qu'avant les effets, pour que le nombre de dés affiché ailleurs ne bouge pas).</summary>
    private int ExplorationBonusDice => _explorationEffectsPrepared
        ? ExplorationDiceToggles.Where(t => t.IsOn).Sum(t => t.Amount)
        : ExplorationDiceBonus.EffectiveBonusDice(WarriorRows.Where(r => !r.IsOutOfAction).Select(r => r.Warrior));

    private void PrepareExplorationEffects()
    {
        var signature = SubjectStateSignature;
        if (_explorationEffectsPrepared && signature == _explorationEffectsSignature) return;
        _explorationEffectsPrepared = true;
        _explorationEffectsSignature = signature;
        // Reconstruction : un bonus déjà vu garde la case que le joueur lui avait laissée, sauf si sa condition a
        // changé entre-temps (porteur mis hors de combat...) - la case reprend alors l'état par défaut.
        var previousOn = ExplorationDiceToggles.ToDictionary(t => t.Effect.Key, t => (t.IsOn, t.Effect.ConditionMet));
        ExplorationDiceToggles.Clear();
        ExplorationDieActions.Clear();
        ExplorationReminders.Clear();

        var effects = RuleEffectResolver.For(EffectMoment.ExplorationRoll, LivingRows.Select(SubjectFor));
        foreach (var effect in effects.Where(e => e.Effect.Kind == EffectKind.ExtraDie))
        {
            var count = EffectValues.ParseCount(effect.Effect.Value);
            var toggle = new EffectToggle(effect, count, ExtraDieText(count, Loc), Loc);
            if (previousOn.TryGetValue(effect.Key, out var was) && was.ConditionMet == effect.ConditionMet) toggle.IsOn = was.IsOn;
            toggle.Changed += () =>
            {
                OnPropertyChanged(nameof(ExplorationDiceCount));
                SyncExplorationDice();
            };
            ExplorationDiceToggles.Add(toggle);
        }
        foreach (var effect in effects.Where(e => e.Effect.Kind == EffectKind.Reroll))
            ExplorationDieActions.Add(new EffectActionRow(effect, Loc["EffectRerollDieDescription"], Loc["EffectRerollAction"],
                Loc["EffectUndo"], Loc, RerollExplorationDieAsync, UndoExplorationDie));
        foreach (var effect in effects.Where(e => e.Effect.Kind == EffectKind.AdjustDie))
            ExplorationDieActions.Add(new EffectActionRow(effect,
                string.Format(Loc["EffectAdjustDieDescription"], EffectValues.ParseCount(effect.Effect.Value)), Loc["EffectAdjustAction"],
                Loc["EffectUndo"], Loc, AdjustExplorationDieAsync, UndoExplorationDie));
        foreach (var effect in effects.Where(EffectTexts.IsReminder))
            ExplorationReminders.Add(EffectReminder.From(effect, Loc));

        OnPropertyChanged(nameof(HasExplorationDiceToggles));
        OnPropertyChanged(nameof(HasExplorationDieActions));
        OnPropertyChanged(nameof(HasExplorationReminders));
        OnPropertyChanged(nameof(ExplorationDiceCount));
        SyncExplorationDice();
    }

    private sealed record DieChange(ExplorationDieEntry Die, string PreviousRoll);

    private async Task<bool> RequireRolledDiceAsync(EffectActionRow action)
    {
        if (ExplorationDice.Count > 0 && ExplorationDice.All(d => d.Value is not null)) return true;
        await ShowInfoAsync(action.Title, Loc["EffectRollDiceFirst"]);
        return false;
    }

    private async Task RerollExplorationDieAsync(EffectActionRow action)
    {
        if (!await RequireRolledDiceAsync(action)) return;
        var dice = ExplorationDice.ToList();
        var index = await ShowActionSheetIndexAsync(Loc["EffectPickDieTitle"],
            dice.Select(d => string.Format(Loc["EffectDieOption"], d.Index, d.Value)).ToArray());
        if (index < 0 || index >= dice.Count) return;

        var die = dice[index];
        var previous = die.ManualRoll;
        die.ManualRoll = Random.Shared.Next(1, 7).ToString();
        ApplyDieChange(action, die, previous);
    }

    private async Task AdjustExplorationDieAsync(EffectActionRow action)
    {
        if (!await RequireRolledDiceAsync(action)) return;
        var amount = EffectValues.ParseCount(action.Effect.Effect.Value);
        var options = ExplorationDice
            .SelectMany(d => new[] { -amount, amount }.Select(delta => (Die: d, Value: EffectValues.AdjustDie(d.Value!.Value, delta))))
            .Where(o => o.Value != o.Die.Value)
            .ToList();
        var index = await ShowActionSheetIndexAsync(Loc["EffectPickDieTitle"],
            options.Select(o => string.Format(Loc["EffectDieChangeFormat"], o.Die.Index, o.Die.Value, o.Value)).ToArray());
        if (index < 0 || index >= options.Count) return;

        var (die, value) = options[index];
        var previous = die.ManualRoll;
        die.ManualRoll = value.ToString();
        ApplyDieChange(action, die, previous);
    }

    private void ApplyDieChange(EffectActionRow action, ExplorationDieEntry die, string previous)
    {
        action.Previous = new DieChange(die, previous);
        action.DoneText = string.Format(Loc["EffectDieChangeFormat"], die.Index, previous, die.ManualRoll);
        action.IsDone = true;
    }

    private static void UndoExplorationDie(EffectActionRow action)
    {
        if (action.Previous is DieChange change) change.Die.ManualRoll = change.PreviousRoll;
        action.Previous = null;
        action.IsDone = false;
    }

    // --- Marchandage : une réduction par Héros, sur n'importe quel achat de l'après-bataille --------------

    private readonly OncePerPostBattleLedger _effectLedger = new();
    private readonly List<HaggleUsage> _haggleUsages = new();
    private readonly List<HaggleRow> _haggleRows = new();
    private readonly Dictionary<EquipmentPick, int> _spotIdsByPick = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, EquipmentPick> _picksBySpotId = new();
    private string? _haggleSignature;

    public ObservableCollection<HaggleRow> ReserveHaggleRows { get; } = new();
    public ObservableCollection<HaggleRow> RecruitHaggleRows { get; } = new();
    /// <summary>Lignes d'étape affichées seulement quand il y a un achat à réduire : sans achat, elles n'auraient
    /// aucun bouton.</summary>
    public bool HasReserveHaggleRows => ReserveHaggleRows.Count > 0 && SpotsFor(PurchasedReserveItems).Count > 0;
    public bool HasRecruitHaggleRows => RecruitHaggleRows.Count > 0 && SpotsFor(RecruitPicks()).Count > 0;

    private void EnsureHaggleUsages()
    {
        var signature = SubjectStateSignature;
        if (signature == _haggleSignature) return;
        _haggleSignature = signature;
        // Reconstruction (un Héros est mort ou revenu entre-temps) : chaque réduction garde son jet et son achat.
        var previousRolls = _haggleUsages.ToDictionary(u => u.Effect.Key, u => u.Roll);
        _haggleUsages.Clear();
        _haggleRows.Clear();
        ReserveHaggleRows.Clear();
        RecruitHaggleRows.Clear();
        foreach (var entry in RareItemSearchEntries) entry.HaggleRows.Clear();

        var heroes = LivingRows.Where(r => r.Warrior.IsHero).Select(SubjectFor);
        foreach (var effect in RuleEffectResolver.For(EffectMoment.Purchase, heroes).Where(e => e.Effect.Kind == EffectKind.Discount))
        {
            var usage = new HaggleUsage(effect) { Roll = previousRolls.GetValueOrDefault(effect.Key) ?? string.Empty };
            usage.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HaggleUsage.RollValue)) NotifyTreasuryChanged();
            };
            _haggleUsages.Add(usage);

            for (var i = 0; i < RareItemSearchEntries.Count; i++)
            {
                var spotId = RareSpotId(i);
                AddHaggleRow(RareItemSearchEntries[i].HaggleRows, usage,
                    () => ResolvePurchaseSpot(spotId) is { } spot ? new[] { spot } : Array.Empty<PurchaseSpot>());
            }
            AddHaggleRow(ReserveHaggleRows, usage, () => SpotsFor(PurchasedReserveItems));
            AddHaggleRow(RecruitHaggleRows, usage, () => SpotsFor(RecruitPicks()));
        }

        OnPropertyChanged(nameof(HasReserveHaggleRows));
        OnPropertyChanged(nameof(HasRecruitHaggleRows));
        foreach (var entry in RareItemSearchEntries) entry.NotifyHaggleRowsChanged();
        NotifyTreasuryChanged();
    }

    private void AddHaggleRow(ObservableCollection<HaggleRow> into, HaggleUsage usage, Func<IReadOnlyList<PurchaseSpot>> candidates)
    {
        // Un achat qui porte déjà le Marchandage d'un autre Héros n'est pas proposé (Core.Rules.OncePerPostBattleLedger).
        var row = new HaggleRow(usage, this,
            () => candidates().Where(spot => !_effectLedger.IsTakenByOther(spot.Id, usage.Effect.Key)).ToList(), Loc);
        into.Add(row);
        _haggleRows.Add(row);
    }

    private void RefreshHaggleRows()
    {
        if (_haggleRows.Count == 0) return;
        foreach (var row in _haggleRows) row.Refresh();
        OnPropertyChanged(nameof(HasReserveHaggleRows));
        OnPropertyChanged(nameof(HasRecruitHaggleRows));
    }

    private IEnumerable<EquipmentPick> RecruitPicks() =>
        HeroRecruitRows.SelectMany(r => r.NameSlots).SelectMany(s => s.Equipment)
            .Concat(HenchmanRecruitRows.SelectMany(r => r.HenchmanGroupDrafts).SelectMany(g => g.Equipment));

    private IReadOnlyList<PurchaseSpot> SpotsFor(IEnumerable<EquipmentPick> picks) =>
        picks.Select(p => ResolvePurchaseSpot(SpotIdFor(p))).OfType<PurchaseSpot>().ToList();

    private string SpotIdFor(EquipmentPick pick)
    {
        if (!_spotIdsByPick.TryGetValue(pick, out var id))
        {
            id = _spotIdsByPick.Count + 1;
            _spotIdsByPick[pick] = id;
            _picksBySpotId[id] = pick;
        }
        return $"pick:{id}";
    }

    /// <summary>L'achat derrière un identifiant, tel qu'il est maintenant - null s'il n'existe plus (objet rare
    /// décoché, achat retiré, équipement de recrue pris dans la réserve).</summary>
    private PurchaseSpot? ResolvePurchaseSpot(string id)
    {
        if (id.StartsWith("rare:") && int.TryParse(id[5..], out var index) && index < RareItemSearchEntries.Count)
        {
            var entry = RareItemSearchEntries[index];
            return entry.IsPurchased
                ? new PurchaseSpot(id, Loc["HaggleStepRareItems"], entry.SelectedItemDisplayName ?? string.Empty, entry.EffectiveCost!.Value)
                : null;
        }
        if (!id.StartsWith("pick:") || !int.TryParse(id[5..], out var pickId) || !_picksBySpotId.TryGetValue(pickId, out var pick))
            return null;

        if (PurchasedReserveItems.Contains(pick))
            return pick.Cost > 0 ? new PurchaseSpot(id, Loc["HaggleStepTrading"], pick.Name, pick.Cost) : null;
        if (RecruitPicks().Contains(pick) && RecruitPickCost(pick, ServedReservePicks()) is > 0 and var cost)
            return new PurchaseSpot(id, Loc["HaggleStepRecruitment"], pick.Name, cost);
        return null;
    }

    public PurchaseSpot? SpotWhereUsed(HaggleUsage usage) =>
        _effectLedger.UsedAt(usage.Effect.Key) is { } location ? ResolvePurchaseSpot(location) : null;

    public async Task UseAsync(HaggleUsage usage, IReadOnlyList<PurchaseSpot> candidates)
    {
        if (candidates.Count == 0) return;
        var spot = candidates[0];
        if (candidates.Count > 1)
        {
            var index = await ShowActionSheetIndexAsync(Loc["HagglePickPurchaseTitle"],
                candidates.Select(c => string.Format(Loc["HagglePurchaseOption"], c.ItemName, c.Price)).ToArray());
            if (index < 0 || index >= candidates.Count) return;
            spot = candidates[index];
        }
        _effectLedger.Use(usage.Effect.Key, spot.Id);
        NotifyTreasuryChanged();
    }

    public void Release(HaggleUsage usage)
    {
        if (_effectLedger.UsedAt(usage.Effect.Key) is { } location) _effectLedger.Release(usage.Effect.Key, location);
        NotifyTreasuryChanged();
    }

    public void RollDiscount(HaggleUsage usage)
    {
        try
        {
            usage.Roll = DiceFormula.Roll(usage.Formula).ToString();
        }
        catch (FormatException)
        {
            // Formule saisie à la main dans le Codex et illisible : le joueur tape le résultat lui-même.
        }
    }

    /// <summary>Ce que les réductions placées font économiser - ajouté au solde du wizard et à la trésorerie à
    /// Terminer (les étapes d'achat retranchent toujours le plein tarif).</summary>
    private int HaggleSavings => _haggleUsages.Sum(u => SpotWhereUsed(u) is { } spot ? spot.Price - u.DiscountedPrice(spot.Price) : 0);

    /// <summary>Ce que le Marchandage fait économiser sur un achat précis (0 s'il n'y est pas placé).</summary>
    private int HaggleSavingsAt(string spotId) => ResolvePurchaseSpot(spotId) is { } spot
        ? _haggleUsages.Where(u => _effectLedger.UsedAt(u.Effect.Key) == spotId).Sum(u => spot.Price - u.DiscountedPrice(spot.Price))
        : 0;

    /// <summary>Économie du Marchandage sur les achats pour la Réserve - le total de l'étape l'affiche net.</summary>
    private int ReserveHaggleSavings => PurchasedReserveItems.Sum(p => HaggleSavingsAt(SpotIdFor(p)));

    private static string RareSpotId(int index) => $"rare:{index}";

    private void ApplyHaggleSavings(IReadOnlyList<(string Item, int Saved)> savings, List<string> sentences)
    {
        foreach (var (item, saved) in savings)
        {
            Warband!.Treasury += saved;
            sentences.Add(string.Format(Loc["HistoryHaggleSentence"], saved, item));
        }
    }

    private List<(string Item, int Saved)> CurrentHaggleSavings() => _haggleUsages
        .Select(u => (Usage: u, Spot: SpotWhereUsed(u)))
        .Where(x => x.Spot is not null)
        .Select(x => (x.Spot!.ItemName, x.Spot.Price - x.Usage.DiscountedPrice(x.Spot.Price)))
        .Where(x => x.Item2 > 0)
        .ToList();
}
