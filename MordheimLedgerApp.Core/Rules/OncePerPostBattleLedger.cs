namespace MordheimLedgerApp.Core.Rules;

/// <summary>Usages des effets « une fois par après-bataille » (Marchandage...) pendant un wizard Fin de
/// partie. Un effet n'est actif qu'à un seul endroit : l'utiliser ailleurs le déplace, et l'appelant décoche
/// l'ancien emplacement (renvoyé par Use) après avoir prévenu le joueur. Clés : ActiveEffect.Key ;
/// emplacements : libres, choisis par l'appelant (ex. "rare:12", "purchase:3").</summary>
public sealed class OncePerPostBattleLedger
{
    private readonly Dictionary<string, string> _locationByKey = new();

    /// <summary>Emplacement où l'effet est actuellement utilisé, null s'il est libre.</summary>
    public string? UsedAt(string key) => _locationByKey.GetValueOrDefault(key);

    /// <summary>Utilise l'effet à location. Renvoie l'emplacement qu'il quitte (à décocher), null s'il était
    /// libre ou déjà utilisé ici.</summary>
    public string? Use(string key, string location)
    {
        var previous = _locationByKey.GetValueOrDefault(key);
        _locationByKey[key] = location;
        return previous == location ? null : previous;
    }

    /// <summary>Un emplacement ne reçoit qu'un seul effet de ce registre : deux Marchandages ne se cumulent pas sur
    /// le même achat (chacun vaut pour « un objet », et le cumul ferait passer le prix sous son minimum). Vrai si
    /// un autre effet que key occupe déjà location.</summary>
    public bool IsTakenByOther(string location, string key) =>
        _locationByKey.Any(p => p.Value == location && p.Key != key);

    /// <summary>Libère l'effet s'il est utilisé à location (annulation) - sans effet sinon.</summary>
    public void Release(string key, string location)
    {
        if (_locationByKey.TryGetValue(key, out var current) && current == location)
            _locationByKey.Remove(key);
    }

    /// <summary>Libère tous les effets utilisés à location (ex. un achat retiré).</summary>
    public void ReleaseLocation(string location)
    {
        foreach (var key in _locationByKey.Where(p => p.Value == location).Select(p => p.Key).ToList())
            _locationByKey.Remove(key);
    }
}
