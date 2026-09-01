using System.Collections.ObjectModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Dramatis Personae" (StepKind.DramatisPersonae) - règle la solde de chaque Dramatis
/// Persona à frais récurrents déjà activement engagé : en or (Johann/Veskit/Marianna) OU en pierre
/// magique (Nicodemus, 2026-09-01 - "must be paid a wyrdstone shard... after every battle he fights") -
/// PAS Bertha/None ni Ulli &amp; Marquand/Pair, tous deux hors périmètre, voir DRAMATIS_PERSONAE_STATUS.md.
/// Contrairement à l'étape "Francs-Tireurs" (EndOfGameDialogViewModel.HiredSwords.cs), ne combine PAS de
/// recrutement optionnel d'un nouveau à la même étape - un Dramatis Persona se recrute déjà via la
/// recherche "Personnage spécial" (RareItems/RareItemPurchase), pas ici. Entièrement absente du wizard si
/// HasDramatisPersonaeUpkeep est faux.</summary>
public partial class EndOfGameDialogViewModel
{
    /// <summary>Catalogue complet (non filtré) - permet de résoudre le profil (FeeKind/Upkeep/Name,
    /// jamais stockés sur Warrior lui-même) d'un Dramatis Persona déjà engagé, même principe que
    /// _hiredSwordCatalog (EndOfGameDialogViewModel.HiredSwords.cs).</summary>
    private readonly List<DramatisPersona> _dramatisPersonaCatalog;

    public ObservableCollection<DramatisPersonaUpkeepEntry> DramatisPersonaUpkeepEntries { get; } = new();

    public bool HasDramatisPersonaeUpkeep => DramatisPersonaUpkeepEntries.Count > 0;

    /// <summary>Peuplée une seule fois à la construction du dialog (voir EndOfGameDialogViewModel ctor) -
    /// mêmes vrais guerriers déjà recrutés que BuildHiredSwordUpkeepEntries, filtrés à FeeKind.Gold avec
    /// un Upkeep renseigné, OU FeeKind.Wyrdstone (Bertha/Ulli &amp; Marquand n'apparaissent jamais ici).</summary>
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
