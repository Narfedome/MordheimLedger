using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Warbands.EndOfGame;
using MordheimLedgerApp.Features.Warbands.StartGame;
using MordheimLedgerApp.Services;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Commande "Lancer la partie" - remplace "Fin de partie" sur WarbandDetailPage tant qu'aucune
/// partie n'est en cours (voir Warband.GameInProgress/HasGameInProgress), demande explicite de
/// l'utilisateur (2026-08-26) : "Start a game" -> "End of game", un bouton à la fois. Wizard informatif
/// à un seul écran (StartGameDialog) - PAS de verrouillage roster/inventaire (décision explicite, rien
/// n'est bloqué pendant qu'une partie est "en cours"), seulement des rappels + le jet répétitif de
/// Vieille blessure (32, le seul résultat de la table des Blessures Graves qui se rejoue à chaque
/// partie plutôt qu'une fois à la Fin de Partie précédente).</summary>
public partial class WarbandDetailViewModel
{
    [RelayCommand]
    private async Task StartGame()
    {
        if (Warband is null) return;

        var unavailableWarriors = new List<UnavailableWarriorRow>();
        foreach (var row in AllActiveWarriorRows.Where(r => r.Warrior.Status == WarriorStatus.Sick))
            unavailableWarriors.Add(new UnavailableWarriorRow(row, row.SickChipText));
        foreach (var row in RetiredWarriors)
            unavailableWarriors.Add(new UnavailableWarriorRow(row, Loc["WarriorStatusRetired"]));
        foreach (var row in DeadWarriors)
            unavailableWarriors.Add(new UnavailableWarriorRow(row, Loc["WarriorStatusDead"]));

        // Vieille blessure (32) : seul résultat mécanisé qui exige un jet avant CHAQUE bataille plutôt
        // qu'une fois à la Fin de Partie où il a été obtenu - voir SERIOUS_INJURIES_STATUS.md. Restreint
        // aux guerriers Actifs (un guerrier Malade/Mort/Retraité ne combat de toute façon pas cette
        // partie, inutile de lui faire passer ce test). UNE carte PAR guerrier, avec UN jet PAR instance
        // de l'Injury qu'il porte à l'intérieur - retour utilisateur (2026-08-26) : "si on a plusieurs
        // oldwound, on doit tirer le nombre de old wound... 2 old wound = 2 jets" pour la mécanique, "au
        // lieu d'avoir 2 card, c'est d'avoir une card avec 2 roll à l'intérieur" pour l'affichage - un
        // guerrier qui a accumulé 2 Vieilles blessures distinctes (2 résultats Serious Injury différents
        // tombés sur 32 au fil des parties) teste chacune indépendamment, un seul échec parmi ses jets
        // suffit à le sortir de la partie (OldWoundWarriorEntry.HasFailure).
        var oldWoundEntries = AllActiveWarriorRows
            .Where(r => r.Warrior.Status == WarriorStatus.Active)
            .Select(r => (r.Warrior, Count: r.Warrior.Injuries.Count(i => InjuryCatalogLookup.RollRangeMatches(i.Item.RollRange, 32))))
            .Where(x => x.Count > 0)
            .Select(x => new OldWoundWarriorEntry(x.Warrior,
                Enumerable.Range(1, x.Count).Select(n => new OldWoundRollEntry(x.Count > 1 ? $"{Loc["StartGameOldWoundRollLabel"]} {n}" : string.Empty)).ToList()))
            .ToList();

        // Aide conditionnelle (ex. Bertha) : voir DramatisPersonaAidEntry - vérifié ICI (pas à la Fin de
        // Partie) car c'est le seul moment où le prochain adversaire est sur le point d'être connu. Un
        // guerrier Actif recruté depuis le catalogue Dramatis Personae (Warrior.DramatisPersonaId) dont
        // l'entrée exige RequiresRatingDisadvantage obtient sa carte - même filtre Status == Active que
        // OldWoundEntries ci-dessus (un guerrier Malade/Mort/Retraité ne combat de toute façon pas cette
        // partie, inutile de lui faire passer ce test).
        var dramatisPersonaeById = (await _libraryService.GetDramatisPersonaeAsync(LocalizationService.Instance.Language)).ToDictionary(p => p.Id);
        var aidEntries = AllActiveWarriorRows
            .Where(r => r.Warrior.Status == WarriorStatus.Active)
            .Where(r => r.Warrior.DramatisPersonaId is { } id && dramatisPersonaeById.TryGetValue(id, out var persona) && persona.RequiresRatingDisadvantage)
            .Select(r => new DramatisPersonaAidEntry(r.Warrior, dramatisPersonaeById[r.Warrior.DramatisPersonaId!.Value].Name, Rating))
            .ToList();

        var beforeBattleItems = await BuildBeforeBattleItemsAsync(Warband);
        var dialogViewModel = new StartGameDialogViewModel(unavailableWarriors, oldWoundEntries, aidEntries, Warband.NextGameNote, Rating, beforeBattleItems);
        if (await ShowDialogAsync(new StartGameDialog(dialogViewModel)) != true) return;

        await Loading.RunAsync(async () =>
        {
            // Un guerrier qui rate un de ses jets (une seule Vieille blessure suffit) ne joue pas cette
            // partie - marqué Malade avec 1 partie restante (même mécanisme que le Puits de
            // l'Exploration) : ça l'exclut correctement d'`activeWarriorRows` si "Fin de partie" est
            // ouvert avant qu'il ait rejoué, contrairement à avant ce correctif où l'échec n'était que
            // journalisé, sans conséquence sur son statut - le guerrier restait Actif et se retrouvait
            // à tort dans le wizard Fin de Partie alors qu'il n'avait pas participé (retour utilisateur
            // 2026-08-26). Seuls les échecs sont journalisés dans l'Historique - un guerrier qui passe
            // le test n'a rien de notable à consigner.
            var sentences = new List<string>();
            foreach (var entry in dialogViewModel.OldWoundEntries.Where(e => e.HasFailure))
            {
                entry.Warrior.Status = WarriorStatus.Sick;
                entry.Warrior.SickGamesRemaining += 1;
                await _warbandService.SaveWarriorAsync(entry.Warrior);
                sentences.Add(string.Format(Loc["HistoryOldWoundFailSentence"], entry.Warrior.Name));
            }
            // Aide conditionnelle ratée (ex. Bertha : écart trop faible ou jet manqué) - voir
            // DramatisPersonaAidEntry.WontFight. Retrait immédiat plutôt que d'attendre la Fin de Partie
            // (ApplyWandererDeparturesAsync la retirerait de toute façon puisqu'elle est Vagabonde, mais
            // seulement une fois cette bataille "traitée" - la retirer ICI reflète honnêtement qu'elle
            // n'a jamais rejoint la bande pour cette partie, retour utilisateur 2026-09-01).
            foreach (var entry in dialogViewModel.AidEntries.Where(e => e.WontFight))
            {
                await _warbandService.DeleteWarriorAsync(entry.Warrior.Id);
                sentences.Add(string.Format(Loc["HistoryDramatisPersonaDepartedSentence"], entry.Warrior.Name));
            }

            // Objets à usage unique (« Avant la bataille ») : dépendants sans dose qui quittent la bande, objets pris.
            var singleUse = new SingleUseService(_warbandService, _libraryService);
            foreach (var entry in dialogViewModel.BeforeBattleItems.Where(e => e.Kind == BeforeBattleEntryKind.AddictionLeave && e.IsChecked))
            {
                await singleUse.LeaveForAddictionAsync(entry.Warrior!);
                sentences.Add(string.Format(Loc["HistoryAddictionLeftSentence"], entry.Warrior!.Name, entry.Title));
            }
            await singleUse.StartGameAsync(Warband.Id, dialogViewModel.BeforeBattleItems
                .Where(e => e.Kind == BeforeBattleEntryKind.Item && e.IsChecked)
                .Select(e => e.ToUse()).ToList());

            if (sentences.Count > 0)
                await _warbandService.AddHistoryEntryAsync(Warband.Id, string.Join(" ", sentences));

            // Valeurs adverses (1.6) : remplacées à chaque partie, lues par la Fin de partie pour l'outsider.
            Warband.OpponentRatings = dialogViewModel.OpponentRatings;
            Warband.GameInProgress = true;
            await _warbandService.SaveWarbandAsync(Warband);
            await LoadAsync(Warband.Id);
        });
    }

