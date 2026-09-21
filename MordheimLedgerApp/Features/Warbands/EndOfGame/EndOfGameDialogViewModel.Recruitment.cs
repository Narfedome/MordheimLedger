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
/// RecruitHenchmenCount/RecruitHenchmenEquipment/RecruitHenchmenNames dans EndOfGameDialogViewModel.cs's
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
/// WarbandDetailViewModel.EndOfGame.ApplyRecruitmentAsync, même moment que tout le reste du wizard.
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
public partial class EndOfGameDialogViewModel
{
    /// <summary>WarbandArchetype complet de CETTE bande (contrairement à _warbandArchetypeId/
    /// _warbandArchetypeName, déjà connus avant cette étape mais insuffisants ici) - nécessaire pour
    /// MaxWarriors (RecruitmentRules.CanRecruit). Chargé par l'appelant (WarbandDetailViewModel.EndOfGame)
    /// comme tout le reste des données de ce wizard, jamais paresseusement ici - voir sa propre doc de
    /// classe pour pourquoi cette étape n'a pas de chargement asynchrone à elle.</summary>
    private readonly WarbandArchetype _recruitableWarbandArchetype;

    /// <summary>WarriorArchetype recrutables pour CETTE bande - peuplés une seule fois au constructeur
    /// (voir recruitableWarriorArchetypes, chargé par l'appelant comme dramatisPersonaCatalog/
    /// localizedHiredSwords etc.) plutôt que paresseusement à l'entrée de cette étape : ce wizard ne charge
    /// jamais de données async après sa propre construction, Next()/Previous() restent volontairement
    /// synchrones.</summary>
    public ObservableCollection<WarriorRecruitRow> RecruitRows { get; } = new();

    public IEnumerable<WarriorRecruitRow> HeroRecruitRows => RecruitRows.Where(r => r.IsHero);

    /// <summary>Sous-ensemble de HeroRecruitRows effectivement recruté (Count &gt; 0) - source directe des
    /// étapes dynamiques StepKind.RecruitHeroDetail (une par NameSlot, voir EndOfGameDialogViewModel.cs's
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
    /// cette étape) - comparé à WarbandArchetype.MaxWarriors par RecruitmentRules.CanRecruit, même principe
    /// que WarbandEditDialogViewModel.TotalWarriorCount mais en partant d'un roster déjà existant plutôt
    /// que de zéro.</summary>
    private int TotalWarriorCountAfterRecruitment => WarriorRows.Sum(r => r.Warrior.HeadCount) + RecruitRows.Sum(r => r.Count);

