using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Résultat déjà résolu d'un jet de Progression (le wizard de Fin de partie fait le jet et les
/// choix) : compétences choisies, sort tiré, ou caractéristique +1.</summary>
public sealed record AdvanceResult(IReadOnlyList<Skill> Skills, Spell? Spell, CharacteristicField? Field)
{
    public static AdvanceResult None { get; } = new([], null, null);
}

/// <summary>Application en base des progressions de la Fin de partie - sortie du wizard (MAUI) le 2026-11-11
/// pour être couverte par des tests d'intégration, après quatre bugs trouvés en recette sur la promotion.
/// Ne fait aucun jet et n'écrit aucune phrase d'Historique : le wizard décide, ce service applique.</summary>
public sealed class AdvancementService
{
    private readonly IWarbandService _warbands;

    public AdvancementService(IWarbandService warbands) => _warbands = warbands;

    /// <summary>Attache les compétences/le sort et applique la caractéristique +1 à target. Ne sauvegarde pas
    /// target lui-même (l'appelant le fait une fois tous ses résultats appliqués) ; compétences et sort sont
    /// des lignes à part, écrites tout de suite (target doit donc déjà exister en base).</summary>
    public async Task ApplyAdvanceAsync(Warrior target, AdvanceResult result)
    {
        foreach (var skill in result.Skills)
            await _warbands.AddWarriorSkillAsync(target.Id, skill);
        if (result.Spell is { } spell)
            await _warbands.AddWarriorSpellAsync(target.Id, spell);
        if (result.Field is { } field)
            ApplyCharacteristicIncrease(target, field);
    }

    /// <summary>« The lad's got talent » : un modèle du groupe devient Héros.
    /// - Le Héros est créé depuis promotedDraft (clone du groupe, voir EntityMapping.CloneAsPromotedHero, nom et
    ///   2 listes déjà choisis) avec l'XP ACTUELLE du groupe (XP de la partie comprise), plus l'XP que l'Exploration
    ///   lui a donnée en tant que Héros (explorationExperience : XP répartie entre les Héros) ;
    /// - il part avec une copie de l'équipement du groupe (quantités par modèle, matériau, bénédiction) ;
    /// - son jet de Progression immédiat est appliqué et il est sauvegardé ;
    /// - le groupe perd un modèle et applique le jet du reste du groupe s'il y en a un - le groupe n'est PAS
    ///   sauvegardé ici (ni supprimé s'il tombe à 0) : c'est le flux normal de l'appelant.</summary>
    public async Task<Warrior> PromoteHenchmanAsync(Warrior group, Warrior promotedDraft, AdvanceResult heroAdvance, AdvanceResult? remainderAdvance,
        int explorationExperience = 0)
    {
        promotedDraft.Experience = group.Experience + Math.Max(0, explorationExperience);
        await _warbands.InsertWarriorAsync(promotedDraft);

        foreach (var carried in group.Equipment.Where(e => e.Id > 0))
        {
            var copy = await _warbands.AddWarriorEquipmentAsync(promotedDraft.Id, carried.Item, carried.Quantity, carried.MaterialRule, carried.FoundValueOverride);
            if (carried.BlessingRule is { } blessing)
                await _warbands.SetWarriorEquipmentBlessingRuleAsync(copy.Id, blessing.Id);
        }

        await ApplyAdvanceAsync(promotedDraft, heroAdvance);
        await _warbands.SaveWarriorAsync(promotedDraft);

        group.HeadCount -= 1;
        if (remainderAdvance is not null)
            await ApplyAdvanceAsync(group, remainderAdvance);
        return promotedDraft;
    }

    /// <summary>+1 sur la caractéristique ; un Homme de main mémorise qu'il l'a déjà augmentée (« jamais deux
    /// fois », voir CharacteristicIncreaseRules).</summary>
    public static void ApplyCharacteristicIncrease(Warrior warrior, CharacteristicField field)
    {
        switch (field)
        {
            case CharacteristicField.Movement: warrior.Movement += 1; break;
            case CharacteristicField.WeaponSkill: warrior.WeaponSkill += 1; break;
            case CharacteristicField.BallisticSkill: warrior.BallisticSkill += 1; break;
            case CharacteristicField.Strength: warrior.Strength += 1; break;
            case CharacteristicField.Toughness: warrior.Toughness += 1; break;
            case CharacteristicField.Wounds: warrior.Wounds += 1; break;
            case CharacteristicField.Initiative: warrior.Initiative += 1; break;
            case CharacteristicField.Attacks: warrior.Attacks += 1; break;
            case CharacteristicField.Leadership: warrior.Leadership += 1; break;
        }

        if (!warrior.IsHero && !warrior.IncreasedCharacteristics.Contains(field))
            warrior.IncreasedCharacteristics.Add(field);
    }
}
