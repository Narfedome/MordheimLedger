namespace MordheimLedgerApp.Core.Rules;

/// <summary>Part de l'XP d'une partie que l'appli peut proposer d'elle-même (livre, section Expérience) : +1 de survie
/// (Héros et groupes, même mis hors de combat), +1 au chef vainqueur (le chef en place pendant la partie), plus le
/// bonus d'outsider. Les ennemis mis hors de combat et les bonus de scénario restent ajoutés par les joueurs : ce
/// n'est qu'un pré-remplissage, jamais imposé.</summary>
public readonly record struct ExperiencePrefill(int Survival, int WinningLeader, int Underdog)
{
    public int Total => Survival + WinningLeader + Underdog;

    public static ExperiencePrefill None => new(0, 0, 0);

    /// <summary>Le joueur n'a pas touché au champ : il est vide, ou vaut encore le dernier pré-remplissage posé. Seul un
    /// champ intact est recalculé (un changement de résultat, par exemple) - une saisie du joueur n'est jamais écrasée.</summary>
    public static bool IsUntouched(string? current, string? lastApplied) =>
        string.IsNullOrEmpty(current) || current == lastApplied;

    /// <param name="gainsExperience">Faux pour un guerrier qui ne gagne jamais d'XP (ex. Zombies).</param>
    /// <param name="survived">Faux si le guerrier (ou tout le groupe) est mort pendant cette partie.</param>
    /// <param name="wasLeaderDuringBattle">Chef de la bande pendant la partie (pas un successeur désigné après).</param>
    /// <param name="victory">La bande a gagné la partie.</param>
    /// <param name="underdogBonus">Voir UnderdogExperience.BonusFor.</param>
    public static ExperiencePrefill For(bool gainsExperience, bool survived, bool wasLeaderDuringBattle, bool victory, int underdogBonus)
    {
        if (!gainsExperience || !survived) return None;
        return new ExperiencePrefill(1, wasLeaderDuringBattle && victory ? 1 : 0, Math.Max(0, underdogBonus));
    }
}
