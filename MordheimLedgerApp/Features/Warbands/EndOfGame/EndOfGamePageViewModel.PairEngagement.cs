using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Une Poignée d'Or" (StepKind.PairEngagement, 2026-09-04, retour utilisateur - "il faut
/// la mettre au moment du paiement de recrutement... je me tate sur l'écran de corruption à le faire
/// après dans un autre step pour éviter de mélanger et de surcharger") - regroupe les deux moitiés
/// mutuellement exclusives d'"A Fistful of Crowns"/"Où est l'Argent ?" pour Marquand &amp; Ulli, chacune
/// affichée seule selon le cas : (1) "Corruption de Marquand Volker &amp; Ulli Leitpold" - bande qui NE
/// possède PAS la paire, débauchée d'ailleurs (montant misé, débité de SA trésorerie). (2) "C'est l'heure
/// de payer !" - bande qui la possède DÉJÀ, ce qu'elle a payé pour la GARDER après une tentative
/// adverse (voir l'exemple Steiner/Albrecht du livre - "seul le camp qui obtient OU GARDE le contrôle
/// paie"). Placée juste après l'étape Dramatis Personae (soldes Johann/Veskit/Marianna/Nicodemus) plutôt
/// que d'y être mélangée - la Corruption s'apparente à un "hire" (nouvelle acquisition, irait
/// naturellement du côté Recrutement) et la Rétention à un "upkeep" (coût récurrent pour garder qui est
/// déjà là) : comme les deux sont mutuellement exclusives pour une même bande et partagent tout leur état
/// ci-dessous, une seule étape à un seul emplacement fixe plutôt que scindée à nouveau en deux endroits.
///
/// Ancienne organisation (avant ce découpage) : la Corruption vivait sur l'étape Prisonniers
/// (EndOfGamePageViewModel.Captives.cs) et la Rétention sur l'étape Dramatis Personae
/// (EndOfGamePageViewModel.DramatisPersonae.cs) - retour utilisateur explicite du 2026-09-01 ("dans la
/// section des frais d'entretien, on doit les avoir") puis reconsidéré le 2026-09-04 pour les regrouper
/// ici. Le sous-système partagé "Où est l'Argent ?" (WantsEquipmentSeizure/WantsDuel - WonDuel vit dans
/// PairDuel.cs, sa propre étape) reste inchangé, seulement déplacé dans ce fichier.
///
/// **Céder du matériel vs Duel : déterminé automatiquement, plus de choix manuel (2026-09-02, retour
/// utilisateur)** - `WantsEquipmentSeizure`/`WantsDuel` comparent la valeur totale de la réserve de bande
/// (`WarbandInventoryTotalValue`) au montant dû : la réserve suffit → céder du matériel (jamais de duel
/// dans ce cas) ; la réserve ne suffit pas → duel avec le meneur (seule alternative qui reste).
///
/// **Générique depuis le 2026-09-04 (retour utilisateur) : piloté par la SpecialRule "A Fistful of
/// Crowns"/"Une Poignée d'Or", pas par FeeKind.Pair.** `_fistfulOfCrownsPersonaIds` (résolue via
/// `_specialRulesByEnglishName`, même idiome que le reste du wizard pour identifier une règle précise
/// indépendamment de la langue courante) - n'importe quel futur Dramatis Persona, seul ou en paire,
/// portant cette même SpecialRule hérite automatiquement de tout ce mécanisme (Corruption, Rétention,
/// Où est l'Argent, et - voir EndOfGamePageViewModel.PairDuel.cs - le Duel lui-même). FeeKind.Pair
/// reste un sujet distinct : le mécanisme de recrutement COMBINÉ propre à Ulli &amp; Marquand (30 CO à
/// deux, un seul HireCost jamais doublé) - une SpecialRule "A Fistful of Crowns" solo sur un futur
/// personnage n'impliquerait pas forcément FeeKind.Pair.</summary>
public partial class EndOfGamePageViewModel
{
    private bool ValidatePairEngagementStep()
    {
        var valid = true;
        if (WantsToRecordPairCorruption)
            valid &= CheckRoll(!int.TryParse(PairCorruptionAmount, out var amount) || amount <= 0, () => PairCorruptionError = Loc["EndOfGameAmountRequired"]);

        // Céder du matériel / Duel avec le meneur : déterminé automatiquement (voir WantsEquipmentSeizure/
        // WantsDuel), plus rien à valider ici - le duel (WonDuel) a sa propre étape (StepKind.PairDuel,
        // voir EndOfGamePageViewModel.PairDuel.cs), sans rien à valider non plus (défaite = mort
        // automatique, pas de jet). PairRetentionAmount reste un champ libre optionnel (0/vide = aucune
        // tentative cette partie), rien à exiger dessus.
        return valid;
    }

