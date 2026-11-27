using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Famille d'une mention honorable : une couleur par famille sur la fiche (pastille + bordure).</summary>
public enum HonorableMentionKind
{
    Died,
    Left,
    Dismissed,
    Lost,
}

/// <summary>Mentions honorables : les guerriers qui ne combattent plus (morts, partis, renvoyés, perdus), gardés sur la
/// fiche en lecture seule.</summary>
public static class HonorableMentionRules
{
    public static bool IsHonorableMention(Warrior warrior) => warrior.Status is WarriorStatus.Dead or WarriorStatus.Retired;

    /// <summary>Famille d'un guerrier sorti, null s'il combat encore.</summary>
    public static HonorableMentionKind? Kind(Warrior warrior) => warrior.Status switch
    {
        WarriorStatus.Dead => warrior.OutReason == OutOfPlayReason.Captured ? HonorableMentionKind.Lost : HonorableMentionKind.Died,
        WarriorStatus.Retired => warrior.OutReason switch
        {
            OutOfPlayReason.Dismissed => HonorableMentionKind.Dismissed,
            OutOfPlayReason.Addiction or OutOfPlayReason.Unpaid or OutOfPlayReason.WandererLeft or OutOfPlayReason.DidNotJoin => HonorableMentionKind.Left,
            _ => HonorableMentionKind.Lost,
        },
        _ => null,
    };
}