    /// <summary>Effectif déjà recruté de CE type précis, avant même d'ouvrir cette étape - comparé à
    /// WarriorArchetype.MaxCount. Compte tous les guerriers vivants (WarriorRows exclut déjà les morts/
    /// retraités - voir WarbandDetailViewModel.EndOfGame's activeWarriorRows), pas seulement les Héros.</summary>
    private int ExistingCountForArchetype(int warriorArchetypeId) =>
        WarriorRows.Where(r => r.Warrior.WarriorArchetypeId == warriorArchetypeId).Sum(r => r.Warrior.HeadCount);

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
        foreach (var row in RecruitRows)
            row.CanIncrement = RecruitmentRules.CanRecruit(ExistingCountForArchetype(row.Archetype.Id) + row.Count, row.Archetype.MaxCount,
                TotalWarriorCountAfterRecruitment, _recruitableWarbandArchetype.MaxWarriors, isExistingWarband: false, EndOfGameTreasuryRemaining, row.Cost);
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
            ExistingHenchmanTopUps.Add(new ExistingHenchmanTopUp(row, archetypeRow));
        }
        RefreshHenchmanTopUpBreakdowns();
    }

    /// <summary>Recalcule et "pousse" le détail de coût (ExistingHenchmanTopUp.Breakdown) de CHAQUE groupe
    /// existant - appelée après tout Increment/DecrementHenchmanTopUp (un changement sur UN groupe peut
    /// affecter le détail d'un AUTRE, la réserve étant partagée et consommée dans l'ordre, voir
    /// GetTopUpBreakdown) et une première fois à la construction.</summary>
    private void RefreshHenchmanTopUpBreakdowns()
    {
        foreach (var topUp in ExistingHenchmanTopUps)
            topUp.Breakdown = GetTopUpBreakdown(topUp);
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
        topUp.AddCount++;
        RefreshHenchmanTopUpBreakdowns();
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

    /// <summary>Combien d'exemplaires de CET item+matériau la réserve de la bande contient - point de
    /// départ du calcul de chaque groupe existant (GetTopUpBreakdown), qui en consomme au fil de l'eau
    /// dans l'ordre d'ExistingHenchmanTopUps, ET point de départ de BuildAvailableReservePool (réserve
    /// disponible pour les NOUVELLES recrues à l'étape Recrutement). Reconstruit à chaque appel plutôt
    /// que mis en cache : _warbandInventory est un simple instantané figé (jamais modifié par ce wizard
    /// tant que Terminer n'a pas tourné), donc toujours cohérent sans état supplémentaire à synchroniser.
    /// Additionne PendingExplorationStashItems (retour utilisateur - "les items récupérés dans
    /// l'exploration chart ne sont pas comptabilisés dans le contexte du end of game pour le
    /// recrutement") : un objet trouvé PENDANT cette même Fin de Partie (étape Exploration, plus tôt dans
    /// le wizard) n'existe pas encore dans _warbandInventory (figé à l'OUVERTURE du wizard) tant que
    /// ApplyExplorationOutcomeAsync n'a pas réellement tourné, à Terminer - or celui-ci s'exécute
    /// justement AVANT ApplyHenchmanRecruitmentAsync dans le pipeline de Terminer, donc l'objet SERA bien
    /// disponible au moment réel du calcul. Additionne aussi PurchasedReserveItems (étape Achat/Vente,
    /// juste avant Recrutement - EndOfGameDialogViewModel.EquipmentTrading.cs) et soustrait la PORTION
    /// réserve de PendingSales (IsFromStash - une vente d'équipement DÉJÀ PORTÉ par un guerrier ne touche
    /// jamais ce pool, voir SellableEquipmentCandidate's own doc).</summary>
    private Dictionary<(int ItemId, int? MaterialRuleId), int> BuildStashPool()
    {
        var pool = _warbandInventory.GroupBy(w => (w.Item.Id, w.MaterialRule?.Id)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (item, materialRule, quantity) in PendingExplorationStashItems())
        {
            var key = (item.Id, materialRule?.Id);
            pool[key] = pool.GetValueOrDefault(key) + quantity;
        }
        foreach (var pick in PurchasedReserveItems)
        {
            var key = (pick.Item.Id, pick.MaterialRule?.Id);
            pool[key] = pool.GetValueOrDefault(key) + 1;
        }
        foreach (var sale in PendingSales.Where(c => c.IsFromStash))
        {
            var key = (sale.Item.Id, sale.MaterialRule?.Id);
            pool[key] = Math.Max(0, pool.GetValueOrDefault(key) - sale.Quantity);
        }
        return pool;
    }

    /// <summary>Réserve encore DISPONIBLE pour la prochaine recrue (Héros ou nouveau groupe d'Hommes de
    /// main) à l'étape Recrutement - part de BuildStashPool (pré-partie + Exploration + Achat - Vente),
    /// puis en retranche ce qui a déjà été réservé par les groupes EXISTANTS (top-up, rejoue le même
    /// calcul que GetTopUpBreakdown - dupliqué ici plutôt que refactorisé, GetTopUpBreakdown ne renvoie
    /// pas son pool final) ET par les picks déjà marqués FromReserve sur les Héros/nouveaux groupes de
    /// CETTE session (voir AddRecruitEquipment) - un même exemplaire ne peut jamais être compté deux fois,
    /// même principe que GetTopUpBreakdown pour les groupes existants entre eux.</summary>
    private Dictionary<(int ItemId, int? MaterialRuleId), int> BuildAvailableReservePool()
    {
        var pool = BuildStashPool();

        foreach (var topUp in ExistingHenchmanTopUps.Where(t => t.AddCount > 0))
        {
            var groupHeadCount = Math.Max(1, topUp.Row.Warrior.HeadCount);
            foreach (var equipment in topUp.CurrentEquipment)
            {
                var neededQty = topUp.AddCount * equipment.Quantity / groupHeadCount;
                var key = (equipment.Item.Id, equipment.MaterialRule?.Id);
                var consumed = Math.Min(pool.GetValueOrDefault(key), neededQty);
                if (consumed > 0) pool[key] = pool[key] - consumed;
            }
        }

        // Un pick FromReserve consomme 1 exemplaire pour un Héros (toujours seul), mais group.Count pour
        // un nouveau groupe d'Hommes de main (le lot entier vient de la réserve d'un coup - tout-ou-rien,
        // voir AddRecruitEquipment's own doc, pas de mélange partiel réserve/achat au sein d'un pick).
        foreach (var pick in HeroRecruitRows.SelectMany(r => r.NameSlots).SelectMany(s => s.Equipment).Where(p => p.FromReserve))
        {
            var key = (pick.Item.Id, pick.MaterialRule?.Id);
            var available = pool.GetValueOrDefault(key);
            if (available > 0) pool[key] = available - 1;
        }
        foreach (var group in HenchmanRecruitRows.SelectMany(r => r.HenchmanGroupDrafts))
        foreach (var pick in group.Equipment.Where(p => p.FromReserve))
        {
            var key = (pick.Item.Id, pick.MaterialRule?.Id);
            var consumed = Math.Min(pool.GetValueOrDefault(key), group.Count);
            if (consumed > 0) pool[key] = pool[key] - consumed;
        }

        return pool;
    }

    /// <summary>Détail de coût d'UN groupe existant (retour utilisateur - "il faut détailler le calcul au
    /// recrutement de vétéran") : recrutement + une ligne par type d'équipement du groupe (avec combien
    /// vient de la réserve, gratuit) + la surtaxe d'Expérience. Rejoue la consommation de réserve de TOUS
    /// les groupes qui précèdent CELUI-CI dans ExistingHenchmanTopUps (ordre fixe) avant de calculer ses
    /// propres lignes, pour rester cohérent d'un groupe à l'autre - un même exemplaire de réserve ne peut
    /// jamais être compté deux fois. HenchmanRecruitmentTotalCost (trésorerie) additionne simplement Total
    /// sur chaque groupe, dans le même ordre.</summary>
    public HenchmanTopUpCostBreakdown GetTopUpBreakdown(ExistingHenchmanTopUp topUp)
    {
        if (topUp.AddCount == 0)
            return new HenchmanTopUpCostBreakdown(0, Array.Empty<HenchmanEquipmentCostLine>(), 0, 0);

        var stashPool = BuildStashPool();
        List<HenchmanEquipmentCostLine>? lines = null;
        var stashUsedTotal = 0;

        foreach (var current in ExistingHenchmanTopUps.Where(t => t.AddCount > 0))
        {
            var isTarget = ReferenceEquals(current, topUp);
            if (isTarget) lines = new List<HenchmanEquipmentCostLine>();

            // Un groupe d'Hommes de main est UN SEUL Warrior (HeadCount = l'effectif, voir sa propre doc) -
            // WarriorEquipment.Quantity y est donc déjà le TOTAL pour tout le groupe (ex. 3 Épées pour 3
            // Guerriers), jamais une quantité "par modèle" comme pour un Héros/une munition (bug trouvé -
            // "on rajoute un guerrier... l'épée coute 30, hors l'épée coute 10" : neededQty = AddCount *
            // equipment.Quantity comptait Quantity une seconde fois en plus de AddCount, au lieu de
            // n'utiliser que la quantité PAR MODÈLE qu'elle représente déjà divisée par l'effectif actuel).
            // Diviser par l'effectif AVANT ce top-up (current.Row.Warrior.HeadCount, jamais encore muté ici
            // - la mutation réelle n'arrive qu'à Terminer) retrouve cette quantité par modèle.
            var groupHeadCount = Math.Max(1, current.Row.Warrior.HeadCount);

            foreach (var equipment in current.CurrentEquipment)
            {
                var neededQty = current.AddCount * equipment.Quantity / groupHeadCount;
                var key = (equipment.Item.Id, equipment.MaterialRule?.Id);
                var available = stashPool.GetValueOrDefault(key);
                var fromStash = Math.Min(available, neededQty);
                if (fromStash > 0) stashPool[key] = available - fromStash;
                var toBuy = neededQty - fromStash;
                // Dague gratuite (livre des règles - "in addition to his free dagger") : chaque recrue
                // ajoutée est un modèle NEUF qui n'en porte encore aucune, donc CHAQUE dague achetée ici
                // correspond à la dague personnelle gratuite d'une recrue différente - jamais gratuite si
                // un matériau a été choisi sur cette ligne (Gromril/Ithilmar = amélioration délibérée, voir
                // EquipmentPricing.IsFreeDaggerEligible).
                var isFreeDagger = equipment.Item.IsFreeDagger && equipment.MaterialRule is null;
                var cost = toBuy * EquipmentPricing.CalculateCost(equipment.Item.Cost, equipment.MaterialRule?.CostMultiplier, isFree: isFreeDagger);

                if (isTarget)
                {
                    // Nom + matériau (comme WarriorEquipment.NameDisplay), mais SANS son suffixe "xN" - ce
                    // N-là est la Quantity totale déjà existante du groupe, pas neededQty (le total
                    // ADDITIONNEL pour les recrues ajoutées, affiché séparément par cette ligne).
                    var materialSuffix = equipment.MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $" ({abbr})" : string.Empty;
                    lines!.Add(new HenchmanEquipmentCostLine($"{equipment.Item.Name}{materialSuffix}", neededQty, cost, fromStash));
                    stashUsedTotal += fromStash;
                }
            }

            if (isTarget) break;
        }

        return new HenchmanTopUpCostBreakdown(topUp.AddCount * topUp.ArchetypeCost, lines ?? new List<HenchmanEquipmentCostLine>(),
            topUp.AddCount * 2 * topUp.GroupExperience, stashUsedTotal);
    }

    /// <summary>Coût total des Hommes de main en attente - groupes existants (GetTopUpBreakdown, réserve
    /// déduite) additionnés dans l'ordre d'ExistingHenchmanTopUps, plus le recrutement ET l'équipement
    /// (étape RecruitHenchmenEquipment, choix au picker - réserve en priorité depuis l'étape Achat/Vente
    /// précédente, voir AddRecruitEquipment, sinon plein tarif) d'un éventuel nouveau groupe.
    /// EquipmentPick.Cost porte déjà le prix unitaire (0 si FromReserve) - même formule que
    /// WarbandEditDialogViewModel.TotalSpent (coût par exemplaire × effectif du groupe). Recalculé à
    /// chaque lecture, donc toujours cohérent entre cet aperçu (EndOfGameTreasuryRemaining) et
    /// l'application réelle à Terminer (WarbandDetailViewModel.EndOfGame.ApplyHenchmanRecruitmentAsync,
    /// même algorithme).</summary>
    private int HenchmanRecruitmentTotalCost() =>
        ExistingHenchmanTopUps.Where(t => t.AddCount > 0).Sum(t => GetTopUpBreakdown(t).Total)
        + HenchmanRecruitRows.Sum(r => r.HenchmanGroupDrafts.Sum(g => g.Count) * r.Cost)
        + HenchmanRecruitRows.SelectMany(r => r.HenchmanGroupDrafts).Sum(g => g.Equipment.Sum(e => e.Cost) * g.Count);

    /// <summary>Coût de l'équipement acheté pour les Héros recrutés cette session (étape RecruitHeroDetail,
    /// choix au picker - réserve en priorité, voir AddRecruitEquipment) - toujours ×1 (jamais de groupe
    /// côté Héros, contrairement aux Hommes de main).</summary>
    private int HeroEquipmentTotalCost() => HeroRecruitRows.SelectMany(r => r.NameSlots).Sum(s => s.Equipment.Sum(e => e.Cost));

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
    /// automatique). Même logique que WarbandEditDialogViewModel.AddEquipment (picker filtré par
    /// EquipmentListId/WarriorArchetypeId, choix de matériau pour les armes de corps à corps via un seul
    /// MaterialPickerDialog paginé, arrêt au premier objet trop cher), sans Compétences/Sorts (mode Bande
    /// existante uniquement, jamais le cas ici). commonOnly: true (livre des règles - "can only buy Common
    /// items... freely", les Objets Rares ont déjà leur propre étape plus tôt dans ce wizard). Réserve en
    /// priorité (2026-09-05, retour utilisateur - "si on faisait d'abord l'achat vente d'équipement, puis
    /// le recrutement avec l'assignation des équipements de la stash") : si BuildAvailableReservePool en
    /// contient déjà assez pour TOUT le lot (perUnitCost, l'effectif du groupe pour un Homme de main - 1
    /// pour un Héros), le pick est FromReserve (gratuit, les consomme) plutôt qu'acheté au plein tarif -
    /// simplification délibérée, pas de mélange partiel réserve/achat au sein d'un même pick (contrairement
    /// à GetTopUpBreakdown, qui gère ce mélange pour les groupes EXISTANTS via une ligne de coût détaillée
    /// séparée - un nouveau pick reste ici un choix unique au picker).</summary>
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

        var items = await _equipmentPicker.PickEquipmentAsync(_recruitableWarbandArchetype.Id, row.Archetype.EquipmentListId, row.Archetype.Id,
            EndOfGameTreasuryRemaining, perUnitCost, destination.Any(p => p.Item.IsFreeDagger), commonOnly: true);

        // Un seul dialog paginé pour toutes les armes de corps à corps du lot - voir
        // WarbandEditDialogViewModel.AddEquipment pour le détail de cette logique, reprise à l'identique.
        var meleeMaterials = new Queue<SpecialRule?>();
        var meleeItems = items.Where(i => i.Category == EquipmentCategory.MeleeWeapon).ToList();
        if (meleeItems.Count > 0)
        {
            var materialRules = (await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language))
                .Where(r => r.CostMultiplier.HasValue).ToList();
            if (materialRules.Count > 0)
            {
                var hasFreeDaggerSlot = destination.Any(p => p.Item.IsFreeDagger);
                var choices = new List<MaterialChoice>();
                foreach (var item in meleeItems)
                {
                    choices.Add(new MaterialChoice(item, materialRules, Loc["WarriorsMaterialNormal"], EquipmentPricing.IsFreeDaggerEligible(item.IsFreeDagger, hasFreeDaggerSlot)));
                    if (item.IsFreeDagger) hasFreeDaggerSlot = true;
                }
                var confirmed = await ShowDialogAsync(new MaterialPickerDialog(new MaterialPickerDialogViewModel(choices)));
                foreach (var choice in choices)
                    meleeMaterials.Enqueue(confirmed == true ? choice.SelectedMaterial : null);
            }
        }

        foreach (var equipmentItem in items)
        {
            var materialRule = equipmentItem.Category == EquipmentCategory.MeleeWeapon && meleeMaterials.Count > 0
                ? meleeMaterials.Dequeue()
                : null;
            var pick = new EquipmentPick(equipmentItem, materialRule)
            {
                IsFree = EquipmentPricing.IsFreeDaggerEligible(equipmentItem.IsFreeDagger, destination.Any(p => p.Item.IsFreeDagger)) && materialRule is null
            };

            if (!pick.IsFree)
            {
                var reserveKey = (equipmentItem.Id, materialRule?.Id);
                if (BuildAvailableReservePool().GetValueOrDefault(reserveKey) >= perUnitCost)
                    pick.FromReserve = true;
            }

            // Coût total si on achète maintenant (perUnitCost = l'effectif du groupe pour un Homme de
            // main, 1 pour un Héros) - sélection multiple : on s'arrête au premier objet trop cher plutôt
            // que de tout annuler, même logique que WarbandEditDialogViewModel.AddEquipment.
            if (EndOfGameTreasuryRemaining < pick.Cost * perUnitCost)
            {
                await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                break;
            }

            destination.Add(pick);
            NotifyTreasuryChanged();
        }

        // Avertissement non-bloquant (2 armes de corps à corps / 2 armes de tir différentes max par
        // guerrier, livre des règles) - voir WeaponLimits/WarbandEditDialogViewModel.AddEquipment.
        if (WeaponLimits.ExceedsLimits(destination.Select(p => p.Item)))
        {
            var warriorLabel = target switch
            {
                WarriorNameSlot { Name.Length: > 0 } nameSlot => nameSlot.Name,
                HenchmanGroupDraft group => group.Name,
                _ => row.Name
            };
            await ShowInfoAsync(Loc["WarbandsWeaponLimitWarningTitle"], string.Format(Loc["WarbandsWeaponLimitWarningMessage"], warriorLabel));
        }
    }

    /// <summary>Tap sur un chip d'équipement acheté - même recap qu'ailleurs dans l'app (voir
    /// WarbandEditDialogViewModel.ShowEquipmentDetail).</summary>
    [RelayCommand]
    private Task ShowRecruitEquipmentDetail(EquipmentPick pick) => _detailDialogs.ShowEquipmentDetailDialogAsync(pick.Item, pick.MaterialRule);

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
    /// a vidé le champ après coup.</summary>
    private bool ValidateRecruitHeroDetailStep(WarriorNameSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.Name))
        {
            RecruitHeroDetailError = string.Format(Loc["WarbandsWarriorNameRequired"], slot.Row.Name);
            return false;
        }

        RecruitHeroDetailError = null;
        return true;
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
