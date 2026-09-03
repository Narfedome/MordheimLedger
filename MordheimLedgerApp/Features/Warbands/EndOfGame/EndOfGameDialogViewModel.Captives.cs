using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Prisonniers &amp; Corruption" (renommée le 2026-09-01, retour utilisateur - était
/// "Prisonniers ennemis") - une seule étape pour toute la bande (voir la doc de EndOfGameDialogViewModel.
/// Steps), demandée par l'utilisateur (2026-08-27) en repensant Capturé (61) : le capteur d'un DE NOS
/// guerriers est une bande adverse non modélisée, mais l'inverse (nous capturons des héros adverses) ne
/// dépend que de NOTRE bande, dont le type est bien connu - d'où le picker de destin filtré par
/// IsUndeadWarband/IsPossessedWarband plutôt qu'une simplification symétrique.
///
/// "Corruption de Marquand Volker &amp; Ulli Leitpold" ("Une Poignée d'Or"/"Où est l'Argent ?", Ulli &amp;
/// Marquand) vit aussi ici depuis le 2026-09-01 - moitié d'un couple de cas symétriques : CETTE case
/// couvre une bande qui NE possède PAS la paire et paie pour la débaucher ; l'autre moitié
/// (ShowPairRetentionOption, "C'est l'heure de payer !" - une bande qui la possède DÉJÀ et paie pour la
/// GARDER) vit dans EndOfGameDialogViewModel.DramatisPersonae.cs, à l'étape Dramatis Personae - retour
/// utilisateur explicite ("dans la section des frais d'entretien, on doit les avoir"). Les deux partagent
/// sans risque le même sous-système "Où est l'Argent ?" (WantsEquipmentSeizure/WantsDuel/WonDuel/
/// PairDuelRoll, déclaré ici) puisqu'une bande ne peut jamais déclencher les deux à la fois.
///
/// **Céder du matériel vs Duel : déterminé automatiquement, plus de choix manuel (2026-09-02, retour
/// utilisateur)** - `WantsEquipmentSeizure`/`WantsDuel` comparent la valeur totale du stash de bande
/// (`WarbandInventoryTotalValue`) au montant dû : le stash suffit → céder du matériel (jamais de duel
/// dans ce cas) ; le stash ne suffit pas → duel avec le meneur (seule alternative qui reste). Un ancien
/// Picker (WheresMoneyChoiceLabels/SelectedWheresMoneyChoice) laissait le joueur choisir librement entre
/// les deux - retiré, la règle ne laisse pas ce choix ("s'ils ne peuvent pas payer... Où est l'Argent" est
/// un repli automatique, pas une option).</summary>
public partial class EndOfGameDialogViewModel
{
    private bool ValidateCaptivesStep()
    {
        var valid = true;

        if (HasCapturedEnemies)
        {
            foreach (var entry in CapturedEnemies)
            {
                valid &= CheckRoll(entry.SelectedFate is null, () => entry.FateError = Loc["EndOfGameRollRequired"]);
                if (entry.ShowGoldAmount)
                    valid &= CheckRoll(!entry.HasValidGoldAmount, () => entry.FateError = Loc["EndOfGameRollRequired"]);
            }
        }

        if (WantsToRecordPairCorruption)
            valid &= CheckRoll(!int.TryParse(PairCorruptionAmount, out var amount) || amount <= 0, () => PairCorruptionError = Loc["EndOfGameAmountRequired"]);

        // Céder du matériel / Duel avec le meneur : déterminé automatiquement depuis le 2026-09-02 (voir
        // WantsEquipmentSeizure/WantsDuel), plus rien à valider ici - le duel (WonDuel/PairDuelRoll) a sa
        // propre étape/validation depuis le 2026-09-01 (StepKind.PairDuel, voir
        // EndOfGameDialogViewModel.PairDuel.cs).
        return valid;
    }

    [RelayCommand]
    private void IncrementCapturedEnemyCount() => CapturedEnemyCount = Math.Min(6, CapturedEnemyCount + 1);

    [RelayCommand]
    private void DecrementCapturedEnemyCount() => CapturedEnemyCount = Math.Max(1, CapturedEnemyCount - 1);

    // Formule du livre pour "Vendu aux esclavagistes" (1D6x5 CO) - le champ reste modifiable ensuite pour
    // un jet physique, même convention que le reste du wizard.
    [RelayCommand]
    private void AutoRollSoldToSlavers(CapturedEnemyEntry entry) => entry.GoldAmount = (Random.Shared.Next(1, 7) * 5).ToString();

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
    private bool _pairAlreadyOwnedInRoster;