    // --- Corruption : bande qui NE possède PAS un Dramatis Persona portant "A Fistful of Crowns" --------
    // "pour les autres bandes ça peut être sur un autre appareil pas celle présente en local" (retour
    // utilisateur) - donc aucun lien de données avec une autre Warband, juste une case optionnelle,
    // affichée pour n'importe quelle bande qui n'a pas déjà ce/ces personnage(s) recrutés, qui sort le
    // montant misé de SA propre trésorerie ("seul le joueur qui obtient ou garde le contrôle paie"). Le
    // cas où CETTE bande le/les possède et se le/les fait corrompre n'a pas besoin de sa propre UI ici :
    // le joueur bascule "Rendre hostile" sur la carte du guerrier (WarbandDetailViewModel.ToggleHostile)
    // et ApplyWandererDeparturesAsync produit tout seul une phrase d'historique différenciée avant le
    // départ Vagabond habituel - aucun paiement de leur côté (voir Warrior.IsHostileThisBattle).
    private DramatisPersona? _pairPersona;
    private bool _pairAlreadyOwnedInRoster;

    /// <summary>Ids du catalogue portant la SpecialRule "A Fistful of Crowns"/"Une Poignée d'Or" - c'est
    /// CETTE règle, plutôt que FeeKind.Pair, qui déclenche tout le mécanisme Corruption/Rétention/Où est
    /// l'Argent ci-dessous (2026-09-04, retour utilisateur : "rendre le truc générique... si on a la
    /// règle Une Poignée d'Or") - n'importe quel futur Dramatis Persona (seul ou en paire, voir
    /// PairedWithDramatisPersonaId) portant cette même SpecialRule hérite automatiquement de tout ce
    /// fichier sans plus dépendre de FeeKind.Pair, qui reste par ailleurs le mécanisme de recrutement
    /// combiné (Ulli & Marquand, 30 CO à deux) - un sujet distinct de la SpecialRule elle-même. Résolue
    /// une seule fois (catalogue figé à l'ouverture du wizard, comme _dramatisPersonaCatalog lui-même) -
    /// vide si la règle n'existe pas dans le catalogue courant. Limite connue, inchangée : le mécanisme
    /// reste pensé pour UN SEUL personnage/groupe "corruptible" actif par bande à la fois
    /// (FirstOrDefault ci-dessous) - si plusieurs personnages distincts portaient un jour cette règle
    /// simultanément, ce serait à généraliser en vraie collection (même schéma que
    /// DramatisPersonaUpkeepEntries).</summary>
    private HashSet<int> _fistfulOfCrownsPersonaIds = new();

    private void BuildFistfulOfCrownsPersonaIds()
    {
        var ruleId = _specialRulesByEnglishName.GetValueOrDefault("A Fistful of Crowns")?.Id;
        _fistfulOfCrownsPersonaIds = ruleId is null
            ? new HashSet<int>()
            : _dramatisPersonaCatalog.Where(p => p.SpecialRules.Any(r => r.Id == ruleId)).Select(p => p.Id).ToHashSet();
    }

    /// <summary>Calculée (jamais figée à la construction) : reste fausse si la bande possède déjà la
    /// paire dans son roster, MAIS aussi si le joueur est en train de l'engager normalement CETTE Fin de
    /// Partie via la recherche "Personnage spécial" (voir IsPairBeingRecruitedThisWizard) - retour
    /// utilisateur explicite : "si on a engager la paire, on ne doit pas voir la case de corruption".
    /// L'étape Achat/Recrutement précède cette étape-ci dans l'ordre du wizard (Steps), donc cette
    /// décision est déjà connue au moment d'afficher cette carte - reste quand même réévaluée en direct
    /// (notifiée depuis le handler WantsToRecruit de RareItemSearchEntries, voir
    /// EndOfGamePageViewModel.cs) au cas où le joueur reviendrait en arrière.</summary>
    public bool ShowPairCorruptionOption => _pairPersona is not null && !_pairAlreadyOwnedInRoster && !IsPairBeingRecruitedThisWizard;

    /// <summary>Voir la doc de ShowPairCorruptionOption - vrai dès qu'une recherche "Personnage spécial"
    /// visant un personnage portant "A Fistful of Crowns" a RÉUSSI (IsRecruited = IsCharacterFound &amp;&amp;
    /// WantsToRecruit) - pas seulement WantsToRecruit seul, qui vaut déjà true par défaut dès la création
    /// de l'entrée (avant même le jet de recherche, voir RareItemSearchEntry.wantsToRecruit) - une
    /// recherche ratée ne doit pas cacher la case Corruption.</summary>
    private bool IsPairBeingRecruitedThisWizard => RareItemSearchEntries.Any(e => e.IsRecruited && e.SelectedCharacter is { } c && _fistfulOfCrownsPersonaIds.Contains(c.Id));

