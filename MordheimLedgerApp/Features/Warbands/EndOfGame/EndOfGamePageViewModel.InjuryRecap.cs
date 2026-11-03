namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Une ligne de l'étape "Bilan des blessures" - un guerrier mis hors de combat, ce qu'il lui
/// arrive (IsLoss : mort, figurines perdues, équipement perdu - affiché en rouge).</summary>
public sealed record InjuryRecapLine(string Name, string Text, bool IsLoss);

/// <summary>Étape "Bilan des blessures" (pas une étape du livre) - juste après la dernière étape Blessure/
/// Vendu aux Fosses, avant Prisonniers : qui est mort, combien de figurines chaque groupe perd et ce qu'il
/// en reste, qui perd son équipement (retour utilisateur 2026-11-02 - "pour les morts, rajouter un écran
/// récap après le jet de toutes les blessures"). Lecture seule, mêmes conditions que Terminer
/// (WarriorOutcomeRow.IsDead/FigureDeathCount/SurvivingHeadCount/LosesAllEquipment) ; rien n'est
/// appliqué avant Enregistrer, Précédent permet encore de corriger un jet.</summary>
public partial class EndOfGamePageViewModel
{
    public IEnumerable<InjuryRecapLine> InjuryRecapLines
    {
        get
        {
            foreach (var row in WarriorRows.Where(r => r.IsOutOfAction))
            {
                if (!row.Warrior.IsHero)
                {
                    var deaths = row.FigureDeathCount;
                    if (deaths == 0)
                        yield return new(row.Name, Loc["EndOfGameInjuryRecapNoLoss"], false);
                    else if (row.SurvivingHeadCount <= 0)
                        yield return new(row.Name, Loc["EndOfGameInjuryRecapWiped"], true);
                    else
                        yield return new(row.Name, string.Format(Loc["EndOfGameInjuryRecapFiguresLost"], deaths, row.SurvivingHeadCount), true);
                    continue;
                }

                if (row.IsDead)
                {
                    yield return new(row.Name, Loc["EndOfGameDeadLabel"], true);
                    continue;
                }

                var injuries = new List<string>();
                if (!string.IsNullOrWhiteSpace(row.InjuryResultText)) injuries.Add(row.InjuryShortName);
                injuries.AddRange(row.MultipleInjuryRolls.Where(s => !string.IsNullOrWhiteSpace(s.InjuryResultText)).Select(s => s.InjuryShortName));
                injuries.AddRange(row.SoldToPitsRerollRoll.Concat(row.MultipleInjuryRolls.SelectMany(s => s.SoldToPitsRerollRoll))
                    .Where(s => !string.IsNullOrWhiteSpace(s.InjuryResultText)).Select(s => s.InjuryShortName));
                yield return new(row.Name, injuries.Count > 0 ? string.Join(", ", injuries) : Loc["EndOfGameInjuryRecapNoLoss"], false);

                if (row.LosesAllEquipment)
                    yield return new(row.Name, Loc["EndOfGameInjuryRecapEquipmentLost"], true);
            }
        }
    }
}