    /// <summary>Calculée (jamais figée à la construction, contrairement à avant le 2026-09-02) : reste
    /// fausse si la bande possède déjà la paire dans son roster (comme avant), MAIS aussi si le joueur est
    /// en train de l'engager normalement CETTE Fin de Partie via la recherche "Personnage spécial" (voir
    /// IsPairBeingRecruitedThisWizard) - retour utilisateur explicite : "si on a engager la paire, on ne
    /// doit pas voir la case de corruption". L'étape Prisonniers (qui affiche cette case) précède
    /// l'étape Achat/Recrutement dans l'ordre du wizard (Steps), donc cette décision n'est souvent connue
    /// qu'APRÈS que le joueur ait déjà vu cette étape - doit rester réévaluée en direct (notifiée depuis
    /// le handler WantsToRecruit de RareItemSearchEntries, voir EndOfGameDialogViewModel.cs) plutôt que
    /// figée une fois pour toutes.</summary>
    public bool ShowPairCorruptionOption => _pairPersona is not null && !_pairAlreadyOwnedInRoster && !IsPairBeingRecruitedThisWizard;

    /// <summary>Voir la doc de ShowPairCorruptionOption - vrai dès qu'une recherche "Personnage spécial"
    /// visant Marquand/Ulli (FeeKind.Pair) a RÉUSSI (IsRecruited = IsCharacterFound && WantsToRecruit) -
    /// pas seulement WantsToRecruit seul, qui vaut déjà true par défaut dès la création de l'entrée
    /// (avant même le jet de recherche, voir RareItemSearchEntry.wantsToRecruit) - une recherche ratée ne
    /// doit pas cacher la case Corruption.</summary>
    private bool IsPairBeingRecruitedThisWizard => RareItemSearchEntries.Any(e => e.IsRecruited && e.SelectedCharacter?.FeeKind == DramatisPersonaHireFeeKind.Pair);

