using System.Collections.ObjectModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Dramatis Personae" (StepKind.DramatisPersonae) - règle la solde de chaque Dramatis
/// Persona à frais récurrents déjà activement engagé : en or (Johann/Veskit/Marianna) OU en pierre
/// magique (Nicodemus, 2026-09-01 - "must be paid a wyrdstone shard... after every battle he fights") -
/// PAS Bertha/None. Entièrement absente du wizard si aucun personnage concerné.
///
/// "C'est l'heure de payer !"/"Corruption de Marquand Volker &amp; Ulli Leitpold" vivaient ici (et sur
/// l'étape Prisonniers) jusqu'au 2026-09-04, retour utilisateur : regroupées dans leur propre étape "Une
/// Poignée d'Or" juste après celle-ci (StepKind.PairEngagement, voir
/// EndOfGamePageViewModel.PairEngagement.cs) - la Corruption s'apparente à un "hire", la Rétention à un
/// "upkeep", mais les deux sont mutuellement exclusives et partagent tout leur état, donc une seule étape
/// à un seul emplacement plutôt que scindées en deux.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Catalogue complet (non filtré) - permet de résoudre le profil (FeeKind/Upkeep/Name,
    /// jamais stockés sur Warrior lui-même) d'un Dramatis Persona déjà engagé, même principe que
    /// _hiredSwordCatalog (EndOfGamePageViewModel.HiredSwords.cs). Aussi consommé par
    /// EndOfGamePageViewModel.PairEngagement.cs (Une Poignée d'Or), même catalogue.</summary>
    private List<DramatisPersona> _dramatisPersonaCatalog = new();

    public ObservableCollection<DramatisPersonaUpkeepEntry> DramatisPersonaUpkeepEntries { get; } = new();

    public bool HasDramatisPersonaeUpkeep => DramatisPersonaUpkeepEntries.Count > 0;

    /// <summary>Peuplée une seule fois à la construction du dialog (voir EndOfGamePageViewModel ctor) -
    /// mêmes vrais guerriers déjà recrutés que BuildHiredSwordUpkeepEntries, filtrés à FeeKind.Gold avec
    /// un Upkeep renseigné, OU FeeKind.Wyrdstone (Bertha/Ulli &amp; Marquand n'apparaissent jamais ici -
    /// leur propre paiement, "Où est l'Argent ?", est à part, voir EndOfGamePageViewModel.
    /// PairEngagement.cs).</summary>
    private void BuildDramatisPersonaUpkeepEntries()
    {
        var payLabel = Loc["EndOfGameHiredSwordPayAction"];
        var dismissLabel = Loc["WarbandsDismissHiredSwordAction"];
        foreach (var row in WarriorRows.Where(r => r.Warrior.IsDramatisPersona))
        {
            var persona = _dramatisPersonaCatalog.FirstOrDefault(p => p.Id == row.Warrior.DramatisPersonaId);
            var hasGoldUpkeep = persona is { FeeKind: DramatisPersonaHireFeeKind.Gold, Upkeep: not null };
            var hasWyrdstoneUpkeep = persona?.FeeKind == DramatisPersonaHireFeeKind.Wyrdstone;
            if (persona is null || !(hasGoldUpkeep || hasWyrdstoneUpkeep)) continue;
            DramatisPersonaUpkeepEntries.Add(new DramatisPersonaUpkeepEntry(row.Warrior, persona, payLabel, dismissLabel));
        }
    }

    private bool ValidateDramatisPersonaeStep()
    {
        var valid = true;
        foreach (var entry in DramatisPersonaUpkeepEntries)
            valid &= CheckRoll(entry.WillPay is null, () => entry.ChoiceError = Loc["EndOfGameRollRequired"]);

        return valid;
    }
}
