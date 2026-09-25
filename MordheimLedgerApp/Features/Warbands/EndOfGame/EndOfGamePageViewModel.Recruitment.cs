using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Recrutement (livre, étape 8 - "Hire New Recruits &amp; Buy Common Items" - "this is done in
/// any order and may be done several times") : une SÉQUENCE d'étapes dédiées plutôt qu'une carte à
/// onglets - voir StepKind.RecruitHeroesCount/RecruitHeroDetail/RecruitVeteranTopUp/
/// RecruitHenchmenCount/RecruitHenchmenEquipment/RecruitHenchmenNames dans EndOfGamePageViewModel.cs's
/// Steps.
///
/// **Historique de design** : deux itérations avant celle-ci. (1) 2026-09-05, trois étapes de wizard
/// séparées (Héros/Hommes de main/Équipement Hommes de main) - jugées trop rigides face au livre ("any
/// order... several times"), fusionnées en (2) une carte unique à 2 onglets internes librement
/// navigables (Guerriers/Équipement, RecruitmentTab), pour rester dans l'esprit "any order" tout en
/// homogénéisant Héros et Hommes de main (équipés au même endroit). Retour utilisateur 2026-09-21 : ce
/// mélange dans un même écran restait confus - "on manque d'homogénéité... on va reprendre le système
/// qu'on a dans WarbandEditDialog... écran par écran, step by step". Design (3), actuel : repasse à des
/// étapes séquentielles strictes, mais avec une différence clé par rapport à (1) - Héros : une étape
/// PAR héros recruté (StepKind.RecruitHeroDetail, dynamique, même principe que Blessure/Progression -
/// WizardStep.RecruitHeroSlot) combinant nom ET équipement pour CE héros précis sur le même écran ("ce
/// qui permet d'assigner directement les items au personnage") - contrairement à (2), les Héros ne sont
/// donc plus nommés dans une étape Noms commune à la fin. Hommes de main : le top-up des groupes déjà
/// existants (budget vétérans) devient sa propre étape (RecruitVeteranTopUp), puis un nouveau groupe
/// garde 3 étapes distinctes (Effectif/Équipement/Noms) - ces 3-là reprennent le contenu exact qui vivait
/// dans l'onglet Guerriers/Équipement de (2), juste déplacé en écrans séparés.
///
/// Réutilise directement les types de brouillon déjà décorrélés de WarbandEditDialogViewModel
/// (WarriorRecruitRow/WarriorNameSlot/HenchmanGroupDraft, EquipmentPick, voir leur propre doc de classe -
/// "Purement de la présentation... aplati en vrais Warrior au moment de persister, rien avant") plutôt
/// que de rouvrir ce dialog mature mid-wizard : celui-ci écrit en base IMMÉDIATEMENT à sa fermeture
/// (Save()), ce qui casserait l'invariant "rien en base tant que Terminer n'a pas tourné" de tout le
/// reste de ce wizard - refus explicite de l'utilisateur d'un mécanisme de rollback ("on peut pas jouer
/// à mumuse avec la bdd"). Les guerriers recrutés ici ne deviennent de vrais Warrior qu'à
/// EndOfGamePageViewModel.Apply.ApplyRecruitmentAsync, même moment que tout le reste du wizard.
///
/// isExistingWarband: false partout (contrairement au mode "Bande existante" du dialog réutilisé) : la
/// trésorerie est ENFORCÉE (RecruitmentRules.CanRecruit compare contre EndOfGameTreasuryRemaining, pas
/// une saisie libre non contrôlée) et les onglets Compétences/XP de RecruitSlotTabsView restent masqués
/// (RecruitSlot.ShowSkillsTab) - une recrue en pleine campagne n'a pas d'historique papier à importer,
/// contrairement au cas "Bande existante" du dialog d'origine.
///
/// Groupes EXISTANTS (RecruitVeteranTopUp) : équipement TOUJOURS AUTOMATIQUE (livre des règles,
/// "New Henchmen must be armed and equipped in the same way as existing members of the group"), jamais
/// un choix libre - voir GetTopUpBreakdown. Nouveau groupe : achat libre au picker (commonOnly: true -
/// "can only buy Common items... freely", les Objets Rares ont déjà leur propre étape plus tôt dans ce
/// wizard), avec Diviser (SplitHenchmanGroupDraft, même mécanique que WarbandEditDialogViewModel) pour
/// équiper différemment une partie d'un même type recruté - c'est pourquoi son étape Noms (nombre RÉEL
/// de groupes) vient APRÈS celle d'Équipement, jamais avant.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>WarbandArchetype complet de CETTE bande (contrairement à _warbandArchetypeId/
    /// _warbandArchetypeName, déjà connus avant cette étape mais insuffisants ici) - nécessaire pour
    /// MaxWarriors (RecruitmentRules.CanRecruit). Chargé par l'appelant (WarbandDetailViewModel.EndOfGame)
    /// comme tout le reste des données de ce wizard, jamais paresseusement ici - voir sa propre doc de
    /// classe pour pourquoi cette étape n'a pas de chargement asynchrone à elle.</summary>
    private WarbandArchetype _recruitableWarbandArchetype = new();

    /// <summary>WarriorArchetype recrutables pour CETTE bande - peuplés une seule fois au constructeur
    /// (voir recruitableWarriorArchetypes, chargé par l'appelant comme dramatisPersonaCatalog/
    /// localizedHiredSwords etc.) plutôt que paresseusement à l'entrée de cette étape : ce wizard ne charge
    /// jamais de données async après sa propre construction, Next()/Previous() restent volontairement
    /// synchrones.</summary>
    public ObservableCollection<WarriorRecruitRow> RecruitRows { get; } = new();

    public IEnumerable<WarriorRecruitRow> HeroRecruitRows => RecruitRows.Where(r => r.IsHero);

    /// <summary>Sous-ensemble de HeroRecruitRows effectivement recruté (Count &gt; 0) - source directe des
    /// étapes dynamiques StepKind.RecruitHeroDetail (une par NameSlot, voir EndOfGamePageViewModel.cs's
    /// Steps), même idiome que WarbandEditDialogViewModel.RecruitedRows. Recalculée à chaque
    /// Increment/Decrement (voir UpdateRecruitRowsEligibility).</summary>
    public IEnumerable<WarriorRecruitRow> RecruitedHeroRows => HeroRecruitRows.Where(r => r.Count > 0);

    /// <summary>Effectif de bande projeté/plafond ("12/15") - même formule que
    /// WarbandEditDialogViewModel.RosterCountDisplay, affichée à l'étape RecruitHeroesCount (retour
    /// utilisateur 2026-09-05 - "manque d'homogénéité... par rapport à ce qui se fait lorsque l'on crée
    /// une nouvelle bande" : la validation appliquait déjà MaxWarriors via CanRecruit, mais ne l'affichait
    /// nulle part en toutes lettres, contrairement à la création). "∞" si l'archétype n'a pas de plafond.</summary>
    public string RecruitmentRosterCountDisplay =>
        $"{TotalWarriorCountAfterRecruitment}/{_recruitableWarbandArchetype.MaxWarriors?.ToString() ?? "∞"}";

    /// <summary>Même format que RareItemPurchaseRemainingTreasuryDisplay ("Trésorerie restante : {0} CO") -
    /// clé resx partagée plutôt que dupliquée, ce texte n'est pas propre à l'étape Objets rares malgré son
    /// nom.</summary>
    public string RecruitmentTreasuryDisplay => string.Format(Loc["EndOfGameRareItemTreasuryFormat"], EndOfGameTreasuryRemaining);

    /// <summary>Effectif total de la bande APRÈS ce recrutement (guerriers déjà là + nouvelles recrues de
    /// cette étape + vétérans ajoutés aux groupes existants - renvois de l'étape Renvoyer) - comparé à
    /// WarbandArchetype.MaxWarriors par RecruitmentRules.CanRecruit, même principe que
    /// WarbandEditDialogViewModel.TotalWarriorCount mais en partant d'un roster déjà existant plutôt que de
    /// zéro. Inclut ExistingHenchmanTopUps.AddCount depuis le 2026-09-22 (retour utilisateur - "la limite
    /// de la taille de la bande à prendre en compte aussi dans le recrutement des vétérans") :
    /// ExistingHenchmanTopUps est peuplé dès le constructeur (AddCount à 0 au départ), donc toujours sûr à
    /// additionner ici même avant d'atteindre cette étape. Retranche WarriorRows.DismissCount (même jour,
    /// retour utilisateur - "dans les recrutement, il faut soustraire les effectifs") : un guerrier renvoyé
    /// à l'étape Renvoyer (juste avant, dans Steps) libère de la place, même limitation "stale jusqu'à
    /// Terminer" qu'ailleurs dans ce wizard (DismissCount plafonné à HeadCount par le stepper, jamais
    /// négatif).</summary>
    private int TotalWarriorCountAfterRecruitment => WarriorRows.Sum(r => r.Warrior.HeadCount - r.DismissCount) + RecruitRows.Sum(r => r.Count)
        + ExistingHenchmanTopUps.Sum(t => t.AddCount);

    /// <summary>Effectif déjà recruté de CE type précis, avant même d'ouvrir cette étape - comparé à
    /// WarriorArchetype.MaxCount. Compte tous les guerriers vivants (WarriorRows exclut déjà les morts/
    /// retraités - voir WarbandDetailViewModel.EndOfGame's activeWarriorRows), pas seulement les Héros.
    /// Retranche DismissCount, même raison que TotalWarriorCountAfterRecruitment.</summary>
    private int ExistingCountForArchetype(int warriorArchetypeId) =>
        WarriorRows.Where(r => r.Warrior.WarriorArchetypeId == warriorArchetypeId).Sum(r => r.Warrior.HeadCount - r.DismissCount);

    /// <summary>Chip tapable sur chaque ligne de WarriorRecruitListView (Steps/RecruitHeroesCountStepView.
    /// xaml/RecruitHenchmenCountStepView.xaml's DetailCommand) - 2026-09-23, retour utilisateur "plutôt
    /// que d'avoir un label ou une chip non interagissable, ça serait top de pouvoir cliquer sur une chip
    /// qui nous révèle le détail du personnage/archétype". Ce candidat n'est encore qu'un WarriorArchetype
    /// du catalogue (pas un vrai Warrior recruté), donc même mécanisme que
    /// WarbandEditDialogViewModel.ShowWarriorDetail (copié à l'identique) plutôt que le nouveau dialog
    /// récap Guerrier (ShowWarriorRecapCommand, EndOfGamePageViewModel.cs) réservé aux guerriers déjà
    /// actifs. row.Archetype n'a que le résumé déjà chargé pour la liste - re-résolu en entier ici pour le
    /// récap (description complète etc.), comme WarbandEditDialogViewModel le fait.</summary>
    [RelayCommand]
    private async Task ShowWarriorDetail(WarriorRecruitRow row)
    {
        var language = LocalizationService.Instance.Language;
        WarriorArchetype? fullWarrior = null;
        await Loading.RunAsync(async () => fullWarrior = await Task.Run(() => _libraryService.GetWarriorArchetypeAsync(row.Archetype.Id, language)));
        if (fullWarrior is null) return;

        // Pas de listes d'équipement chargées à ce stade (le nom de la liste, pas son contenu, n'est pas
        // utile ici) - EquipmentListDisplay retombe sur "aucune" dans le dialog récap, même limite
        // acceptée que WarbandEditDialogViewModel.ShowWarriorDetail.
        await _detailDialogs.ShowWarriorArchetypeDetailDialogAsync(fullWarrior, Array.Empty<NamedRef>());
    }

    // --- Héros : effectif -----------------------------------------------------------------------------

    [RelayCommand]
    private void IncrementRecruit(WarriorRecruitRow row)
    {
        if (!RecruitmentRules.CanRecruit(ExistingCountForArchetype(row.Archetype.Id) + row.Count, row.Archetype.MaxCount,
                TotalWarriorCountAfterRecruitment, _recruitableWarbandArchetype.MaxWarriors, isExistingWarband: false, EndOfGameTreasuryRemaining, row.Cost))
            return;

        row.Count++;
        if (row.IsHero)
        {
            var slot = new WarriorNameSlot(row, isExistingWarband: false);
            row.NameSlots.Add(slot);
            RenumberRecruitHeroLabels(row);
            // Suggestion pré-remplie (comme WarbandEditDialogViewModel.PopulateSuggestedNames), pas
            // seulement un Placeholder : sans ça, ValidateRecruitNamesStep bloquerait tant que le joueur
            // n'a pas explicitement retapé un nom déjà visible à l'écran (le Placeholder d'un Entry vide
            // ne compte jamais comme une vraie valeur) - modifiable ensuite comme n'importe quel autre nom.
            slot.Name = slot.ArchetypeLabel;
        }

        UpdateRecruitRowsEligibility();
        NotifyTreasuryChanged();
    }

    [RelayCommand]
    private void DecrementRecruit(WarriorRecruitRow row)
    {
        if (row.Count == 0) return;

        row.Count--;
        if (row.IsHero && row.NameSlots.Count > 0)
        {
            row.NameSlots.RemoveAt(row.NameSlots.Count - 1);
            RenumberRecruitHeroLabels(row);
        }

        UpdateRecruitRowsEligibility();
        NotifyTreasuryChanged();
    }

    private static void RenumberRecruitHeroLabels(WarriorRecruitRow row)
    {
        var multiple = row.NameSlots.Count > 1;
        for (var i = 0; i < row.NameSlots.Count; i++)
            row.NameSlots[i].ArchetypeLabel = multiple ? $"{row.Archetype.Name} {i + 1}" : row.Archetype.Name;
    }

    /// <summary>Recalcule CanIncrement de chaque ligne - le MaxWarriors de la bande et la trésorerie
    /// dépendent de TOUTES les lignes/étapes, pas seulement celle qui vient de changer. Notifie aussi
    /// StepLabel/IsLastStep : RecruitedHeroRows/RecruitedHenchmanRows passant de/à vide fait
    /// apparaître/disparaître les étapes dynamiques RecruitHeroDetail et RecruitHenchmenEquipment/Names
    /// (retour utilisateur - "si il n'y a pas de nouveau groupe, on n'affiche pas les step d'équipement et
    /// de nom"), même idiome que RareItemSearchEntries/WarriorRows ailleurs dans ce fichier pour une étape
    /// qui apparaît/disparaît.</summary>
    private void UpdateRecruitRowsEligibility()
    {
        OnPropertyChanged(nameof(RecruitedHeroRows));
        OnPropertyChanged(nameof(RecruitedHenchmanRows));
        OnPropertyChanged(nameof(RecruitmentRosterCountDisplay));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(IsLastStep));
        // ReallocationCarriers dépend de RecruitedHeroRows (nouvelles recrues) - retour utilisateur
        // 2026-09-22 : "une nouvelle recrue qui n'est pas affichée" à l'étape Réallouer. ReallocationCarriers
        // est une propriété "live" (jamais mise en cache), mais MAUI évalue le binding
        // BindableLayout.ItemsSource UNE SEULE FOIS, tôt (avant même tout recrutement) - sans notification
        // explicite, l'affichage ne se rafraîchit jamais quand une recrue s'ajoute ensuite (même si la
        // commande MoveReallocationItem, qui relit la propriété directement en C#, voit bien la recrue).
        OnPropertyChanged(nameof(ReallocationCarriers));
        foreach (var row in RecruitRows)
        {
            // ExternalHeadCount (affiché par CountDisplay, "X/Y") doit lui aussi refléter un renvoi
            // pendant à l'étape Renvoyer (retour utilisateur 2026-09-22 - "dans les écrans de recrutement
            // sur les -/+ les dismissed ne sont pas décomptés") : posé une seule fois au constructeur
            // jusqu'ici (DismissCount valait alors toujours 0), jamais rafraîchi depuis - cette méthode est
            // déjà appelée à chaque changement de DismissCount (voir l'abonnement PropertyChanged sur
            // WarriorRows), donc le bon endroit pour le tenir à jour.
            row.ExternalHeadCount = ExistingCountForArchetype(row.Archetype.Id);
            row.CanIncrement = RecruitmentRules.CanRecruit(ExistingCountForArchetype(row.Archetype.Id) + row.Count, row.Archetype.MaxCount,
                TotalWarriorCountAfterRecruitment, _recruitableWarbandArchetype.MaxWarriors, isExistingWarband: false, EndOfGameTreasuryRemaining, row.Cost);
        }
    }

    // Rien à valider pour l'étape RecruitHeroesCount (steppers seuls) - le nom et l'équipement de chaque
    // héros se valident/s'achètent dans sa propre étape RecruitHeroDetail (ValidateRecruitHeroDetailStep,
    // plus bas).

    // --- Hommes de main : groupes existants (budget vétérans) + nouveau groupe ---------------------------

    /// <summary>Une ligne par groupe d'Homme de main encore actif ce tour (WarriorRows, jamais un Héros) -
    /// peuplée une seule fois par BuildExistingHenchmanTopUps (appelée depuis le constructeur principal,
    /// même moment que BuildPairCorruptionOption et consorts).</summary>
    public ObservableCollection<ExistingHenchmanTopUp> ExistingHenchmanTopUps { get; } = new();

    private void BuildExistingHenchmanTopUps()
    {
        foreach (var row in WarriorRows.Where(r => !r.Warrior.IsHero))
        {
            var archetypeRow = RecruitRows.FirstOrDefault(r => r.Archetype.Id == row.Warrior.WarriorArchetypeId);
            if (archetypeRow is null) continue; // ne devrait pas arriver (archétype retiré du catalogue entre-temps)
            ExistingHenchmanTopUps.Add(new ExistingHenchmanTopUp(row, archetypeRow)
            {
                ExistingCountForType = ExistingCountForArchetype(archetypeRow.Archetype.Id)
            });
        }
        RefreshHenchmanTopUpBreakdowns();
    }

    /// <summary>Recalcule et "pousse" le détail de coût (ExistingHenchmanTopUp.Breakdown) de CHAQUE groupe
    /// existant - appelée après tout Increment/DecrementHenchmanTopUp (un changement sur UN groupe peut
    /// affecter le détail d'un AUTRE, la réserve étant partagée et consommée dans l'ordre, voir
    /// GetTopUpBreakdown), une première fois à la construction, ET à chaque changement de DismissCount
    /// (voir l'abonnement PropertyChanged sur WarriorRows dans EndOfGamePageViewModel.cs). Rafraîchit
    /// aussi ExistingCountForType (retour utilisateur 2026-09-22 - "dans les écrans de recrutement sur les
    /// -/+ les dismissed ne sont pas décomptés... pas bon pour les vétérans") : posé une seule fois par
    /// BuildExistingHenchmanTopUps jusqu'ici (DismissCount valait alors toujours 0), jamais rafraîchi
    /// depuis - même bug/même correctif que WarriorRecruitRow.ExternalHeadCount côté
    /// UpdateRecruitRowsEligibility.</summary>
    private void RefreshHenchmanTopUpBreakdowns()
    {
        foreach (var topUp in ExistingHenchmanTopUps)
        {
            topUp.ExistingCountForType = ExistingCountForArchetype(topUp.WarriorArchetypeId);
            topUp.Breakdown = GetTopUpBreakdown(topUp);
        }
    }

    /// <summary>Budget vétérans restant (livre des règles - "you can hire as many warriors as you wish, as
    /// long as their combined Experience does not exceed your dice roll") : le jet de l'étape Disponibilité
    /// des Vétérans, moins ce que les groupes existants ont déjà réservé (AddCount × GroupExperience
    /// chacun). Un nouveau groupe (HenchmanRecruitRows plus bas) ne dispute JAMAIS ce budget - la règle ne
    /// concerne que les groupes déjà là (StartingExperience 0 pour un type jamais recruté).</summary>
    public int RemainingVeteranBudget =>
        (int.TryParse(VeteranExperienceRoll, out var roll) ? roll : 0)
        - ExistingHenchmanTopUps.Sum(t => t.AddCount * t.GroupExperience);

    public bool HasExistingHenchmanGroups => ExistingHenchmanTopUps.Count > 0;

    public string RecruitHenchmenExistingHintDisplay => string.Format(Loc["EndOfGameRecruitHenchmenExistingHint"], RemainingVeteranBudget);

    [RelayCommand]
    private void IncrementHenchmanTopUp(ExistingHenchmanTopUp topUp)
    {
        if (topUp.GroupExperience > RemainingVeteranBudget) return;
        // WarriorArchetype.MaxCount et WarbandArchetype.MaxWarriors - jusqu'ici seul le budget XP
        // bloquait l'incrément, retour utilisateur 2026-09-22. isExistingWarband: true réutilise
        // RecruitmentRules.CanRecruit uniquement pour ses deux gardes MaxCount/MaxWarriors, en
        // désactivant volontairement sa garde trésorerie (déjà gérée séparément par le budget XP et par
        // le coût total affiché - pas de vérification treasury<cost pertinente ici, le coût réel dépend
        // de la réserve/GetTopUpBreakdown, pas d'un simple ArchetypeCost).
        if (!RecruitmentRules.CanRecruit(ExistingCountForArchetype(topUp.WarriorArchetypeId) + topUp.AddCount, topUp.MaxCountForType,
                TotalWarriorCountAfterRecruitment, _recruitableWarbandArchetype.MaxWarriors, isExistingWarband: true, EndOfGameTreasuryRemaining, cost: 0))
            return;

        topUp.AddCount++;
        RefreshHenchmanTopUpBreakdowns();
        UpdateRecruitRowsEligibility();
        NotifyTreasuryChanged();
        OnPropertyChanged(nameof(RemainingVeteranBudget));
        OnPropertyChanged(nameof(RecruitHenchmenExistingHintDisplay));
    }

    [RelayCommand]
    private void DecrementHenchmanTopUp(ExistingHenchmanTopUp topUp)
    {
        if (topUp.AddCount == 0) return;
        topUp.AddCount--;
        RefreshHenchmanTopUpBreakdowns();
        UpdateRecruitRowsEligibility();
        NotifyTreasuryChanged();
        OnPropertyChanged(nameof(RemainingVeteranBudget));
        OnPropertyChanged(nameof(RecruitHenchmenExistingHintDisplay));
    }

    /// <summary>Types d'Homme de main recrutables pour CETTE bande - même WarriorRecruitRow que les Héros
    /// (RecruitRows), juste le sous-ensemble non-Héros. Contrairement aux Héros, un seul HenchmanGroupDraft
    /// par ligne (le groupe ENTIER partage un même équipement, livre des règles) plutôt qu'un slot par
    /// individu - même principe que WarbandEditDialogViewModel.IncrementWarrior côté Hommes de main.</summary>
    public IEnumerable<WarriorRecruitRow> HenchmanRecruitRows => RecruitRows.Where(r => !r.IsHero);

    public IEnumerable<WarriorRecruitRow> RecruitedHenchmanRows => HenchmanRecruitRows.Where(r => r.Count > 0);

    [RelayCommand]
    private void IncrementHenchmanGroup(WarriorRecruitRow row)
    {
        if (!RecruitmentRules.CanRecruit(ExistingCountForArchetype(row.Archetype.Id) + row.Count, row.Archetype.MaxCount,
                TotalWarriorCountAfterRecruitment, _recruitableWarbandArchetype.MaxWarriors, isExistingWarband: false, EndOfGameTreasuryRemaining, row.Cost))
            return;

        row.Count++;
        if (row.HenchmanGroupDrafts.Count == 0)
            row.HenchmanGroupDrafts.Add(new HenchmanGroupDraft(row, row.Archetype.Name, 1, isExistingWarband: false));
        else
            row.HenchmanGroupDrafts[^1].Count++;

        UpdateRecruitRowsEligibility();
        NotifyTreasuryChanged();
    }

    [RelayCommand]
    private void DecrementHenchmanGroup(WarriorRecruitRow row)
    {
        if (row.Count == 0 || row.HenchmanGroupDrafts.Count == 0) return;

        row.Count--;
        var last = row.HenchmanGroupDrafts[^1];
        last.Count--;
        if (last.Count <= 0) row.HenchmanGroupDrafts.RemoveAt(row.HenchmanGroupDrafts.Count - 1);

        UpdateRecruitRowsEligibility();
        NotifyTreasuryChanged();
    }

    /// <summary>Réserve encore DISPONIBLE pour la prochaine recrue (Héros ou nouveau groupe d'Hommes de
    /// main) - la réserve après tout le recrutement déjà décidé (vétérans + picks FromReserve), un même
    /// exemplaire n'étant jamais compté deux fois. Voir EndOfGamePageViewModel.Reserve.cs.</summary>
    private Dictionary<(int ItemId, int? MaterialRuleId), int> BuildAvailableReservePool() => BuildReserve(ReserveStage.AfterRecruitment).ToPool();

    /// <summary>Le matériau RÉELLEMENT associé à un exemplaire disponible en réserve pour cet Item - null si
    /// aucun exemplaire dispo (achat neuf classique) ou si la seule variante dispo est sans matériau.
    /// Retour utilisateur 2026-09-21 - "une épée ornée gagnée à l'Exploration s'affiche en épée simple et
    /// une épée simple est donnée à la recrue au lieu de l'épée (o)" : une trouvaille avec matériau doit
    /// être reconnue comme disponible et consommée telle quelle, pas remplacée par un achat neuf sans
    /// matériau.</summary>
    private SpecialRule? ResolveAvailableReserveMaterial(int itemId, int perUnitCost)
    {
        var reserve = BuildReserve(ReserveStage.AfterRecruitment);
        var pool = reserve.ToPool();
        return reserve.Lines.FirstOrDefault(l => l.Item.Id == itemId && pool[(l.Item.Id, l.MaterialRule?.Id)] >= perUnitCost)?.MaterialRule;
    }

    /// <summary>Détail de coût d'UN groupe existant (retour utilisateur - "il faut détailler le calcul au
    /// recrutement de vétéran") : recrutement + une ligne par type d'équipement du groupe (avec combien
    /// vient de la réserve, gratuit) + la surtaxe d'Expérience. Mise en forme de PlanTopUps, la seule
    /// définition de la consommation de réserve par les vétérans (voir EndOfGamePageViewModel.Reserve.cs) -
    /// HenchmanRecruitmentTotalCost (trésorerie) et Terminer additionnent ce même Total.</summary>
    public HenchmanTopUpCostBreakdown GetTopUpBreakdown(ExistingHenchmanTopUp topUp)
    {
        if (topUp.AddCount == 0 || !PlanTopUps().TryGetValue(topUp, out var plan))
            return new HenchmanTopUpCostBreakdown(0, Array.Empty<HenchmanEquipmentCostLine>(), 0, 0);

        // Nom + matériau (comme WarriorEquipment.NameDisplay), mais SANS son suffixe "xN" - ce N-là est la
        // Quantity totale déjà existante du groupe, pas NeededQty (le total ADDITIONNEL pour les recrues
        // ajoutées, affiché séparément par cette ligne).
        var lines = plan.Select(p =>
        {
            var materialSuffix = p.Equipment.MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $" ({abbr})" : string.Empty;
            return new HenchmanEquipmentCostLine($"{p.Equipment.Item.Name}{materialSuffix}", p.NeededQty, p.Cost, p.FromStash);
        }).ToList();

        return new HenchmanTopUpCostBreakdown(topUp.AddCount * topUp.ArchetypeCost, lines,
            topUp.AddCount * 2 * topUp.GroupExperience, plan.Sum(p => p.FromStash));
    }

    /// <summary>Coût total des Hommes de main en attente - groupes existants (GetTopUpBreakdown, réserve
    /// déduite) additionnés dans l'ordre d'ExistingHenchmanTopUps, plus le recrutement ET l'équipement
    /// (étape RecruitHenchmenEquipment, choix au picker - réserve en priorité depuis l'étape Achat/Vente
    /// précédente, voir AddRecruitEquipment, sinon plein tarif) d'un éventuel nouveau groupe.
    /// EquipmentPick.Cost porte déjà le prix unitaire (0 si FromReserve) - même formule que
    /// WarbandEditDialogViewModel.TotalSpent (coût par exemplaire × effectif du groupe). Recalculé à
    /// chaque lecture, donc toujours cohérent entre cet aperçu (EndOfGameTreasuryRemaining) et
    /// l'application réelle à Terminer (EndOfGamePageViewModel.Apply.ApplyHenchmanRecruitmentAsync,
    /// même algorithme).</summary>
    private int HenchmanRecruitmentTotalCost()
    {
        var served = ServedReservePicks();
        return ExistingHenchmanTopUps.Where(t => t.AddCount > 0).Sum(t => GetTopUpBreakdown(t).Total)
            + HenchmanRecruitRows.Sum(r => r.HenchmanGroupDrafts.Sum(g => g.Count) * r.Cost)
            + HenchmanRecruitRows.SelectMany(r => r.HenchmanGroupDrafts).Sum(g => g.Equipment.Sum(e => RecruitPickCost(e, served)) * g.Count)
            + HenchmanRecruitRows.SelectMany(r => r.HenchmanGroupDrafts).Sum(g => g.Mutations.Sum(m => m.Cost) * g.Count);
    }

    /// <summary>Coût de l'équipement acheté pour les Héros recrutés cette session (étape RecruitHeroDetail,
    /// choix au picker - réserve en priorité, voir AddRecruitEquipment) - toujours ×1 (jamais de groupe
    /// côté Héros, contrairement aux Hommes de main). Inclut les mutations achetées (Possédés/Mutants - livre, "+ the cost of
    /// mutations").</summary>
    private int HeroEquipmentTotalCost()
    {
        var served = ServedReservePicks();
        return HeroRecruitRows.SelectMany(r => r.NameSlots).Sum(s => s.Equipment.Sum(e => RecruitPickCost(e, served)) + s.Mutations.Sum(m => m.Cost));
    }

    // --- Équipement : achat pour Héros (WarriorNameSlot, étape RecruitHeroDetail) ET nouveaux groupes
    // d'Hommes de main (HenchmanGroupDraft, étape RecruitHenchmenEquipment) - réserve en priorité (étape
    // Achat/Vente précédente), sinon plein tarif - jamais les groupes existants (équipement automatique,
    // voir plus haut) ---------------------------------------------------------------------------------

    /// <summary>Détache un second HenchmanGroupDraft du groupe tapé - même mécanique que
    /// WarbandEditDialogViewModel.SplitHenchmanGroupDraft (livre des règles : "if your Henchman group has
    /// four warriors, and you want to buy them swords, you must buy four swords" - deux équipements
    /// différents pour le même type = deux groupes). Demande combien d'unités transférer (1..group.Count-1)
    /// - le nouveau groupe démarre sans équipement, les deux sont renumérotés "{Archétype} 1"/"{Archétype}
    /// 2"... (RenumberHenchmanGroupDrafts) plutôt que de partager le même nom brut, toujours personnalisable
    /// ensuite à l'étape RecruitHenchmenNames.</summary>
    [RelayCommand]
    private async Task SplitHenchmanGroupDraft(HenchmanGroupDraft group)
    {
        if (group.Count <= 1) return;

        var input = await ShowPromptAsync(Loc["WarbandsSplitGroupTitle"], string.Format(Loc["WarbandsSplitGroupPrompt"], group.Count - 1));
        if (!int.TryParse(input, out var moved) || moved <= 0 || moved >= group.Count) return;

        group.Count -= moved;
        var newGroup = new HenchmanGroupDraft(group.Row, group.Row.Archetype.Name, moved, isExistingWarband: false);
        var index = group.Row.HenchmanGroupDrafts.IndexOf(group);
        group.Row.HenchmanGroupDrafts.Insert(index + 1, newGroup);
        RenumberHenchmanGroupDrafts(group.Row);
        NotifyTreasuryChanged();
    }

    private static void RenumberHenchmanGroupDrafts(WarriorRecruitRow row)
    {
        var multiple = row.HenchmanGroupDrafts.Count > 1;
        for (var i = 0; i < row.HenchmanGroupDrafts.Count; i++)
            row.HenchmanGroupDrafts[i].Name = multiple ? $"{row.Archetype.Name} {i + 1}" : row.Archetype.Name;
    }

    /// <summary>Achat d'équipement pour une cible : un WarriorNameSlot (Héros) ou un HenchmanGroupDraft
    /// (nouveau groupe d'Hommes de main, jamais un groupe déjà existant - son équipement reste
    /// automatique). Filtré par row.Archetype.EquipmentListId (la liste propre au type recruté), même
    /// principe que WarbandEditDialogViewModel.AddEquipment - **revenu sur une version "achat libre"
    /// (equipmentListId: null) du 2026-09-05** (retour utilisateur d'alors - "ce que j'imaginais pour
    /// l'équipement c'est soit de piocher dans la réserve, soit avoir accès à ce qu'on peut acheter au
    /// magasin, en faisant fi de la liste d'équipement du personnage") : FAQ officielle citée par
    /// l'utilisateur 2026-09-22 - "you must always equip any newly hired warriors using the equipment list
    /// from your warband" (Héros ET Hommes de main confirmés). RestrictedToWarriorArchetypeIds
    /// (warriorArchetypeId, toujours passé) continue de narrower en plus les objets réservés à d'autres
    /// archétypes précis. commonOnly: true (livre des règles - "can only buy Common items... freely", les
    /// Objets Rares ont déjà leur propre étape plus tôt dans ce wizard) -
    /// jamais de choix de matériau pour les armes de corps à corps (retour utilisateur 2026-09-21, revu par
    /// rapport à une première version qui ouvrait MaterialPickerDialog ici : Gromril/Ithilmar sont des
    /// améliorations RARES, jamais proposées par un marchand qui ne vend QUE des Objets Communs, même
    /// raison que AddReserveEquipment côté Achat). Réserve en priorité (2026-09-05, retour utilisateur -
    /// "si on faisait d'abord l'achat vente d'équipement, puis le recrutement avec l'assignation des
    /// équipements de la stash") : si BuildAvailableReservePool en contient déjà assez pour TOUT le lot
    /// (perUnitCost, l'effectif du groupe pour un Homme de main - 1 pour un Héros), le pick est FromReserve
    /// (gratuit, les consomme) plutôt qu'acheté au plein tarif - simplification délibérée, pas de mélange
    /// partiel réserve/achat au sein d'un même pick (contrairement à GetTopUpBreakdown, qui gère ce mélange
    /// pour les groupes EXISTANTS via une ligne de coût détaillée séparée - un nouveau pick reste ici un
    /// choix unique au picker).</summary>
    [RelayCommand]
    private async Task AddRecruitEquipment(object target)
    {
        WarriorRecruitRow row;
        ObservableCollection<EquipmentPick> destination;
        int perUnitCost;
        switch (target)
        {
            case WarriorNameSlot slot:
                row = slot.Row;
                destination = slot.Equipment;
                perUnitCost = 1;
                break;
            case HenchmanGroupDraft group:
                row = group.Row;
                destination = group.Equipment;
                perUnitCost = group.Count;
                break;
            default:
                return;
        }

        // Section "Réserve" épinglée en haut du picker (retour utilisateur 2026-09-21 - "je pense qu'il
        // peut être utile d'afficher les équipements présents dans la réserve dans le sélecteur", puis
        // "on affiche aussi si il y a un matériau sur l'arme") - regroupé par Item.Id (une seule tuile par
        // Item, pas par variante de matériau - EquipmentItemRow/EquipmentItemView affichent une tuile par
        // Item), mais MaterialRule porte un matériau représentatif quand il y en a un en stock (résolu via
        // ResolveAvailableReserveMaterial - même simplification "un seul matériau représentatif" que côté
        // application du pick, voir sa propre doc), affiché en suffixe sur la tuile (ex. "Épée (Ornée)").
        var reserveQuantities = BuildAvailableReservePool()
            .GroupBy(kv => kv.Key.ItemId)
            .ToDictionary(g => g.Key, g => (g.Sum(kv => kv.Value), ResolveAvailableReserveMaterial(g.Key, perUnitCost)));

        // equipmentListId = la liste propre au type recruté (row.Archetype.EquipmentListId) - revenu sur la
        // version "achat libre" (equipmentListId: null) du 2026-09-05 : FAQ officielle citée par
        // l'utilisateur 2026-09-22 - "you must always equip any newly hired warriors using the equipment
        // list from your warband", confirmée pour Héros ET Hommes de main. RestrictedToWarriorArchetypeIds
        // (row.Archetype.Id) continue de s'appliquer par-dessus, comme avant.
        var items = await _equipmentPicker.PickEquipmentAsync(_recruitableWarbandArchetype.Id, row.Archetype.EquipmentListId, row.Archetype.Id,
            EndOfGameTreasuryRemaining, perUnitCost, destination.Any(p => p.Item.IsFreeDagger), commonOnly: true, reserveQuantities: reserveQuantities, allowCreate: false);

        // Jamais de CHOIX interactif de matériau ici (retour utilisateur 2026-09-21 - même raison que
        // AddReserveEquipment/EquipmentTrading.cs) : Gromril/Ithilmar sont des améliorations RARES
        // (SpecialRule.CostMultiplier), jamais proposées par un marchand qui ne vend QUE des Objets
        // Communs (commonOnly: true, comme cette étape) - une recrue en pleine campagne s'équipe au même
        // comptoir que l'Achat, pas de raison de traiter les deux différemment. Un matériau peut malgré
        // tout être ATTACHÉ automatiquement, sans choix du joueur, quand l'objet vient de la réserve (une
        // trouvaille d'Exploration a pu arriver avec un matériau déjà déterminé, ex. une Épée Ornée) - voir
        // ResolveAvailableReserveMaterial ci-dessous.
        foreach (var equipmentItem in items)
        {
            // Réserve vérifiée EN PREMIER, avant toute considération de dague gratuite (retour utilisateur
            // 2026-09-21 - "la première dague qu'on valide est une dague simple... la mécanique de dague
            // gratuite ne doit être que sur la partie achat qu'on a déjà en place et pas sur la réserve") :
            // un exemplaire en réserve (Dague Ornée trouvée à l'Exploration comprise) reste TOUJOURS son
            // vrai matériau et repart marqué FromReserve, jamais réinterprété comme "la dague gratuite" du
            // guerrier (qui ne concerne QUE ce qui est acheté à neuf au comptoir, EquipmentPick.Cost étant
            // déjà à 0 pour FromReserve comme pour IsFree - inutile et faux de cumuler les deux ici).
            var reserveMaterial = ResolveAvailableReserveMaterial(equipmentItem.Id, perUnitCost);
            var reserveKey = (equipmentItem.Id, (int?)null);
            var fromReserve = reserveMaterial is not null || BuildAvailableReservePool().GetValueOrDefault(reserveKey) >= perUnitCost;

            EquipmentPick pick;
            if (fromReserve)
            {
                pick = new EquipmentPick(equipmentItem, reserveMaterial) { FromReserve = true };
            }
            else
            {
                pick = new EquipmentPick(equipmentItem, materialRule: null)
                {
                    IsFree = EquipmentPricing.IsFreeDaggerEligible(equipmentItem.IsFreeDagger, destination.Any(p => p.Item.IsFreeDagger))
                };
            }

            // Plus de dialog "fonds insuffisants" ici (retirée 2026-09-23, même correctif que
            // AddReserveEquipment/EquipmentTrading.cs - elle s'affichait une demi-seconde puis
            // disparaissait, même course de navigation modale que l'avertissement de limite d'armes
            // ci-dessous). Tous les objets choisis rejoignent la cible ; un solde négatif bloque Suivant
            // via IsTreasuryBlocked (bandeau dédié dans RecruitHeroDetailStepView.xaml/
            // RecruitHenchmenEquipmentStepView.xaml).
            destination.Add(pick);
            NotifyTreasuryChanged();
        }

        // L'avertissement "2 armes de corps à corps / 2 armes de tir différentes max par guerrier" ne se
        // déclenche plus ici (retour utilisateur 2026-09-21 - "on a plus rien qui s'affiche lorsqu'on
        // valide quelque chose... les avertissements devraient se mettre au moment où on change de step en
        // cliquant sur next") : un ShowInfoAsync lancé juste après la fermeture du picker (lui-même une
        // page plein écran poussée modalement) se prenait dans la même course de navigation modale que la
        // page qui se dépile - voir EndOfGamePageViewModel.cs's Next()/ShowWeaponLimitWarningIfNeededAsync,
        // qui vérifie la même règle mais au clic sur Suivant, une fois qu'on est bien revenu sur cette
        // étape (plus aucune transition modale en cours).
    }

    /// <summary>Tap sur un chip d'équipement acheté - même recap qu'ailleurs dans l'app (voir
    /// WarbandEditDialogViewModel.ShowEquipmentDetail).</summary>
    [RelayCommand]
    private Task ShowRecruitEquipmentDetail(EquipmentPick pick) => _detailDialogs.ShowEquipmentDetailDialogAsync(pick.Item, pick.MaterialRule);

    /// <summary>Onglet Mutations d'une recrue (Possédés/Mutants, RecruitSlot.CanBuyMutations) - même
    /// principe que WarbandEditDialogViewModel.AddMutation : le coût s'ajoute au recrutement (livre, "+ the
    /// cost of mutations", voir HeroEquipmentTotalCost/HenchmanRecruitmentTotalCost), persisté à Terminer
    /// (ApplyRecruitmentAsync/ApplyHenchmanRecruitmentAsync).</summary>
    [RelayCommand]
    private async Task AddRecruitMutation(object target)
    {
        RecruitSlot? slot = target switch
        {
            WarriorNameSlot s => s,
            HenchmanGroupDraft g => g,
            _ => null
        };
        if (slot is null) return;

        var unitCount = slot is HenchmanGroupDraft henchmen ? henchmen.Count : 1;
        foreach (var mutation in await _mutationPicker.PickMutationsAsync(_warbandArchetypeId, EndOfGameTreasuryRemaining, unitCount))
            slot.Mutations.Add(mutation);
        NotifyTreasuryChanged();
        if (slot is WarriorNameSlot hero && RecruitHeroDetailError is not null) ValidateRecruitHeroDetailStep(hero);
    }

    [RelayCommand]
    private Task ShowRecruitMutationDetail(Mutation mutation) => _detailDialogs.ShowMutationDetailDialogAsync(mutation);

    [RelayCommand]
    private void RemoveRecruitMutation(Mutation mutation)
    {
        foreach (var slot in RecruitRows.SelectMany(r => r.NameSlots.Cast<RecruitSlot>().Concat(r.HenchmanGroupDrafts)))
        {
            if (!slot.Mutations.Remove(mutation)) continue;
            NotifyTreasuryChanged();
            return;
        }
    }

    /// <summary>Retire un EquipmentPick de quelle que collection le contient (Equipment d'un WarriorNameSlot
    /// ou d'un HenchmanGroupDraft) - identité de référence, pas besoin de savoir d'avance laquelle puisque
    /// chaque instance n'est ajoutée qu'à une seule collection. Même idiome que
    /// WarbandEditDialogViewModel.RemoveEquipment.</summary>
    [RelayCommand]
    private void RemoveRecruitEquipment(EquipmentPick pick)
    {
        foreach (var row in RecruitRows)
        {
            foreach (var slot in row.NameSlots)
            {
                if (slot.Equipment.Remove(pick))
                {
                    NotifyTreasuryChanged();
                    return;
                }
            }
            foreach (var group in row.HenchmanGroupDrafts)
            {
                if (group.Equipment.Remove(pick))
                {
                    NotifyTreasuryChanged();
                    return;
                }
            }
        }
    }

    // --- Validation par étape : nom de CE héros (RecruitHeroDetail, une étape par héros) et noms des
    // nouveaux groupes d'Hommes de main (RecruitHenchmenNames, tous ensemble après Équipement - le
    // nombre RÉEL de groupes n'est stable qu'une fois cette étape quittée, un Split peut en créer
    // d'autres). Les groupes déjà existants n'ont rien à nommer (déjà nommés). ---------------------------

    /// <summary>Nom requis pour CE héros précis (livre des règles - un nom par recrue), pré-rempli
    /// (ArchetypeLabel via IncrementRecruit) mais modifiable - ce garde-fou ne mord donc que si le joueur
    /// a vidé le champ après coup. Bloque aussi Suivant si la trésorerie est passée négative
    /// (IsTreasuryBlocked, 2026-09-23 - remplace l'ancienne dialog "fonds insuffisants" déclenchée juste
    /// après le picker, qui clignotait/restait bloquée à cause de la course de navigation modale - même
    /// correctif que ShowWeaponLimitWarningIfNeededAsync) : les deux checks restent indépendants, comme
    /// ValidateRareItemPurchaseStep.</summary>
    private bool ValidateRecruitHeroDetailStep(WarriorNameSlot slot)
    {
        var valid = true;
        if (string.IsNullOrWhiteSpace(slot.Name))
        {
            RecruitHeroDetailError = string.Format(Loc["WarbandsWarriorNameRequired"], slot.Row.Name);
            valid = false;
        }
        else if (RecruitmentRules.IsMissingMandatoryMutation(slot.Row.Archetype.MustStartWithMutation, slot.Mutations.Count))
        {
            // Mutant - "must start the game with one or more mutations", voir RecruitmentRules.
            RecruitHeroDetailError = string.Format(Loc["WarbandsMandatoryMutationMissing"], slot.Name.Trim());
            valid = false;
        }
        else
        {
            RecruitHeroDetailError = null;
        }

        return valid && !IsTreasuryBlocked;
    }

    [ObservableProperty]
    private string? recruitHeroDetailError;

    /// <summary>Un nom requis pour chaque nouveau groupe d'Hommes de main (livre des règles - "you will
    /// need to... name each Henchman group"), pré-rempli (nom d'archétype via IncrementHenchmanGroup/
    /// RenumberHenchmanGroupDrafts après un Split) mais modifiable.</summary>
    private bool ValidateRecruitHenchmenNamesStep()
    {
        foreach (var row in HenchmanRecruitRows)
        {
            foreach (var group in row.HenchmanGroupDrafts.Where(g => string.IsNullOrWhiteSpace(g.Name)))
            {
                RecruitHenchmenNamesError = string.Format(Loc["WarbandsWarriorNameRequired"], row.Name);
                return false;
            }
        }

        RecruitHenchmenNamesError = null;
        return true;
    }

    [ObservableProperty]
    private string? recruitHenchmenNamesError;
}
