using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Un guerrier de la bande, vu par la règle du Grimoire.</summary>
/// <param name="IsSpellcaster">Lance déjà des sorts (type sorcier, successeur, Franc-Tireur sorcier, Magie Mineure apprise).</param>
/// <param name="HasArcaneLore">Possède la compétence Savoir Arcanique.</param>
public sealed record TomeCandidate(Warrior Warrior, bool IsSpellcaster, bool HasArcaneLore);

public enum TomeUseKind
{
    /// <summary>La bande a un lanceur de sorts : il gagne un sort de plus (sa liste ou la Magie Mineure).</summary>
    ExtraSpell,
    /// <summary>Pas de lanceur : un Héros avec Savoir Arcanique apprend la Magie Mineure (et tire son premier sort).</summary>
    LearnLesserMagic,
    /// <summary>Personne ne peut l'utiliser : l'effet est perdu (il ne vaut qu'à l'acquisition).</summary>
    Nobody
}

/// <summary>Grimoire (Tome of Magic, livre + FAQ) : « If a warband includes a wizard, he will gain an extra spell from
/// the tome, permanently. He may randomly generate this new spell from his own list or the Lesser Magic list. »
/// Sinon, un guerrier avec Savoir Arcanique peut l'utiliser pour apprendre la Magie Mineure. L'effet ne profite qu'à
/// un seul guerrier et ne vaut qu'au moment où le Grimoire est trouvé : le donner ensuite à un autre n'a pas d'effet.</summary>
public static class TomeOfMagicRules
{
    /// <summary>Qui peut utiliser le Grimoire, et de quelle façon. Les morts ne comptent pas (filtrés par l'appelant).</summary>
    public static (TomeUseKind Kind, IReadOnlyList<Warrior> Users) Users(IReadOnlyList<TomeCandidate> warband)
    {
        var casters = warband.Where(c => c.IsSpellcaster).Select(c => c.Warrior).ToList();
        if (casters.Count > 0) return (TomeUseKind.ExtraSpell, casters);
        var learners = warband.Where(c => c.HasArcaneLore).Select(c => c.Warrior).ToList();
        return learners.Count > 0 ? (TomeUseKind.LearnLesserMagic, learners) : (TomeUseKind.Nobody, Array.Empty<Warrior>());
    }

    /// <summary>Nombre de Grimoires acquis pendant cette Fin de partie (objet rare, Exploration, scénario) - un effet chacun.</summary>
    public static int AcquiredCount(IEnumerable<(string? OfficialId, int Quantity)> acquisitions) =>
        acquisitions.Where(a => a.OfficialId == Data.OfficialIds.TomeOfMagicItem).Sum(a => Math.Max(0, a.Quantity));

    /// <summary>Assigner un Grimoire en dehors de son acquisition (inventaire, réallocation, correction) : il n'a plus
    /// d'effet - l'appli l'indique, sans rien bloquer.</summary>
    public static bool IsSpentTome(string? itemOfficialId) => itemOfficialId == Data.OfficialIds.TomeOfMagicItem;
}
