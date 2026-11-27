using System.Text.Json.Serialization;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>Quand un objet à usage unique est consommé (Codex : « À usage unique »).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SingleUseTiming>))]
public enum SingleUseTiming
{
    /// <summary>Pris au début de partie, effet pour la partie (Ombre Cramoisie, Racine de Mandragore, Larmes de Shallaya…).</summary>
    BeforeBattle,
    /// <summary>Dure une partie, qu'il serve ou non (Ail, Poudre noire supérieure) : coché d'office au début de partie.</summary>
    LastsOneGame,
    /// <summary>Utilisé pendant la bataille, à cocher en Fin de partie (Herbes curatives, Eau bénite, Bombe incendiaire).</summary>
    DuringBattle,
}

/// <summary>Sur qui porte l'objet.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SingleUseTarget>))]
public enum SingleUseTarget
{
    Warrior,
    /// <summary>Poison : enduit une arme du guerrier (jamais une arme à poudre noire).</summary>
    Weapon,
    /// <summary>Toute la bande (Bière de Bugman).</summary>
    Warband,
}

/// <summary>Bonus de caractéristique pour la partie : Value fixe, plus DiceSides (D3 = 3) lancé au début de partie.</summary>
public sealed class GameStatModifier
{
    [JsonConverter(typeof(JsonStringEnumConverter<CharacteristicField>))]
    public CharacteristicField Field { get; set; }
    public int Value { get; set; }
    public int? DiceSides { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<AfterBattleOutcomeKind>))]
public enum AfterBattleOutcomeKind
{
    /// <summary>Devient dépendant : une dose avant chaque bataille, sinon il quitte la bande (Ombre Cramoisie).</summary>
    Addiction,
    /// <summary>Caractéristique modifiée de façon permanente (Field, Delta).</summary>
    CharacteristicChange,
    /// <summary>Gagne une règle spéciale permanente (SpecialRuleId = id officiel, ex. Stupidité).</summary>
    GainSpecialRule,
}

/// <summary>Une ligne de la table de conséquences jouée après la bataille (Min..Max inclus).</summary>
public sealed class AfterBattleOutcome
{
    public int Min { get; set; }
    public int Max { get; set; }
    public AfterBattleOutcomeKind Kind { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<CharacteristicField>))]
    public CharacteristicField? Field { get; set; }
    public int? Delta { get; set; }
    public string? SpecialRuleId { get; set; }
}

/// <summary>Profil d'un objet à usage unique (EquipmentItem.SingleUse, null = objet ordinaire). Stocké en JSON dans
/// EquipmentItemEntity.SingleUseJson (même choix que Skill.Effects : une colonne simple pour la synchro du contenu
/// officiel, références par id officiel). L'appli propose, le joueur décide : rien n'est imposé.</summary>
public sealed class SingleUseProfile
{
    public SingleUseTiming Timing { get; set; }
    public SingleUseTarget Target { get; set; }
    public List<GameStatModifier> StatModifiers { get; set; } = new();

    /// <summary>Nombre et faces des dés de la table de conséquences (2D6 : 2 et 6), null = pas de table.</summary>
    public int? AfterBattleDiceCount { get; set; }
    public int? AfterBattleDiceSides { get; set; }
    public List<AfterBattleOutcome> AfterBattle { get; set; } = new();
}