    /// <summary>Nom complet ("Marquand Volker &amp; Ulli Leitpold" pour la paire, ou juste le nom seul pour
    /// un futur personnage solo portant la même règle) - pas seulement _pairPersona (le représentant
    /// "visible" du groupe, IsHiddenFromSearchPicker exclut un éventuel partenaire caché du picker de
    /// recherche) - PairedWithDramatisPersona est déjà résolue par LibraryService.GetDramatisPersonaeAsync,
    /// même navigation que RareItemSearchEntry. Null pour un personnage solo, auquel cas seul son propre
    /// nom s'affiche.</summary>
    public string PairCorruptionLabel => _pairPersona is { } p
        ? string.Format(Loc["EndOfGamePairCorruptionOptionFormat"], p.PairedWithDramatisPersona is { } partner ? $"{p.Name} & {partner.Name}" : p.Name)
        : string.Empty;

    // Les [NotifyPropertyChangedFor] individuels (IsPairCorruptionUnaffordable/WantsEquipmentSeizure/
    // WantsDuel/SeizedEquipmentItems/etc.) ont été remplacés par un seul appel à NotifyTreasuryChanged()
    // dans les handlers ci-dessous - voir sa doc (EndOfGamePageViewModel.cs) : ce montant dispute
    // désormais la MÊME trésorerie que le reste du wizard (achat d'Objets rares, Francs-Tireurs...), une
    // longue liste dupliquée par propriété devenait trop facile à laisser incomplète (voir le bug
    // IsPairDuelStep trouvé le 2026-09-03).
    [ObservableProperty]
    private bool wantsToRecordPairCorruption;

