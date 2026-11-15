using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Exchange;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Un guerrier adverse et le nom de son rôle (type de guerrier, Franc-Tireur, Dramatis Persona).</summary>
public sealed record OpponentWarrior(Warrior Warrior, string RoleName);

/// <summary>Bande adverse prête à afficher en lecture seule.</summary>
public sealed record OpponentRoster(Warband Warband, string ArchetypeName, int Rating, IReadOnlyList<OpponentWarrior> Warriors);

/// <summary>Bande d'un adversaire de la partie connectée (v2, lot 4a), sans rien écrire dans la base du joueur
/// (décision : les bandes adverses ne restent que le temps de la partie, rien n'arrive dans son Codex). Le paquet est
/// importé dans une base temporaire, partie de la seed.db3 embarquée, puis relu avec les services habituels : même
/// résolution des noms et du contenu du Codex que pour une vraie bande.</summary>
public static class OpponentRosterLoader
{
    /// <param name="databasePath">Fichier de la base temporaire (remplacé s'il existe).</param>
    /// <param name="officialSeed">seed.db3 embarquée (null en test : la base se remplit depuis les JSON).</param>
    public static async Task<OpponentRoster> LoadAsync(string databasePath, Func<Task<Stream>>? officialSeed,
        ExchangePackage package, string languageCode)
    {
        if (File.Exists(databasePath)) File.Delete(databasePath);
        if (officialSeed is not null)
        {
            await using var source = await officialSeed();
            await using var target = File.Create(databasePath);
            await source.CopyToAsync(target);
        }

        var db = new AppDatabase(databasePath);
        try
        {
            var library = new LibraryService(db);
            var warbands = new WarbandService(db, library);
            var result = await new ExchangeService(db).ImportAsync(package);
            var warbandId = result.WarbandIds.Single();
            var warband = (await warbands.GetWarbandAsync(warbandId))!;
            var archetypeNames = (await library.GetWarriorArchetypesAsync(warband.WarbandArchetypeId, languageCode))
                .ToDictionary(a => a.Id, a => a.Name);
            var hiredSwords = (await library.GetHiredSwordsAsync(languageCode)).ToDictionary(h => h.Id, h => h.Name);
            var personae = (await library.GetDramatisPersonaeAsync(languageCode)).ToDictionary(p => p.Id, p => p.Name);
            var warriors = (await warbands.GetWarriorsAsync(warbandId, languageCode))
                .Where(w => w.Status is not (WarriorStatus.Dead or WarriorStatus.Retired))
                .OrderByDescending(w => w.IsHero)
                .Select(w => new OpponentWarrior(w,
                    w.WarriorArchetypeId is { } a && archetypeNames.TryGetValue(a, out var role) ? role
                    : w.HiredSwordId is { } h && hiredSwords.TryGetValue(h, out var sword) ? sword
                    : w.DramatisPersonaId is { } p && personae.TryGetValue(p, out var persona) ? persona
                    : string.Empty))
                .ToList();
            return new OpponentRoster(warband,
                await warbands.GetWarbandArchetypeNameAsync(warband.WarbandArchetypeId, languageCode),
                await warbands.GetWarbandRatingAsync(warbandId), warriors);
        }
        finally
        {
            await db.Connection.CloseAsync();
        }
    }
}
