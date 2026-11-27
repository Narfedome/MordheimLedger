using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Un objet utilisé pendant la bataille (Herbes curatives, Eau bénite…), coché en Fin de partie : l'exemplaire
/// à retirer, porté par un guerrier ou pris dans la réserve.</summary>
public sealed record ConsumedItem(int? WarriorEquipmentId, int? WarbandEquipmentId);

/// <summary>Résultat de la table de conséquences d'un objet pris avant la bataille, pour un guerrier (outcome null = sans
/// effet : rien à écrire).</summary>
public sealed record AfterBattleResult(int WarriorId, int EquipmentItemId, AfterBattleOutcome Outcome);

/// <summary>Écritures des objets à usage unique. Comme les services de la Fin de partie, il applique des décisions déjà
/// prises dans l'appli (cases, jets) et n'écrit aucune phrase d'Historique.</summary>
public sealed class SingleUseService
{
    private readonly IWarbandService _warbands;
    private readonly ILibraryService _library;

    public SingleUseService(IWarbandService warbands, ILibraryService library)
    {
        _warbands = warbands;
        _library = library;
    }

    /// <summary>Début de partie : les objets cochés sont pris pour la partie (affichés sur la fiche jusqu'à la Fin de
    /// partie, qui les retire de l'équipement). Remplace une éventuelle sélection précédente.</summary>
    public Task StartGameAsync(int warbandId, IReadOnlyList<GameItemUse> uses) => _warbands.ReplaceGameItemUsesAsync(warbandId, uses);

    /// <summary>Un dépendant sans dose quitte la bande (coché au début de partie) : il reste sur la fiche, Retiré.</summary>
    public async Task LeaveForAddictionAsync(Warrior warrior)
    {
        warrior.Status = WarriorStatus.Retired;
        await _warbands.SaveWarriorAsync(warrior);
    }

    /// <summary>Fin de partie : retire de l'équipement les objets pris avant la bataille et ceux cochés comme utilisés
    /// pendant, applique les conséquences tirées, puis oublie la partie.</summary>
    public async Task FinishGameAsync(int warbandId, IReadOnlyList<ConsumedItem> usedDuringBattle, IReadOnlyList<AfterBattleResult> results,
        string languageCode)
    {
        foreach (var use in await _warbands.GetGameItemUsesAsync(warbandId))
            await ConsumeAsync(use.WarriorEquipmentId, use.WarbandEquipmentId);
        foreach (var item in usedDuringBattle)
            await ConsumeAsync(item.WarriorEquipmentId, item.WarbandEquipmentId);

        if (results.Count > 0)
        {
            var warriors = (await _warbands.GetWarriorsAsync(warbandId, languageCode)).ToDictionary(w => w.Id);
            var rulesByOfficialId = (await _library.GetSpecialRulesAsync(languageCode))
                .Where(r => r.OfficialId is not null).ToDictionary(r => r.OfficialId!, r => r.Id);
            foreach (var result in results)
            {
                if (!warriors.TryGetValue(result.WarriorId, out var warrior)) continue;
                if (Apply(warrior, result, rulesByOfficialId)) await _warbands.SaveWarriorAsync(warrior);
            }
        }

        await _warbands.ReplaceGameItemUsesAsync(warbandId, Array.Empty<GameItemUse>());
    }

    private async Task ConsumeAsync(int? warriorEquipmentId, int? warbandEquipmentId)
    {
        if (warriorEquipmentId is { } carried) await _warbands.ConsumeOneWarriorEquipmentAsync(carried);
        else if (warbandEquipmentId is { } stored) await _warbands.ConsumeOneWarbandEquipmentAsync(stored);
    }

    private static bool Apply(Warrior warrior, AfterBattleResult result, IReadOnlyDictionary<string, int> rulesByOfficialId)
    {
        var outcome = result.Outcome;
        switch (outcome.Kind)
        {
            case AfterBattleOutcomeKind.Addiction:
                warrior.AddictedToEquipmentItemId = result.EquipmentItemId;
                return true;
            case AfterBattleOutcomeKind.CharacteristicChange when outcome.Field is { } field && outcome.Delta is { } delta:
                CharacteristicModifier.Apply(warrior, field, delta);
                return true;
            case AfterBattleOutcomeKind.GainSpecialRule when outcome.SpecialRuleId is { } officialId
                && rulesByOfficialId.TryGetValue(officialId, out var ruleId) && !warrior.GainedSpecialRuleIds.Contains(ruleId):
                warrior.GainedSpecialRuleIds.Add(ruleId);
                return true;
            default:
                return false;
        }
    }
}
