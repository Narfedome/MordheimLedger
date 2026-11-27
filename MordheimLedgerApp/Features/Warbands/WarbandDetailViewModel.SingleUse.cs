using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Objets à usage unique sur la fiche : ce qui a été pris au début de la partie en cours (puces « Cette partie »,
/// profil de la partie, bandeau pour la bande), la dépendance et les règles gagnées (Stupidité des Champignons).</summary>
public partial class WarbandDetailViewModel
{
    /// <summary>« Partie en cours · Bière de Bugman » : objets pris pour toute la bande, null sinon.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGameBanner))]
    private string? gameBanner;

    public bool HasGameBanner => !string.IsNullOrEmpty(GameBanner);

    private async Task<string?> ApplySingleUsesAsync(Warband warband, List<WarriorRow> rows, string language)
    {
        var uses = warband.GameInProgress ? await _warbandService.GetGameItemUsesAsync(warband.Id) : new List<GameItemUse>();
        var needsCatalog = uses.Count > 0 || rows.Any(r => r.Warrior.AddictedToEquipmentItemId is not null || r.Warrior.GainedSpecialRuleIds.Count > 0);
        if (!needsCatalog) return null;

        var items = (await _libraryService.GetEquipmentItemsAsync(language)).ToDictionary(i => i.Id);
        var rules = (await _libraryService.GetSpecialRulesAsync(language)).ToDictionary(r => r.Id);
        string ItemName(int id) => items.TryGetValue(id, out var item) ? item.Name : "?";

        foreach (var row in rows)
        {
            var warrior = row.Warrior;
            foreach (var ruleId in warrior.GainedSpecialRuleIds)
                if (rules.TryGetValue(ruleId, out var rule) && row.SpecialRules.All(c => c.Item.Id != ruleId))
                    row.SpecialRules.Add(new SpecialRuleChip { Item = rule, Name = rule.Name });
            if (warrior.AddictedToEquipmentItemId is { } addiction)
                row.AddictionText = string.Format(Loc["SingleUseAddictedFormat"], ItemName(addiction));

            var own = uses.Where(u => u.WarriorId == warrior.Id).ToList();
            if (own.Count == 0) continue;
            row.GameUseChips = own.Select(u => new GameUseChip(UseText(u, ItemName(u.EquipmentItemId)))).ToList();
            row.GameModifiers = own.Where(u => !u.WithoutEffect).SelectMany(u => u.StatModifiers).ToList();
            row.GameProfileText = GameProfileText(warrior, row.GameModifiers);
        }

        var bandUses = uses.Where(u => u.WarriorId is null).Select(u => ItemName(u.EquipmentItemId)).ToList();
        return bandUses.Count == 0 ? null : string.Format(Loc["SingleUseBandBannerFormat"], string.Join(", ", bandUses));
    }

    private string UseText(GameItemUse use, string itemName)
    {
        var details = new List<string>();
        if (!string.IsNullOrEmpty(use.WeaponName)) details.Add(use.WeaponName);
        if (!use.WithoutEffect && use.StatModifiers.Count > 0)
            details.Add(string.Join(", ", use.StatModifiers.Select(m => $"{Loc[$"Stat{m.Field}Abbr"]} {m.Value:+0;-0}")));
        if (use.WithoutEffect) details.Add(Loc["SingleUseWithoutEffectShort"]);
        return details.Count == 0 ? itemName : $"{itemName} · {string.Join(" · ", details)}";
    }

    /// <summary>« Pour cette partie : M 4 → 5, I 3 → 5 ».</summary>
    private string? GameProfileText(Warrior warrior, IReadOnlyList<RolledStatModifier> modifiers)
    {
        var parts = modifiers.Select(m => m.Field).Distinct()
            .Select(field => (field, value: CharacteristicModifier.Read(warrior, field)))
            .Where(x => x.value is not null)
            .Select(x => $"{Loc[$"Stat{x.field}Abbr"]} {x.value} → {SingleUseRules.GameValue(x.value!.Value, x.field, modifiers)}")
            .ToList();
        return parts.Count == 0 ? null : string.Format(Loc["SingleUseGameProfileFormat"], string.Join(", ", parts));
    }
}
