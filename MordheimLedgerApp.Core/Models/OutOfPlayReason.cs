namespace MordheimLedgerApp.Core.Models;

/// <summary>Pourquoi un guerrier ne combat plus (Mentions honorables de la fiche). Le statut (Mort/Retraité) reste la
/// source de vérité pour les règles ; la cause ne sert qu'à l'affichage (pastille, couleur) et à l'Historique.</summary>
public enum OutOfPlayReason
{
    /// <summary>Non précisée : un mort, ou un retraité d'avant la 1.6.</summary>
    Unspecified = 0,
    /// <summary>Capturé et jamais racheté.</summary>
    Captured = 1,
    /// <summary>Retraite forcée par une blessure (deux yeux perdus).</summary>
    ForcedRetirement = 2,
    /// <summary>Renvoyé à la Fin de partie.</summary>
    Dismissed = 3,
    /// <summary>Dépendant sans dose au début de partie.</summary>
    Addiction = 4,
    /// <summary>Franc-Tireur ou personnage dont la solde n'a pas été payée.</summary>
    Unpaid = 5,
    /// <summary>Dramatis Persona vagabond reparti après la bataille.</summary>
    WandererLeft = 6,
    /// <summary>Dramatis Persona qui n'est pas venu au début de partie (aide conditionnelle ratée).</summary>
    DidNotJoin = 7,
}
