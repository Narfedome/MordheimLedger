using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Dramatis Personae" (StepKind.DramatisPersonae) - deux blocs indépendants. (1) Règle la
/// solde de chaque Dramatis Persona à frais récurrents déjà activement engagé : en or (Johann/Veskit/
/// Marianna) OU en pierre magique (Nicodemus, 2026-09-01 - "must be paid a wyrdstone shard... after every
/// battle he fights") - PAS Bertha/None. (2) Une Poignée d'Or (2026-09-01) : case optionnelle pour toute
/// bande qui ne possède PAS déjà Ulli &amp; Marquand (voir ShowPairCorruptionOption/BuildPairCorruptionOption
/// ci-dessous) - la bande qui les possède déjà n'a rien ici, voir Warrior.IsHostileThisBattle à la place.
/// Contrairement à l'étape "Francs-Tireurs" (EndOfGameDialogViewModel.HiredSwords.cs), ne combine PAS de
/// recrutement optionnel d'un nouveau Dramatis Persona à la même étape - ça se passe via la recherche
/// "Personnage spécial" (RareItems/RareItemPurchase), pas ici. Entièrement absente du wizard si ni (1) ni
/// (2) n'ont quoi que ce soit à montrer - voir la condition complète dans EndOfGameDialogViewModel.Steps.</summary>
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

        if (WantsToRecordPairCorruption)
            valid &= CheckRoll(!int.TryParse(PairCorruptionAmount, out var amount) || amount <= 0, () => PairCorruptionError = Loc["EndOfGameAmountRequired"]);

        if (IsPairCorruptionUnaffordable)
        {
            valid &= CheckRoll(SelectedWheresMoneyChoice is null, () => WheresMoneyChoiceError = Loc["EndOfGameRollRequired"]);

            if (WantsDuel && !WonDuel)
            {
                foreach (var sub in PairDuelRoll)
                {
                    valid &= CheckRoll(string.IsNullOrWhiteSpace(sub.InjuryResultText), () => sub.RollError = Loc["EndOfGameRollRequired"]);
                    if (sub.ShowDeepWoundSubRoll)
                        valid &= CheckRoll(!sub.HasValidDeepWoundSubRoll, () => sub.DeepWoundRollError = Loc["EndOfGameRollRequired"]);
                    if (sub.ShowCapturedChoice && sub.IsRansomed)
                        valid &= CheckRoll(!sub.HasValidRansomAmount, () => sub.CapturedChoiceError = Loc["EndOfGameRollRequired"]);
                }
            }
        }

        return valid;
    }

    // --- Une Poignée d'Or (2026-09-01) : option pour toute bande qui NE possède PAS la paire ----------
    // "pour les autres bandes ça peut être sur un autre appareil pas celle présente en local" (retour
    // utilisateur) - donc aucun lien de données avec une autre Warband, juste une case optionnelle,
    // affichée pour n'importe quelle bande qui n'a pas déjà Marquand/Ulli recrutés, qui sort le montant
    // misé de SA propre trésorerie ("seul le joueur qui obtient ou garde le contrôle paie", voir la
    // SpecialRule "A Fistful of Crowns"). Le cas où CETTE bande possède la paire et se la fait corrompre
    // n'a pas besoin de sa propre UI ici : le joueur bascule "Rendre hostile" sur la carte du guerrier
    // (WarbandDetailViewModel.ToggleHostile) et ApplyWandererDeparturesAsync produit tout seul une phrase
    // d'historique différenciée avant le départ Vagabond habituel - aucun paiement de leur côté (voir
    // Warrior.IsHostileThisBattle).
    private DramatisPersona? _pairPersona;

    public bool ShowPairCorruptionOption { get; private set; }

    /// <summary>Nom complet de la paire ("Marquand Volker &amp; Ulli Leitpold"), pas seulement _pairPersona
    /// (Marquand seul, IsHiddenFromSearchPicker exclut Ulli) - PairedWithDramatisPersona est déjà résolue
    /// par LibraryService.GetDramatisPersonaeAsync, même navigation que RareItemSearchEntry.</summary>
    public string PairCorruptionLabel => _pairPersona is { } p
        ? string.Format(Loc["EndOfGamePairCorruptionOptionFormat"], p.PairedWithDramatisPersona is { } partner ? $"{p.Name} & {partner.Name}" : p.Name)
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPairCorruptionUnaffordable))]
    private bool wantsToRecordPairCorruption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPairCorruptionUnaffordable))]
    private string pairCorruptionAmount = string.Empty;

    [ObservableProperty]
    private string? pairCorruptionError;

    partial void OnWantsToRecordPairCorruptionChanged(bool value)
    {
        if (!value)
        {
            PairCorruptionAmount = string.Empty;
            PairCorruptionError = null;
            ResetWheresMoneyChoice();
        }
    }

    partial void OnPairCorruptionAmountChanged(string value)
    {
        PairCorruptionError = null;
        if (!IsPairCorruptionUnaffordable) ResetWheresMoneyChoice();
    }

    /// <summary>Efface le choix Où est l'Argent (et le sous-jet du duel avec) dès que le montant repasse
    /// sous le solde prévisionnel ou que la case Corruption est décochée - évite qu'un choix "Duel avec le
    /// meneur" déjà fait reste actif (et son sous-jet appliqué) une fois le manque comblé autrement.</summary>
    private void ResetWheresMoneyChoice()
    {
        SelectedWheresMoneyChoice = null;
        WonDuel = false;
        PairDuelRoll.Clear();
        OnPropertyChanged(nameof(HasPairDuelRoll));
    }

    /// <summary>Un seul représentant "visible" du duo (Marquand - IsHiddenFromSearchPicker exclut Ulli
    /// même ici, même raison qu'au picker de recherche : une seule ligne suffit) pour le libellé de la
    /// case ; ShowPairCorruptionOption regarde les DEUX ids (FeeKind.Pair) pour savoir si cette bande a
    /// déjà la paire, peu importe laquelle des deux fiches elle porte.</summary>
    private void BuildPairCorruptionOption()
    {
        _pairPersona = _dramatisPersonaCatalog.FirstOrDefault(p => p.FeeKind == DramatisPersonaHireFeeKind.Pair && !p.IsHiddenFromSearchPicker);
        if (_pairPersona is null)
        {
            ShowPairCorruptionOption = false;
            return;
        }

        var pairIds = _dramatisPersonaCatalog.Where(p => p.FeeKind == DramatisPersonaHireFeeKind.Pair).Select(p => p.Id).ToHashSet();
        var alreadyOwned = WarriorRows.Any(r => r.Warrior.IsDramatisPersona && r.Warrior.DramatisPersonaId is { } id && pairIds.Contains(id));
        ShowPairCorruptionOption = !alreadyOwned;

        _equipmentSeizureLabel = Loc["EndOfGameWheresMoneyEquipmentChoice"];
        _duelLabel = Loc["EndOfGameWheresMoneyDuelChoice"];
        WheresMoneyChoiceLabels.Add(_equipmentSeizureLabel);
        WheresMoneyChoiceLabels.Add(_duelLabel);
    }

    // --- Où est l'Argent ? (2026-09-01) : repli si le montant de PairCorruptionAmount dépasserait ce que
    // la bande peut payer - "C'est l'heure de payer ! ... Si c'est le cas, Ulli et Marquand se payeront en
    // armes et équipement... Si c'est impossible, ils se vengeront sur le chef de la bande". ------------

    /// <summary>Solde prévisionnel à cette étape - même principe que RareItemBaselineTreasury/
    /// HiredSwordTreasuryAfter (gold déjà connu plus tôt dans CETTE MÊME séquence : trésorerie restante
    /// après Achat d'Objets rares), moins les soldes en or déjà cochées "Payer" à cette étape (Johann/
    /// Veskit/Marianna) - Nicodemus (Wyrdstone) n'entre pas dans ce calcul, devise séparée qui ne dispute
    /// jamais le même budget que la corruption (payée en or/pierre magique VENDUE, jamais directement en
    /// pierres).</summary>
    private int DramatisPersonaeBaselineTreasury => RareItemPurchaseRemainingTreasury
        - DramatisPersonaUpkeepEntries.Where(e => e.WillPay == true && !e.IsWyrdstoneFee).Sum(e => e.UpkeepCost);

    /// <summary>"C'est l'heure de payer !" - true si le montant de corruption saisi dépasserait ce solde
    /// prévisionnel. Pilote la visibilité du bloc Où est l'Argent.</summary>
    public bool IsPairCorruptionUnaffordable => WantsToRecordPairCorruption
        && int.TryParse(PairCorruptionAmount, out var amount) && amount > DramatisPersonaeBaselineTreasury;

    public List<string> WheresMoneyChoiceLabels { get; } = new();
    private string? _equipmentSeizureLabel;
    private string? _duelLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WantsEquipmentSeizure))]
    [NotifyPropertyChangedFor(nameof(WantsDuel))]
    private string? selectedWheresMoneyChoice;

    /// <summary>Céder du matériel : pas de sélecteur de valeur automatique (aucun mécanisme de "somme de
    /// valeur" ailleurs dans l'app pour une vente partielle) - juste un rappel textuel invitant à retirer
    /// manuellement du matériel équivalent depuis l'écran Inventaire, une fois cette Fin de Partie
    /// enregistrée. Rien à appliquer côté WarbandDetailViewModel.EndOfGame pour ce choix.</summary>
    public bool WantsEquipmentSeizure => SelectedWheresMoneyChoice == _equipmentSeizureLabel;

    public bool WantsDuel => SelectedWheresMoneyChoice == _duelLabel;

    [ObservableProperty]
    private string? wheresMoneyChoiceError;

    partial void OnSelectedWheresMoneyChoiceChanged(string? value)
    {
        WheresMoneyChoiceError = null;
        if (value == _duelLabel)
        {
            if (PairDuelRoll.Count == 0 && !WonDuel) PopulatePairDuelRoll();
        }
        else if (PairDuelRoll.Count > 0)
        {
            PairDuelRoll.Clear();
            OnPropertyChanged(nameof(HasPairDuelRoll));
        }
    }

    /// <summary>Même principe que WonPitFight (Vendu aux Fosses, EndOfGameDialogViewModel.Injury.cs) mais
    /// appliqué au chef de bande de CETTE session plutôt qu'à un guerrier hors de combat - décoché par
    /// défaut (défaite), coché = rien de plus à faire ("Ulli et Marquand" échouent à se venger).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPairDuelRoll))]
    private bool wonDuel;

    partial void OnWonDuelChanged(bool value)
    {
        if (value)
        {
            PairDuelRoll.Clear();
            OnPropertyChanged(nameof(HasPairDuelRoll));
        }
        else if (WantsDuel && PairDuelRoll.Count == 0)
        {
            PopulatePairDuelRoll();
        }
    }

    /// <summary>Défaite du duel : un unique sous-jet D66 sur la table des Blessures Graves Héros (le
    /// meneur est toujours un Héros) - réutilise InjurySubRollEntry tel quel, même patron que
    /// WarriorOutcomeRow.SoldToPitsRerollRoll. Collection à un seul élément pour réutiliser le même
    /// DataTemplate/les mêmes commandes AutoRoll que les autres sous-jets du wizard.</summary>
    public ObservableCollection<InjurySubRollEntry> PairDuelRoll { get; } = new();
    public bool HasPairDuelRoll => PairDuelRoll.Count > 0;

    private void PopulatePairDuelRoll()
    {
        var entry = new InjurySubRollEntry(1, 1, isHero: true, labelKey: "EndOfGamePairDuelRollLabel");
        entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasPairDuelRoll));
        PairDuelRoll.Add(entry);
        OnPropertyChanged(nameof(HasPairDuelRoll));
    }
}
