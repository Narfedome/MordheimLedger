using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Écritures des blessures de la Fin de partie (sorties du wizard le 2026-11-11, même principe
/// qu'AdvancementService) : le wizard résout les jets et choisit l'ordre (jet principal, sous-jets de
/// « Blessures multiples », relance d'un combat de gladiateur perdu), ce service applique chaque résultat.
///
/// La trésorerie (rançon, gain d'un combat de gladiateur) est modifiée sur l'objet Warband passé, sans le
/// sauvegarder : la Fin de partie sauvegarde la bande une fois tout appliqué. Le guerrier non plus n'est pas
/// sauvegardé à chaque résultat : voir FinishWarriorAsync.</summary>
public sealed class InjuryService
{
    /// <summary>Gain d'un combat de gladiateur remporté (Vendu aux Fosses, 65).</summary>
    public const int PitFightWinGold = 50;

    /// <summary>XP d'un combat de gladiateur remporté.</summary>
    public const int PitFightWinExperience = 2;

    private readonly IWarbandService _warbands;
    private readonly ILibraryService _library;
    private readonly string _languageCode;
    private List<Injury>? _catalog;

    public InjuryService(IWarbandService warbands, ILibraryService library, string languageCode)
    {
        _warbands = warbands;
        _library = library;
        _languageCode = languageCode;
    }

    /// <summary>L'entrée du catalogue de blessures pour ce jet (et cette branche, pour 23/25) - créée à partir
    /// de fallbackText seulement si le jet ne correspond à aucune entrée officielle.</summary>
    public async Task<Injury> ResolveInjuryAsync(int roll, bool isHero, string fallbackText, int? branchSubRoll = null)
    {
        _catalog ??= await _library.GetInjuriesAsync(_languageCode);
        var category = isHero ? InjuryCategory.Hero : InjuryCategory.Henchman;
        // Jet hors table : une blessure déjà créée sous ce nom est réutilisée plutôt que dupliquée à chaque fois.
        var injury = InjuryCatalogLookup.Find(_catalog, category, roll, branchSubRoll)
            ?? _catalog.FirstOrDefault(i => i.Category == category && i.Name == fallbackText);
        if (injury is not null) return injury;

        injury = new Injury { Name = fallbackText, Category = category, Source = ContentSource.Official };
        await _library.SaveInjuryAsync(injury, _languageCode);
        _catalog.Add(injury);
        return injury;
    }

    /// <summary>Ajoute la blessure (puce temporaire si elle ne fait que manquer des parties) et applique son
    /// effet. Renvoie true si le guerrier a changé et doit être sauvegardé.</summary>
    public async Task<bool> RecordInjuryAsync(Warrior warrior, Injury injury, SeriousInjuryOutcome? outcome)
    {
        var isTemporary = outcome?.Kind is SeriousInjuryEffectKind.MissNextGame or SeriousInjuryEffectKind.MissGamesRollD3;
        await _warbands.AddWarriorInjuryAsync(warrior.Id, injury, isTemporary);
        return outcome is not null && await ApplyEffectAsync(warrior, outcome);
    }

    /// <summary>Effet d'une Blessure grave (Palier 1, voir SeriousInjuryEffectTable). Les parties manquées se
    /// CUMULENT (« Blessures multiples » : cumulez tous les effets). Renvoie true si le guerrier a changé ;
    /// la perte d'équipement s'écrit d'elle-même.</summary>
    public async Task<bool> ApplyEffectAsync(Warrior warrior, SeriousInjuryOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case SeriousInjuryEffectKind.CharacteristicPenalty when outcome.Field is { } field:
                CharacteristicModifier.Apply(warrior, field, -1);
                return true;
            case SeriousInjuryEffectKind.LoseAllEquipment:
                await StripEquipmentAsync(warrior);
                return false;
            case SeriousInjuryEffectKind.GainExperience:
                warrior.Experience += 1;
                return true;
            case SeriousInjuryEffectKind.MissNextGame:
                warrior.Status = WarriorStatus.Sick;
                warrior.SickGamesRemaining += 1;
                return true;
            case SeriousInjuryEffectKind.MissGamesRollD3:
                warrior.Status = WarriorStatus.Sick;
                warrior.SickGamesRemaining += outcome.Value ?? SeriousInjuryEffectTable.RollD3();
                return true;
            case SeriousInjuryEffectKind.ForcedRetirement:
                warrior.Status = WarriorStatus.Retired;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Rancune (56) : la cible devient une WarriorHatred (une bande du catalogue ou un texte libre).</summary>
    public Task AddHatredAsync(Warrior warrior, int? targetWarbandArchetypeId, string? targetFreeText) =>
        _warbands.AddWarriorHatredAsync(warrior.Id, targetWarbandArchetypeId, targetFreeText);

    /// <summary>Capturé (61) : racheté = la rançon est payée et le guerrier revient avec son équipement ; sinon
    /// il est perdu pour de bon (considéré mort, équipement perdu).</summary>
    public async Task CaptureAsync(Warband warband, Warrior warrior, int? ransom)
    {
        if (ransom is { } paid)
        {
            warband.Treasury -= paid;
            return;
        }
        warrior.Status = WarriorStatus.Dead;
        await StripEquipmentAsync(warrior);
    }

    /// <summary>Vendu aux Fosses (65), combat gagné : or et XP, équipement intact.</summary>
    public void WinPitFight(Warband warband, Warrior warrior)
    {
        warband.Treasury += PitFightWinGold;
        warrior.Experience += PitFightWinExperience;
    }

    /// <summary>Vendu aux Fosses, combat perdu et survécu à la relance : équipement perdu sans condition.</summary>
    public Task LosePitFightAsync(Warrior warrior) => StripEquipmentAsync(warrior);

    /// <summary>Mort sur un jet.</summary>
    public static void Kill(Warrior warrior) => warrior.Status = WarriorStatus.Dead;

    /// <summary>Hommes de main : les figurines mortes sont retirées de l'effectif. Aucune blessure n'est
    /// enregistrée sur le groupe - la table des Hommes de main ne laisse rien de durable (mort ou rétablissement
    /// complet), une puce « Perdu » ou « Rétablissement complet » par figurine n'avait pas de sens (recette du
    /// 2026-11-11). Renvoie true si le groupe est anéanti.</summary>
    public static bool RemoveDeadFigures(Warrior group, int deaths)
    {
        group.HeadCount -= Math.Max(0, deaths);
        return group.HeadCount <= 0;
    }

    /// <summary>Fin du traitement d'un guerrier : un groupe vide (morts, dernier membre promu) est supprimé,
    /// sinon le guerrier est sauvegardé s'il a changé.</summary>
    public async Task FinishWarriorAsync(Warrior warrior, bool changed)
    {
        if (warrior.HeadCount <= 0)
            await _warbands.DeleteWarriorAsync(warrior.Id);
        else if (changed)
            await _warbands.SaveWarriorAsync(warrior);
    }

    /// <summary>Perte de tout l'équipement - seulement les lignes déjà en base (id positif) : un objet réalloué
    /// à ce Héros pendant la même Fin de partie (id négatif) a été acquis APRÈS la blessure et lui reste.</summary>
    public async Task StripEquipmentAsync(Warrior warrior)
    {
        foreach (var equipment in warrior.Equipment.Where(e => e.Id > 0).ToList())
        {
            await _warbands.RemoveWarriorEquipmentAsync(equipment.Id);
            warrior.Equipment.Remove(equipment);
        }
    }
}
