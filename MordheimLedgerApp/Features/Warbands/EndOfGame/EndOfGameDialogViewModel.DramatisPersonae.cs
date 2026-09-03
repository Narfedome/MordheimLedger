using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Dramatis Personae" (StepKind.DramatisPersonae) - deux sujets indépendants. (1) Règle
/// la solde de chaque Dramatis Persona à frais récurrents déjà activement engagé : en or (Johann/Veskit/
/// Marianna) OU en pierre magique (Nicodemus, 2026-09-01 - "must be paid a wyrdstone shard... after every
/// battle he fights") - PAS Bertha/None. (2) "C'est l'heure de payer !" (2026-09-01, retour utilisateur) :
/// pour une bande qui possède DÉJÀ Ulli &amp; Marquand, le montant qu'elle a éventuellement dû payer pour
/// les garder après une tentative de corruption adverse ("A Fistful of Crowns" - voir l'exemple Steiner/
/// Albrecht du livre : le camp qui GARDE le contrôle paie sa propre contre-offre). Distinct de la case
/// "Corruption de Marquand Volker &amp; Ulli Leitpold" de l'étape Prisonniers (EndOfGameDialogViewModel.
/// Captives.cs), qui couvre le cas symétrique - une bande qui NE les possède PAS et les a débauchés
/// ailleurs. Les deux sont mutuellement exclusives pour une même bande (elle les possède ou non), donc
/// partagent sans risque le même sous-système "Où est l'Argent ?" (WantsEquipmentSeizure/WantsDuel/
/// WonDuel/PairDuelRoll, déclaré dans Captives.cs - Céder du matériel/Duel déterminé automatiquement
/// depuis le 2026-09-02, plus de choix manuel du joueur).
///
/// Entièrement absente du wizard si ni (1) ni (2) n'ont quoi que ce soit à montrer.</summary>
public partial class EndOfGameDialogViewModel
{
    /// <summary>Catalogue complet (non filtré) - permet de résoudre le profil (FeeKind/Upkeep/Name,
    /// jamais stockés sur Warrior lui-même) d'un Dramatis Persona déjà engagé, même principe que
    /// _hiredSwordCatalog (EndOfGameDialogViewModel.HiredSwords.cs). Aussi consommé par
    /// EndOfGameDialogViewModel.Captives.cs (Une Poignée d'Or), même catalogue.</summary>
    private readonly List<DramatisPersona> _dramatisPersonaCatalog;

    public ObservableCollection<DramatisPersonaUpkeepEntry> DramatisPersonaUpkeepEntries { get; } = new();

    public bool HasDramatisPersonaeUpkeep => DramatisPersonaUpkeepEntries.Count > 0;

    /// <summary>Peuplée une seule fois à la construction du dialog (voir EndOfGameDialogViewModel ctor) -
    /// mêmes vrais guerriers déjà recrutés que BuildHiredSwordUpkeepEntries, filtrés à FeeKind.Gold avec
    /// un Upkeep renseigné, OU FeeKind.Wyrdstone (Bertha/Ulli &amp; Marquand n'apparaissent jamais ici -
    /// leur propre paiement, "Où est l'Argent ?", est à part, voir plus bas).</summary>
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