    /// <summary>Nom complet de la paire ("Marquand Volker &amp; Ulli Leitpold"), pas seulement _pairPersona
    /// (Marquand seul, IsHiddenFromSearchPicker exclut Ulli) - PairedWithDramatisPersona est déjà résolue
    /// par LibraryService.GetDramatisPersonaeAsync, même navigation que RareItemSearchEntry.</summary>
    public string PairCorruptionLabel => _pairPersona is { } p
        ? string.Format(Loc["EndOfGamePairCorruptionOptionFormat"], p.PairedWithDramatisPersona is { } partner ? $"{p.Name} & {partner.Name}" : p.Name)
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPairCorruptionUnaffordable))]
    [NotifyPropertyChangedFor(nameof(WantsEquipmentSeizure))]
    [NotifyPropertyChangedFor(nameof(WantsDuel))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentItems))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentTotalValue))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentTotalDisplay))]
    private bool wantsToRecordPairCorruption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPairCorruptionUnaffordable))]
    [NotifyPropertyChangedFor(nameof(WantsEquipmentSeizure))]
    [NotifyPropertyChangedFor(nameof(WantsDuel))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentItems))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentTotalValue))]
    [NotifyPropertyChangedFor(nameof(SeizedEquipmentTotalDisplay))]
    [NotifyPropertyChangedFor(nameof(DramatisPersonaeBaselineTreasuryDisplay))]
    private string pairCorruptionAmount = string.Empty;

    [ObservableProperty]
    private string? pairCorruptionError;

    partial void OnWantsToRecordPairCorruptionChanged(bool value)
    {
        if (!value)
        {
            PairCorruptionAmount = string.Empty;
            PairCorruptionError = null;
        }
        SyncPairDuelRoll();
    }

    partial void OnPairCorruptionAmountChanged(string value)
    {
        PairCorruptionError = null;
        SyncPairDuelRoll();
    }

    /// <summary>Peuple/efface le sous-jet du duel dès que WantsDuel change de valeur (2026-09-02, retour
    /// utilisateur - détermination automatique entre Céder du matériel et Duel, plus de choix manuel du
    /// joueur) - appelé depuis les handlers de changement de PairCorruptionAmount/PairRetentionAmount/
    /// WantsToRecordPairCorruption, seuls déclencheurs possibles de WantsDuel (propriété calculée, pas un
    /// ObservableProperty - impossible d'y accrocher directement un partial void OnXxxChanged).</summary>
    private void SyncPairDuelRoll()
    {
        if (WantsDuel)
        {
            if (PairDuelRoll.Count == 0 && !WonDuel) PopulatePairDuelRoll();
        }
        else
        {
            WonDuel = false;
            if (PairDuelRoll.Count > 0)
            {
                PairDuelRoll.Clear();
                OnPropertyChanged(nameof(HasPairDuelRoll));
            }
        }
    }

    /// <summary>Un seul représentant "visible" du duo (Marquand - IsHiddenFromSearchPicker exclut Ulli
    /// même ici, même raison qu'au picker de recherche : une seule ligne suffit) pour le libellé de la
    /// case ; ShowPairCorruptionOption regarde les DEUX ids (FeeKind.Pair) pour savoir si cette bande a
    /// déjà la paire, peu importe laquelle des deux fiches elle porte.</summary>
    private void BuildPairCorruptionOption()
    {
        _pairPersona = _dramatisPersonaCatalog.FirstOrDefault(p => p.FeeKind == DramatisPersonaHireFeeKind.Pair && !p.IsHiddenFromSearchPicker);
        if (_pairPersona is null) return;

        var pairIds = _dramatisPersonaCatalog.Where(p => p.FeeKind == DramatisPersonaHireFeeKind.Pair).Select(p => p.Id).ToHashSet();
        _pairAlreadyOwnedInRoster = WarriorRows.Any(r => r.Warrior.IsDramatisPersona && r.Warrior.DramatisPersonaId is { } id && pairIds.Contains(id));
    }

    // --- Où est l'Argent ? (2026-09-01) : repli si le montant de PairCorruptionAmount dépasserait ce que
    // la bande peut payer - "C'est l'heure de payer ! ... Si c'est le cas, Ulli et Marquand se payeront en
    // armes et équipement... Si c'est impossible, ils se vengeront sur le chef de la bande". ------------

    /// <summary>Solde prévisionnel à cette étape - même principe que RareItemBaselineTreasury/
    /// HiredSwordTreasuryAfter (gold déjà connu plus tôt dans CETTE MÊME séquence : trésorerie restante
    /// après Achat d'Objets rares), moins les soldes en or déjà cochées "Payer" à l'étape Dramatis
    /// Personae (Johann/Veskit/Marianna) - Nicodemus (Wyrdstone) n'entre pas dans ce calcul, devise
    /// séparée qui ne dispute jamais le même budget que la corruption (payée en or/pierre magique VENDUE,
    /// jamais directement en pierres).</summary>
    private int DramatisPersonaeBaselineTreasury => RareItemPurchaseRemainingTreasury
        - DramatisPersonaUpkeepEntries.Where(e => e.WillPay == true && !e.IsWyrdstoneFee).Sum(e => e.UpkeepCost);

    /// <summary>Affichage direct de DramatisPersonaeBaselineTreasury (2026-09-02, retour utilisateur - "peu
    /// importe la somme que je mets, je n'ai pas le choix de céder du matériel/combattre le meneur") :
    /// avant ce correctif, rien à l'écran ne montrait CE solde précis (celui qui sert réellement de seuil
    /// ici), seulement la trésorerie persistée de la bande (fiche, avant cette Fin de Partie) - un joueur
    /// dont la bande a gagné de l'or à l'Exploration/à la Vente de pierres magiques CETTE partie (pas
    /// encore enregistré) pouvait saisir un montant qui semblait dépasser sa trésorerie connue sans jamais
    /// déclencher le repli, sans aucun moyen de comprendre pourquoi.</summary>
    public string DramatisPersonaeBaselineTreasuryDisplay => string.Format(Loc["EndOfGameDramatisPersonaeBaselineTreasuryFormat"], DramatisPersonaeBaselineTreasury);

    /// <summary>"C'est l'heure de payer !" - true si le montant de corruption saisi dépasserait ce solde
    /// prévisionnel. Pilote la visibilité du bloc Où est l'Argent.</summary>
    public bool IsPairCorruptionUnaffordable => WantsToRecordPairCorruption
        && int.TryParse(PairCorruptionAmount, out var amount) && amount > DramatisPersonaeBaselineTreasury;

    /// <summary>Valeur totale du stash de bande, toutes lignes confondues (2026-09-02, retour utilisateur
    /// - "on détermine automatiquement si on a le duel ou non, pas de choix dans le picker") : seule
    /// mesure qui tranche désormais entre Céder du matériel et Duel avec le meneur, remplace l'ancien
    /// choix manuel (WheresMoneyChoiceLabels/SelectedWheresMoneyChoice, retiré).</summary>
    private int WarbandInventoryTotalValue => _warbandInventory.Sum(w => w.SellValue);

    /// <summary>Montant réellement dû à cette étape (corruption OU rétention, jamais les deux - voir la
    /// doc de classe) - 0 si aucun des deux blocs Où est l'Argent n'est actif.</summary>
    private int PairPaymentTargetAmount => IsPairCorruptionUnaffordable && int.TryParse(PairCorruptionAmount, out var corruptionAmount)
        ? corruptionAmount
        : IsPairRetentionUnaffordable && int.TryParse(PairRetentionAmount, out var retentionAmount) ? retentionAmount : 0;

    /// <summary>Automatique (2026-09-02) : le stash de la bande, à lui seul, couvre le montant dû -> elle
    /// cède du matériel plutôt que de risquer son meneur en duel. Faux si aucun paiement n'est dû
    /// (PairPaymentTargetAmount == 0) ou si le stash ne suffit pas (voir WantsDuel, l'autre branche).</summary>
    public bool WantsEquipmentSeizure => PairPaymentTargetAmount > 0 && WarbandInventoryTotalValue >= PairPaymentTargetAmount;

    /// <summary>Automatique (2026-09-02) : le stash ne suffit pas à couvrir le montant dû -> seule
    /// alternative qui reste, un duel avec le meneur de bande (voir WantsEquipmentSeizure, l'autre
    /// branche - les deux sont mutuellement exclusives et ne couvrent jamais PairPaymentTargetAmount == 0
    /// à la fois).</summary>
    public bool WantsDuel => PairPaymentTargetAmount > 0 && WarbandInventoryTotalValue < PairPaymentTargetAmount;

    /// <summary>"Céder du matériel" : sélection automatique dans le stash de bande, du PLUS VALEUREUX
    /// objet au moins valeureux (2026-09-02, retour utilisateur - "on prend les équipements les plus
    /// valuable d'abord jusqu'à atteindre la somme", ex. une Épée en Gromril à 60 CO + 3 Épées classiques
    /// à 10 CO chacune pour une rançon de 50 CO -> on ne perd QUE l'Épée en Gromril, jamais les 3 armes
    /// bon marché en plus). Perdre un minimum d'OBJETS (même si ça dépasse un peu la somme due) plutôt
    /// qu'un minimum de VALEUR (l'ancien tri croissant, remplacé) - la boucle s'arrête dès que la cible
    /// est atteinte/dépassée. Une ligne = une pile ENTIÈRE (jamais un retrait partiel - aucun mécanisme de
    /// pile partielle ailleurs dans l'app, voir AlternativePaymentItemId's own doc). N'est en pratique
    /// appelée que quand WantsEquipmentSeizure est vrai (le stash suffit), donc n'a normalement jamais
    /// besoin de vider tout le stash sans atteindre la cible - conservé quand même en repli défensif.</summary>
    public List<WarbandEquipment> SeizedEquipmentItems
    {
        get
        {
            var target = PairPaymentTargetAmount;
            if (target <= 0) return new List<WarbandEquipment>();

            var seized = new List<WarbandEquipment>();
            var sum = 0;
            foreach (var item in _warbandInventory.OrderByDescending(w => w.SellValue))
            {
                if (sum >= target) break;
                seized.Add(item);
                sum += item.SellValue;
            }
            return seized;
        }
    }

    public int SeizedEquipmentTotalValue => SeizedEquipmentItems.Sum(w => w.SellValue);

    /// <summary>Computed here (not nested in a XAML StringFormat, invalid syntax for a {loc:Loc} value) -
    /// "Objets saisis : X CO (besoin : Y CO)" pour que le joueur voie tout de suite si la sélection
    /// automatique couvre bien la cible (elle ne le peut pas toujours - un stash trop pauvre saisit tout
    /// ce qu'il a sans jamais atteindre PairPaymentTargetAmount, voir SeizedEquipmentItems's own doc).</summary>
    public string SeizedEquipmentTotalDisplay => string.Format(Loc["EndOfGameSeizedEquipmentTotalFormat"], SeizedEquipmentTotalValue, PairPaymentTargetAmount);
}
