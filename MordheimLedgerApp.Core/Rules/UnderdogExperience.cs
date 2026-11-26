namespace MordheimLedgerApp.Core.Rules;

/// <summary>XP d'outsider (livre, section Expérience, table « Underdogs ») : la bande dont la Valeur est la plus
/// basse gagne de l'XP en plus selon l'écart. Multijoueur (Town Cryer 5) : seule la bande la plus faible peut être
/// l'outsider, comparée à la bande juste au-dessus d'elle - ni la plus forte, ni une moyenne. Sert à pré-remplir
/// l'étape Expérience ; le joueur garde la main sur le total.</summary>
public static class UnderdogExperience
{
    /// <summary>Un palier de la table : un écart d'au moins MinDifference donne Bonus points d'XP.</summary>
    public readonly record struct Tier(int MinDifference, int Bonus);

    /// <summary>Croissant par MinDifference ; un écart de 0 à 50 ne donne rien.</summary>
    public static readonly IReadOnlyList<Tier> Tiers = new[]
    {
        new Tier(51, 1),
        new Tier(76, 2),
        new Tier(101, 3),
        new Tier(151, 4),
        new Tier(301, 5)
    };

    /// <summary>Bonus pour un écart de Valeur (négatif ou nul = aucun bonus).</summary>
    public static int BonusForGap(int ratingGap)
    {
        var bonus = 0;
        foreach (var tier in Tiers)
        {
            if (ratingGap >= tier.MinDifference)
                bonus = tier.Bonus;
        }
        return bonus;
    }

    /// <summary>Écart retenu pour une bande face à ses adversaires : null si elle n'est pas strictement la plus faible
    /// (ou sans adversaire connu), sinon la Valeur de l'adversaire le plus faible moins la sienne.</summary>
    public static int? GapFor(int ownRating, IReadOnlyCollection<int> opponentRatings)
    {
        if (opponentRatings.Count == 0) return null;
        var nextHighest = opponentRatings.Min();
        return nextHighest > ownRating ? nextHighest - ownRating : null;
    }

    /// <summary>Bonus d'XP d'outsider de chaque Héros et groupe survivant de la bande.</summary>
    public static int BonusFor(int ownRating, IReadOnlyCollection<int> opponentRatings) =>
        GapFor(ownRating, opponentRatings) is { } gap ? BonusForGap(gap) : 0;
}