    /// <summary>Section « Avant la bataille » : chaque objet à usage unique proposé avant la bataille (pris ou valable une
    /// partie) porté par un guerrier actif, les objets pour toute la bande (porté ou en réserve), et chaque dépendant -
    /// sa dose pré-cochée s'il en a une (sur lui ou en réserve), sinon la ligne « quitte la bande » cochée.</summary>
    private async Task<List<BeforeBattleItemEntry>> BuildBeforeBattleItemsAsync(Warband warband)
    {
        var language = LocalizationService.Instance.Language;
        var items = (await _libraryService.GetEquipmentItemsAsync(language)).ToDictionary(i => i.Id);
        var warbandRaceId = (await _libraryService.GetWarbandArchetypeAsync(warband.WarbandArchetypeId, language))?.RaceId;
        var typeRaces = _recruitableArchetypes.ToDictionary(a => a.Id, a => a.RaceId);
        var hiredSwordRaces = _recruitableHiredSwords.ToDictionary(h => h.Id, h => h.RaceId);
        var personaRaces = _recruitableDramatisPersonae.ToDictionary(p => p.Id, p => p.RaceId);
        int? RaceOf(Warrior w) => RaceRules.EffectiveRaceId(
            w.WarriorArchetypeId is { } t && typeRaces.TryGetValue(t, out var tr) ? tr
            : w.HiredSwordId is { } h && hiredSwordRaces.TryGetValue(h, out var hr) ? hr
            : w.DramatisPersonaId is { } p && personaRaces.TryGetValue(p, out var pr) ? pr
            : null, warbandRaceId);

        EquipmentItem? Resolve(EquipmentItem item) => items.TryGetValue(item.Id, out var full) ? full : item;
        var reserve = Inventory.Select(e => (Row: e, Item: Resolve(e.Item))).Where(x => x.Item is not null).ToList();
        var entries = new List<BeforeBattleItemEntry>();
        var active = AllActiveWarriorRows.Where(r => r.Warrior.Status == WarriorStatus.Active).Select(r => r.Warrior).ToList();

        foreach (var warrior in active)
        {
            var weapons = warrior.Equipment.Where(e => SingleUseRules.IsPoisonable(e.Item)).Select(e => e.NameDisplay).Distinct().ToList();
            foreach (var carried in warrior.Equipment)
            {
                if (Resolve(carried.Item) is not { } item || !SingleUseRules.IsOfferedBeforeBattle(item)) continue;
                var forBand = item.SingleUse!.Target == SingleUseTarget.Warband;
                var withoutEffect = RaceRules.IsWithoutEffect(forBand ? warbandRaceId : RaceOf(warrior), item.NoEffectOnRaceIds);
                var isDose = warrior.AddictedToEquipmentItemId == item.Id;
                entries.Add(new BeforeBattleItemEntry(BeforeBattleEntryKind.Item, forBand ? null : warrior, item, item.Name,
                    Subtitle(forBand ? Loc["SingleUseWholeWarband"] : warrior.Name, item),
                    isChecked: isDose, isLocked: SingleUseRules.IsPreChecked(item), withoutEffect: withoutEffect, weapons: weapons)
                    { WarriorEquipmentId = carried.Id });
            }
        }

        foreach (var (row, item) in reserve.Where(x => SingleUseRules.IsOfferedBeforeBattle(x.Item!) && x.Item!.SingleUse!.Target == SingleUseTarget.Warband))
            entries.Add(new BeforeBattleItemEntry(BeforeBattleEntryKind.Item, null, item, item!.Name, Subtitle(Loc["SingleUseWholeWarband"], item),
                isChecked: false, isLocked: SingleUseRules.IsPreChecked(item), withoutEffect: RaceRules.IsWithoutEffect(warbandRaceId, item.NoEffectOnRaceIds))
                { WarbandEquipmentId = row.Id });

        foreach (var addict in active.Where(w => w.AddictedToEquipmentItemId is not null))
        {
            var drugId = addict.AddictedToEquipmentItemId!.Value;
            if (entries.Any(e => e.Warrior?.Id == addict.Id && e.Item?.Id == drugId)) continue;
            var drugName = items.TryGetValue(drugId, out var drug) ? drug.Name : "?";
            if (reserve.FirstOrDefault(x => x.Item!.Id == drugId) is { Row: not null } stored)
            {
                // Dose prise dans la réserve pour lui.
                var weapons = addict.Equipment.Where(e => SingleUseRules.IsPoisonable(e.Item)).Select(e => e.NameDisplay).Distinct().ToList();
                entries.Add(new BeforeBattleItemEntry(BeforeBattleEntryKind.Item, addict, stored.Item, stored.Item!.Name,
                    Subtitle(addict.Name, stored.Item), isChecked: true,
                    withoutEffect: RaceRules.IsWithoutEffect(RaceOf(addict), stored.Item.NoEffectOnRaceIds), weapons: weapons)
                    { WarbandEquipmentId = stored.Row.Id });
                continue;
            }
            entries.Add(new BeforeBattleItemEntry(BeforeBattleEntryKind.AddictionLeave, addict, null,
                string.Format(Loc["SingleUseAddictLeavesTitle"], addict.Name),
                string.Format(Loc["SingleUseAddictLeavesSubtitle"], drugName), isChecked: true));
        }

        return entries;
    }

    private string Subtitle(string who, EquipmentItem item)
    {
        var effect = item.SingleUse!.StatModifiers.Count > 0
            ? string.Join(", ", item.SingleUse.StatModifiers.Select(m =>
                $"{Loc[$"Stat{m.Field}Abbr"]} {(m.DiceSides is > 0 ? (m.Value != 0 ? $"{m.Value:+0;-0} +1D{m.DiceSides}" : $"+1D{m.DiceSides}") : $"{m.Value:+0;-0}")}"))
            : Loc[$"SingleUse{item.SingleUse.Timing}"];
        return $"{who} · {effect}";
    }
}
