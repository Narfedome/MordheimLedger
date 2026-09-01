using System.Collections.ObjectModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Dramatis Personae" (StepKind.DramatisPersonae) - règle la solde de chaque Dramatis
/// Persona à frais en or déjà activement engagé (Johann/Veskit/Marianna - PAS Bertha/None, Nicodemus/
/// Wyrdstone, Ulli &amp; Marquand/Pair, tous les trois hors périmètre de cette passe, voir
/// DRAMATIS_PERSONAE_STATUS.md). Contrairement à l'étape "Francs-Tireurs" (EndOfGameDialogViewModel.
/// HiredSwords.cs), ne combine PAS de recrutement optionnel d'un nouveau à la même étape - un Dramatis
/// Persona se recrute déjà via la recherche "Personnage spécial" (RareItems/RareItemPurchase), pas ici.
/// Entièrement absente du wizard si HasDramatisPersonaeUpkeep est faux.</summary>
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
    /// un Upkeep renseigné (Bertha/Nicodemus/Ulli &amp; Marquand n'apparaissent jamais ici).</summary>
    private void BuildDramatisPersonaUpkeepEntries()
    {
        var payLabel = Loc["EndOfGameHiredSwordPayAction"];
        var dismissLabel = Loc["WarbandsDismissHiredSwordAction"];
        foreach (var row in WarriorRows.Where(r => r.Warrior.IsDramatisPersona))
        {
            var persona = _dramatisPersonaCatalog.FirstOrDefault(p => p.Id == row.Warrior.DramatisPersonaId);
            if (persona is not { FeeKind: DramatisPersonaHireFeeKind.Gold, Upkeep: not null }) continue;
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
