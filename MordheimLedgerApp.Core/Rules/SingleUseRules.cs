using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Bonus de caractéristique tiré au début de partie pour un objet à usage unique (Ombre Cramoisie : I +2).</summary>
public sealed record RolledStatModifier(CharacteristicField Field, int Value);

/// <summary>Objets à usage unique (drogues, poisons, Ail, Herbes curatives…) : ce que l'appli propose au début et à la
/// fin de partie. Rien n'est imposé : un objet sans effet sur la race du guerrier reste cochable (message ambre).</summary>
public static class SingleUseRules
{
    public static bool IsSingleUse(EquipmentItem item) => item.SingleUse is not null;

    /// <summary>Proposé au début de partie : pris avant la bataille ou valable une partie.</summary>
    public static bool IsOfferedBeforeBattle(EquipmentItem item) =>
        item.SingleUse?.Timing is SingleUseTiming.BeforeBattle or SingleUseTiming.LastsOneGame;

    /// <summary>Coché d'office au début de partie : l'Ail ou la Torche durent une partie, qu'ils servent ou non.</summary>
    public static bool IsPreChecked(EquipmentItem item) => item.SingleUse?.Timing == SingleUseTiming.LastsOneGame;

    /// <summary>À cocher en Fin de partie s'il a servi (Herbes curatives, Eau bénite…).</summary>
    public static bool IsAskedAfterBattle(EquipmentItem item) => item.SingleUse?.Timing == SingleUseTiming.DuringBattle;

    /// <summary>Arme qu'un poison peut enduire : arme de corps à corps ou de tir, jamais à poudre noire (livre).</summary>
    public static bool IsPoisonable(EquipmentItem weapon) =>
        weapon.Category is EquipmentCategory.MeleeWeapon or EquipmentCategory.MissileWeapon;

    /// <summary>Bonus de la partie, dés lancés (rollDie(faces) → 1..faces). Un bonus nul n'est pas gardé.</summary>
    public static IReadOnlyList<RolledStatModifier> RollStatModifiers(SingleUseProfile profile, Func<int, int> rollDie) =>
        profile.StatModifiers
            .Select(m => new RolledStatModifier(m.Field, m.Value + (m.DiceSides is { } sides and > 0 ? rollDie(sides) : 0)))
            .Where(m => m.Value != 0)
            .ToList();

    /// <summary>Faces des dés de la table de conséquences (2D6 → (2, 6)), null = pas de jet après la bataille.</summary>
    public static (int Count, int Sides)? AfterBattleDice(SingleUseProfile profile) =>
        profile.AfterBattle.Count > 0 && profile.AfterBattleDiceCount is { } count and > 0 && profile.AfterBattleDiceSides is { } sides and > 0
            ? (count, sides)
            : null;

    /// <summary>Ligne de la table pour ce total, null = sans effet.</summary>
    public static AfterBattleOutcome? ResolveAfterBattle(SingleUseProfile profile, int total) =>
        profile.AfterBattle.FirstOrDefault(o => total >= o.Min && total <= o.Max);

    /// <summary>Objet qui peut être perdu parce que son porteur a été mis hors de combat (Habits de soie, si c'est le chef).</summary>
    public static bool IsAtRiskAfterOutOfAction(EquipmentItem item, bool carrierIsLeader) =>
        item.LostAfterOutOfActionOn is > 0 && (carrierIsLeader || !item.LostAfterOutOfActionLeaderOnly);

    /// <summary>Jet de 1D6 : perdu si le résultat est inférieur ou égal au seuil de l'objet.</summary>
    public static bool IsLostAfterOutOfAction(EquipmentItem item, int roll) =>
        item.LostAfterOutOfActionOn is { } upTo && roll >= 1 && roll <= upTo;

    /// <summary>Valeur d'une caractéristique pour la partie : profil + bonus de la partie.</summary>
    public static int GameValue(int baseValue, CharacteristicField field, IEnumerable<RolledStatModifier> modifiers) =>
        baseValue + modifiers.Where(m => m.Field == field).Sum(m => m.Value);
}
