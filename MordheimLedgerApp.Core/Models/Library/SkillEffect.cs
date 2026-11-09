using System.Text.Json.Serialization;

namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>Moment du jeu où un effet intervient - calqué sur les étapes du wizard Fin de partie et sur les
/// sélecteurs qui en tiennent compte (voir Core.Rules.RuleEffectResolver).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EffectMoment>))]
public enum EffectMoment
{
    /// <summary>Jet de recherche d'un objet rare (2D6 contre la Rareté).</summary>
    RareItemSearch,
    /// <summary>N'importe quel achat de l'après-bataille (Objets rares, Achat, Recrutement).</summary>
    Purchase,
    /// <summary>Jet d'Exploration.</summary>
    ExplorationRoll,
    /// <summary>Jet de Blessure grave d'un Héros.</summary>
    SeriousInjuryRoll,
    /// <summary>Choix d'une compétence (Progression, ajout manuel).</summary>
    SkillChoice,
    /// <summary>Choix d'un équipement pour un guerrier précis.</summary>
    EquipmentChoice,
    /// <summary>Choix d'un sort.</summary>
    SpellChoice,
    /// <summary>Augmentation d'une caractéristique (maximum racial).</summary>
    CharacteristicIncrease,
    /// <summary>Pendant la partie : l'app ne calcule rien, simple rappel.</summary>
    InGame,
}

/// <summary>Nature d'un effet - ce que l'app en fait au moment concerné.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EffectKind>))]
public enum EffectKind
{
    /// <summary>Bonus/malus au jet (Value = "+2", "-1").</summary>
    Modifier,
    /// <summary>Relance, le second résultat reste (Value = nombre de dés, "1" par défaut).</summary>
    Reroll,
    /// <summary>Ajuster un dé au choix de ±n (Value = "1").</summary>
    AdjustDie,
    /// <summary>Dé(s) supplémentaire(s) (Value = "1").</summary>
    ExtraDie,
    /// <summary>Réduction de prix (Value = formule de dés, ex. "2D6" ; MinimumPrice = plancher).</summary>
    Discount,
    /// <summary>Ouvre un sélecteur au-delà de ses restrictions normales (Targets = catégories ou OfficialId).</summary>
    Allow,
    /// <summary>Prérequis pour choisir la compétence (Condition) - signalé en ambre, jamais bloquant.</summary>
    Prerequisite,
    /// <summary>Simple rappel au moment concerné (Note, ou à défaut la description de la compétence).</summary>
    Reminder,
}

/// <summary>Condition qui doit être remplie pour que l'effet s'applique. Une condition non remplie n'est
/// jamais cachée : l'effet remonte avec ConditionMet = false (message ambre).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EffectCondition>))]
public enum EffectCondition
{
    None,
    /// <summary>Le guerrier n'a pas été mis hors de combat pendant la partie.</summary>
    NotOutOfAction,
    /// <summary>Le guerrier n'a pas été tué pendant la partie (Blessure grave 11-15) - hors de combat ne
    /// suffit pas à l'exclure (FAQ : les modificateurs ±1 d'Exploration des Nains et des Elfes restent
    /// valables « if not killed »).</summary>
    NotDead,
    /// <summary>Le guerrier est le chef de la bande.</summary>
    Leader,
    /// <summary>Le guerrier peut lancer des sorts.</summary>
    Spellcaster,
    /// <summary>Le guerrier porte l'un des objets de ConditionTargets (OfficialId d'équipement).</summary>
    CarriesItem,
    /// <summary>Ni la bande ni le type de guerrier ne figurent dans ConditionTargets (OfficialId de bande
    /// ou de type de guerrier).</summary>
    NotInWarbandOrArchetype,
}

[JsonConverter(typeof(JsonStringEnumConverter<EffectFrequency>))]
public enum EffectFrequency
{
    Permanent,
    /// <summary>Un seul usage actif par après-bataille : le réutiliser ailleurs le déplace (voir
    /// Core.Rules.OncePerPostBattleLedger).</summary>
    OncePerPostBattle,
}

/// <summary>Effet de règle porté par une compétence (Skill.Effects) : ce que l'app propose au joueur à un
/// moment donné, sans jamais l'imposer. Stocké en JSON dans SkillEntity.EffectsJson et déclaré tel quel dans
/// les JSON de seed (champ "effects"). Les références (Targets, ConditionTargets) passent par OfficialId ou
/// par nom d'enum, jamais par Id local - elles restent donc valides d'une base à l'autre.</summary>
public class SkillEffect
{
    public EffectMoment Moment { get; set; }
    public EffectKind Kind { get; set; }

    /// <summary>Valeur selon Kind : "+2" (Modifier), "2D6" (Discount), "1" (Reroll/AdjustDie/ExtraDie).</summary>
    public string? Value { get; set; }

    /// <summary>Pour Allow : noms d'EquipmentCategory/SkillCategory/CharacteristicField, ou OfficialId (école
    /// de magie, compétence).</summary>
    public List<string> Targets { get; set; } = new();

    public EffectCondition Condition { get; set; }

    /// <summary>OfficialId visés par CarriesItem/NotInWarbandOrArchetype.</summary>
    public List<string> ConditionTargets { get; set; } = new();

    public EffectFrequency Frequency { get; set; }

    /// <summary>Pour Discount : prix plancher (Marchandage : 1 CO).</summary>
    public int? MinimumPrice { get; set; }

    /// <summary>Texte libre affiché avec l'effet (rappel) - null = description de la compétence.</summary>
    public string? Note { get; set; }

    public SkillEffect Clone() => new()
    {
        Moment = Moment,
        Kind = Kind,
        Value = Value,
        Targets = new List<string>(Targets),
        Condition = Condition,
        ConditionTargets = new List<string>(ConditionTargets),
        Frequency = Frequency,
        MinimumPrice = MinimumPrice,
        Note = Note,
    };
}
