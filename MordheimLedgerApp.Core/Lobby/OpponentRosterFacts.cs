using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Exchange;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Ce que la Fin de partie connectée (v2, lot 4b) lit directement dans le paquet d'une bande adverse, sans
/// l'importer : les Dramatis Personae qu'elle aligne (corruption d'« Une Poignée d'Or » proposée seulement pour eux).</summary>
public static class OpponentRosterFacts
{
    /// <summary>Clés du catalogue (« o:&lt;OfficialId&gt; » ou « s:&lt;GUID&gt; ») des Dramatis Personae présents dans les
    /// bandes du paquet. Les guerriers morts ne sont plus dans la base, donc plus dans le paquet.</summary>
    public static HashSet<string> DramatisPersonaKeys(ExchangePackage package)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var warband in package.Warbands)
        {
            if (!warband.Children.TryGetValue(nameof(WarriorEntity), out var warriors)) continue;
            foreach (var warrior in warriors)
                if (warrior.References.TryGetValue(nameof(WarriorEntity.DramatisPersonaId), out var key))
                    keys.Add(key);
        }
        return keys;
    }

    /// <summary>Même chose depuis le JSON du lobby (LobbySnapshot.Packages) ; un paquet illisible ne compte pour rien.</summary>
    public static HashSet<string> DramatisPersonaKeys(string packageJson)
    {
        try { return DramatisPersonaKeys(ExchangeArchive.FromJson(packageJson)); }
        catch (ExchangeFormatException) { return []; }
    }
}