    [ObservableProperty]
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
        NotifyTreasuryChanged();
        ResetWonDuelIfNoLongerNeeded();
    }

    partial void OnPairCorruptionAmountChanged(string value)
    {
        PairCorruptionError = null;
        NotifyTreasuryChanged();
        ResetWonDuelIfNoLongerNeeded();
    }

    /// <summary>Remet WonDuel à faux dès que WantsDuel repasse à faux - évite qu'une victoire déjà cochée
    /// reste active si le manque est comblé autrement (matériel cédé, montant réduit...) avant que le
    /// joueur n'atteigne l'étape Duel. Appelée depuis les handlers de changement de PairCorruptionAmount/
    /// PairRetentionAmount/WantsToRecordPairCorruption, seuls déclencheurs possibles de WantsDuel
    /// (propriété calculée, pas un ObservableProperty - impossible d'y accrocher directement un partial
    /// void OnXxxChanged).</summary>
    private void ResetWonDuelIfNoLongerNeeded()
    {
        if (!WantsDuel) WonDuel = false;
    }

    /// <summary>Un seul représentant "visible" du groupe (pour la paire, Marquand -
    /// IsHiddenFromSearchPicker exclut Ulli même ici, même raison qu'au picker de recherche : une seule
    /// ligne suffit ; pour un futur personnage solo, lui-même directement) pour le libellé de la case ;
    /// ShowPairCorruptionOption regarde TOUS les ids portant la règle (_fistfulOfCrownsPersonaIds) pour
    /// savoir si cette bande en possède déjà un, peu importe laquelle des fiches concernées.</summary>
    private void BuildPairCorruptionOption()
    {
        BuildFistfulOfCrownsPersonaIds();
        _pairPersona = _dramatisPersonaCatalog.FirstOrDefault(p => _fistfulOfCrownsPersonaIds.Contains(p.Id) && !p.IsHiddenFromSearchPicker);
        if (_pairPersona is null) return;

        _pairAlreadyOwnedInRoster = WarriorRows.Any(r => r.Warrior.IsDramatisPersona && r.Warrior.DramatisPersonaId is { } id && _fistfulOfCrownsPersonaIds.Contains(id));
    }

    // --- "C'est l'heure de payer !" : bande qui possède DÉJÀ Ulli & Marquand ----------------------------
    // Symétrique de la Corruption ci-dessus : là-haut, une bande qui NE les possède PAS enregistre ce
    // qu'elle a payé pour les débaucher ; ici, la bande qui les possède enregistre ce qu'elle a
    // éventuellement dû payer pour les GARDER après une tentative adverse (voir l'exemple Steiner/
    // Albrecht du livre - "seul le joueur qui obtient OU GARDE le contrôle paie"). Champ libre, jamais
    // une case à cocher : 0/vide = aucune tentative cette partie (rien à payer), un montant renseigné =
    // une tentative a eu lieu et a été repoussée à ce prix.

    private DramatisPersona? _ownedPairPersona;

    public bool ShowPairRetentionOption { get; private set; }

    /// <summary>Nom complet de la paire, même construction que PairCorruptionLabel ci-dessus.</summary>
    public string PairRetentionLabel => _ownedPairPersona is { } p
        ? p.PairedWithDramatisPersona is { } partner ? $"{p.Name} & {partner.Name}" : p.Name
        : string.Empty;

    [ObservableProperty]
    private string pairRetentionAmount = string.Empty;

    [ObservableProperty]
    private string? pairRetentionError;

    partial void OnPairRetentionAmountChanged(string value)
    {
        PairRetentionError = null;
        NotifyTreasuryChanged();
        ResetWonDuelIfNoLongerNeeded();
    }

    /// <summary>Repéré à la construction (voir EndOfGamePageViewModel ctor, appelée APRÈS
    /// BuildPairCorruptionOption qui peuple _fistfulOfCrownsPersonaIds) - true si CETTE bande a déjà un
    /// personnage portant "A Fistful of Crowns" dans son roster actif, peu importe laquelle des fiches
    /// concernées (pour la paire, Marquand ou Ulli).</summary>
    private void BuildPairRetentionOption()
    {
        // Exclut un couple/personnage déjà basculé "hostile" (retour utilisateur - "on l'applique aussi
        // dans l'entretien si on les a recruté et qu'ils sont non hostile") : hostile veut dire corrompu
        // CETTE bataille, donc quitte de toute façon à la Fin de Partie (ApplyWandererDeparturesAsync,
        // Vagabond) sans paiement de son côté - "payer pour les GARDER" n'a plus de sens dans ce cas,
        // seul quelqu'un resté loyal (ou jamais approché) peut avoir coûté une contre-offre.
        var ownedRow = WarriorRows.FirstOrDefault(r => r.Warrior.IsDramatisPersona && r.Warrior.DramatisPersonaId is { } id
            && _fistfulOfCrownsPersonaIds.Contains(id) && !r.Warrior.IsHostileThisBattle);
        if (ownedRow is null)
        {
            ShowPairRetentionOption = false;
            return;
        }

        _ownedPairPersona = _dramatisPersonaCatalog.FirstOrDefault(p => p.Id == ownedRow.Warrior.DramatisPersonaId);
        ShowPairRetentionOption = _ownedPairPersona is not null;
    }

    /// <summary>"C'est l'heure de payer !" - true si le montant saisi dépasserait le solde prévisionnel
    /// (même DramatisPersonaeBaselineTreasury que le cas symétrique - cette bande n'a jamais les deux
    /// entrées à la fois, aucun risque de double-compte). Pilote le même bloc "Où est l'Argent ?" que la
    /// Corruption ci-dessus (WantsEquipmentSeizure/WantsDuel/WonDuel), affiché ici à la place.</summary>
    public bool IsPairRetentionUnaffordable => int.TryParse(PairRetentionAmount, out var amount) && amount > 0 && amount > DramatisPersonaeBaselineTreasury;

    // --- Où est l'Argent ? : repli si le montant (Corruption OU Rétention) dépasserait ce que la bande
    // peut payer - "C'est l'heure de payer ! ... Si c'est le cas, Ulli et Marquand se payeront en armes et
    // équipement... Si c'est impossible, ils se vengeront sur le chef de la bande". -------------------

    /// <summary>Montant actuellement retranché de EndOfGameTreasuryRemaining pour Corruption OU
    /// Rétention (jamais les deux à la fois pour une même bande - voir la doc de classe) - 0 si aucune
    /// des deux n'est active/valide. Consommé par EndOfGameTreasuryRemaining lui-même.</summary>
    private int PairPaymentAmountCommitted =>
        (WantsToRecordPairCorruption && int.TryParse(PairCorruptionAmount, out var corruptionAmount) ? corruptionAmount : 0)
        + (int.TryParse(PairRetentionAmount, out var retentionAmount) && retentionAmount > 0 ? retentionAmount : 0);

    /// <summary>Solde disponible AVANT ce paiement précis de Corruption/Rétention - "remet en
    /// circulation" le montant que EndOfGameTreasuryRemaining a déjà retranché via
    /// PairPaymentAmountCommitted, pour comparer CE montant à ce qui reste UNE FOIS toutes les autres
    /// dépenses de ce même wizard prises en compte ("un seul solde de trésorerie qu'on ajuste au fur et à
    /// mesure des étapes").</summary>
    private int DramatisPersonaeBaselineTreasury => EndOfGameTreasuryRemaining + PairPaymentAmountCommitted;

    /// <summary>Affichage direct de DramatisPersonaeBaselineTreasury - montre le solde précis qui sert
    /// réellement de seuil ici, pas seulement la trésorerie persistée de la bande (fiche, avant cette Fin
    /// de Partie), qui peut différer une fois l'or d'Exploration/de la Vente de pierres magiques CETTE
    /// partie pris en compte.</summary>
    public string DramatisPersonaeBaselineTreasuryDisplay => string.Format(Loc["EndOfGameDramatisPersonaeBaselineTreasuryFormat"], DramatisPersonaeBaselineTreasury);

    /// <summary>"C'est l'heure de payer !" - true si le montant de corruption saisi dépasserait ce solde
    /// prévisionnel. Pilote la visibilité du bloc Où est l'Argent.</summary>
    public bool IsPairCorruptionUnaffordable => WantsToRecordPairCorruption
        && int.TryParse(PairCorruptionAmount, out var amount) && amount > DramatisPersonaeBaselineTreasury;

    /// <summary>Valeur totale de la réserve de bande, toutes lignes confondues - seule mesure qui tranche
    /// automatiquement entre Céder du matériel et Duel avec le meneur.</summary>
    private int WarbandInventoryTotalValue => _warbandInventory.Sum(w => w.SellValue);

    /// <summary>Montant réellement dû à cette étape (corruption OU rétention, jamais les deux - voir la
    /// doc de classe) - 0 si aucun des deux blocs Où est l'Argent n'est actif.</summary>
    private int PairPaymentTargetAmount => IsPairCorruptionUnaffordable && int.TryParse(PairCorruptionAmount, out var corruptionAmount)
        ? corruptionAmount
        : IsPairRetentionUnaffordable && int.TryParse(PairRetentionAmount, out var retentionAmount) ? retentionAmount : 0;

    /// <summary>Automatique : la réserve de la bande, à elle seule, couvre le montant dû -> elle cède du
    /// matériel plutôt que de risquer son meneur en duel. Faux si aucun paiement n'est dû
    /// (PairPaymentTargetAmount == 0) ou si la réserve ne suffit pas (voir WantsDuel, l'autre branche).</summary>
    public bool WantsEquipmentSeizure => PairPaymentTargetAmount > 0 && WarbandInventoryTotalValue >= PairPaymentTargetAmount;

    /// <summary>Automatique : la réserve ne suffit pas à couvrir le montant dû -> seule alternative qui
    /// reste, un duel avec le meneur de bande (voir WantsEquipmentSeizure, l'autre branche - les deux
    /// sont mutuellement exclusives et ne couvrent jamais PairPaymentTargetAmount == 0 à la fois).</summary>
    public bool WantsDuel => PairPaymentTargetAmount > 0 && WarbandInventoryTotalValue < PairPaymentTargetAmount;

    /// <summary>"Céder du matériel" : sélection automatique dans la réserve de bande, du PLUS VALEUREUX
    /// objet au moins valeureux ("on prend les équipements les plus valuable d'abord jusqu'à atteindre la
    /// somme", ex. une Épée en Gromril à 60 CO + 3 Épées classiques à 10 CO chacune pour une rançon de
    /// 50 CO -> on ne perd QUE l'Épée en Gromril, jamais les 3 armes bon marché en plus). Perdre un
    /// minimum d'OBJETS (même si ça dépasse un peu la somme due) plutôt qu'un minimum de VALEUR - la
    /// boucle s'arrête dès que la cible est atteinte/dépassée. Une ligne = une pile ENTIÈRE (jamais un
    /// retrait partiel - aucun mécanisme de pile partielle ailleurs dans l'app, voir
    /// AlternativePaymentItemId's own doc). N'est en pratique appelée que quand WantsEquipmentSeizure est
    /// vrai (la réserve suffit), donc n'a normalement jamais besoin de vider toute la réserve sans atteindre
    /// la cible - conservé quand même en repli défensif.</summary>
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
    /// automatique couvre bien la cible (elle ne le peut pas toujours - une réserve trop pauvre saisit tout
    /// ce qu'il a sans jamais atteindre PairPaymentTargetAmount, voir SeizedEquipmentItems's own doc).</summary>
    public string SeizedEquipmentTotalDisplay => string.Format(Loc["EndOfGameSeizedEquipmentTotalFormat"], SeizedEquipmentTotalValue, PairPaymentTargetAmount);
}
