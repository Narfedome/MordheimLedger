namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// Reference lookup for the rulebook's Serious Injury table (D66: two D6, first die = tens digit).
/// Pure flavor/reference text - deliberately does not mutate any Warrior stat itself (see the
/// roadmap's "no rules engine in V1" boundary, also documented on Models/WarriorStatus.cs): the
/// player reads the result and applies it by hand (Notes, Status).
///
/// IMPORTANT: entered from general knowledge of the Mordheim rulebook, NOT verified against the
/// actual book (same caveat as OfficialContentSeed.cs's Reiklander Mercenaries stats) - the exact
/// roll ranges and wording here are very likely off and need a full rewrite from the real table.
/// </summary>
public static class SeriousInjuryTable
{
    private static readonly (int Roll, string Text)[] Entries =
    [
        (11, "Mort. Le guerrier est perdu."),
        (12, "Mort. Le guerrier est perdu."),
        (13, "Mort. Le guerrier est perdu."),
        (14, "Mort. Le guerrier est perdu."),
        (15, "Mort. Le guerrier est perdu."),
        (16, "Blessures multiples : retirer deux fois de plus sur cette table (un nouveau 16 est ignoré)."),
        (21, "Jambe blessée : Mouvement réduit de façon permanente."),
        (22, "Bras blessé : Force réduite de façon permanente."),
        (23, "Folie passagère : rate la prochaine bataille."),
        (24, "Blessure à la poitrine : Endurance réduite de façon permanente."),
        (25, "Borgne : Capacité de Tir (ou Capacité de Combat) réduite de façon permanente."),
        (26, "Vieille blessure de guerre : rate la prochaine bataille, sauf soins."),
        (31, "Nerfs fragiles : doit réussir un test de Commandement pour charger à l'avenir."),
        (32, "Endurci : rien cette fois, immunisé contre le prochain résultat de folie."),
        (33, "Main blessée : Capacité de Tir réduite de façon permanente."),
        (34, "Capturé par une bande rivale : peut être libéré contre rançon."),
        (35, "Rétablissement complet."),
        (36, "Rétablissement complet."),
        (41, "Rétablissement complet."),
        (42, "Rétablissement complet."),
        (43, "Rétablissement complet."),
        (44, "Détroussé : perd un objet au hasard de son équipement."),
        (45, "Survit contre toute attente : gagne 1 point d'expérience."),
        (46, "Haine tenace envers la bande responsable."),
        (51, "Cou raide : Initiative réduite de façon permanente."),
        (52, "Jambe folle : Mouvement réduit de façon permanente."),
        (53, "Cicatrices impressionnantes : Commandement réduit, mais provoque la Peur."),
        (54, "Rétablissement complet."),
        (55, "Rétablissement complet."),
        (56, "Vendu aux arènes : le guerrier est perdu, sauf rachat."),
        (61, "Rétablissement complet."),
        (62, "Rétablissement complet."),
        (63, "Rétablissement complet."),
        (64, "Rétablissement complet."),
        (65, "Rétablissement complet."),
        (66, "Combattant aguerri : bonus mineur à la prochaine bataille."),
    ];

    public static (int Roll, string Text) Roll()
    {
        var tens = Random.Shared.Next(1, 7);
        var units = Random.Shared.Next(1, 7);
        var roll = tens * 10 + units;
        var entry = Array.Find(Entries, e => e.Roll == roll);
        return entry;
    }
}