        // Céder du matériel / Duel avec le meneur : déterminé automatiquement depuis le 2026-09-02 (voir
        // WantsEquipmentSeizure/WantsDuel, Captives.cs), plus rien à valider ici - le duel (WonDuel/
        // PairDuelRoll) a sa propre étape/validation depuis le 2026-09-01 (StepKind.PairDuel, voir
        // EndOfGameDialogViewModel.PairDuel.cs).
        return valid;
    }

    // --- "C'est l'heure de payer !" (2026-09-01) - bande qui possède DÉJÀ Ulli & Marquand ------------
    // Symétrique de "Corruption de Marquand Volker & Ulli Leitpold" (Captives.cs) : là-bas, une bande qui
    // NE les possède PAS enregistre ce qu'elle a payé pour les débaucher ; ici, la bande qui les possède
    // enregistre ce qu'elle a éventuellement dû payer pour les GARDER après une tentative adverse (voir
    // l'exemple Steiner/Albrecht du livre - "seul le joueur qui obtient OU GARDE le contrôle paie").
    // Champ libre, jamais une case à cocher : 0/vide = aucune tentative cette partie (rien à payer), un
    // montant renseigné = une tentative a eu lieu et a été repoussée à ce prix.

    private DramatisPersona? _ownedPairPersona;

    public bool ShowPairRetentionOption { get; private set; }

    /// <summary>Nom complet de la paire, même construction que Captives.cs's PairCorruptionLabel.</summary>
    public string PairRetentionLabel => _ownedPairPersona is { } p
        ? p.PairedWithDramatisPersona is { } partner ? $"{p.Name} & {partner.Name}" : p.Name
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPairRetentionUnaffordable))]
    [NotifyPropertyChangedFor(nameof(WantsEquipmentSeizure))]
    [NotifyPropertyChangedFor(nameof(WantsDuel))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentItems))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentTotalValue))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentTotalDisplay))]
    [NotifyPropertyChangedFor(nameof(DramatisPersonaeBaselineTreasuryDisplay))]
    private string pairRetentionAmount = string.Empty;

    [ObservableProperty]
    private string? pairRetentionError;

    partial void OnPairRetentionAmountChanged(string value)
    {
        PairRetentionError = null;
        SyncPairDuelRoll();
    }

    /// <summary>Repéré à la construction (voir EndOfGameDialogViewModel ctor) - true si CETTE bande a
    /// déjà Marquand ou Ulli dans son roster actif, peu importe laquelle des deux fiches (voir
    /// Captives.cs's BuildPairCorruptionOption pour le cas symétrique/le même calcul "pairIds").</summary>
    private void BuildPairRetentionOption()
    {
        var pairIds = _dramatisPersonaCatalog.Where(p => p.FeeKind == DramatisPersonaHireFeeKind.Pair).Select(p => p.Id).ToHashSet();
        // Exclut un couple déjà basculé "hostile" (2026-09-01, retour utilisateur - "on l'applique aussi
        // dans l'entretien si on les a recruté et qu'ils sont non hostile") : hostile veut dire corrompu
        // CETTE bataille, donc quitte de toute façon à la Fin de Partie (ApplyWandererDeparturesAsync,
        // Vagabond) sans paiement de son côté - "payer pour les GARDER" n'a plus de sens dans ce cas,
        // seul un couple resté loyal (ou jamais approché) peut avoir coûté une contre-offre.
        var ownedRow = WarriorRows.FirstOrDefault(r => r.Warrior.IsDramatisPersona && r.Warrior.DramatisPersonaId is { } id
            && pairIds.Contains(id) && !r.Warrior.IsHostileThisBattle);
        if (ownedRow is null)
        {
            ShowPairRetentionOption = false;
            return;
        }

        _ownedPairPersona = _dramatisPersonaCatalog.FirstOrDefault(p => p.Id == ownedRow.Warrior.DramatisPersonaId);
        ShowPairRetentionOption = _ownedPairPersona is not null;
    }

    /// <summary>"C'est l'heure de payer !" - true si le montant saisi dépasserait le solde prévisionnel
    /// (même DramatisPersonaeBaselineTreasury que le cas symétrique, Captives.cs - cette bande n'a jamais
    /// les deux entrées à la fois, aucun risque de double-compte). Pilote le même bloc "Où est l'Argent ?"
    /// que Captives.cs (WantsEquipmentSeizure/WantsDuel/WonDuel/PairDuelRoll), affiché ici à la place.</summary>
    public bool IsPairRetentionUnaffordable => int.TryParse(PairRetentionAmount, out var amount) && amount > 0 && amount > DramatisPersonaeBaselineTreasury;
}
