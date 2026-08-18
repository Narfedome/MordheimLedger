using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape Exploration (Séquence d'après-bataille, "Revenus" - voir Core.Rules.ExplorationChart
/// et Models.Library.ExplorationResult/ExplorationOutcome) : dés, sous-jet, test de caractéristique,
/// objet bonus, validation. Extrait de EndOfGameDialogViewModel.cs (2026-08-18, refactor de découpage,
/// voir CLAUDE.md) après deux bugs réels d'ordonnancement trouvés dans cette zone (Boutique/bonus,
/// statut Sick) - c'est le plus gros morceau du wizard, isolé pour que son périmètre soit visible d'un
/// coup d'œil plutôt que noyé dans le reste. La résolution des règles elle-même (quelle Outcome
/// s'applique) vit dans Core.Rules.ExplorationOutcomeResolver, testée ; ce fichier ne fait
/// qu'orchestrer l'UI (quel champ est lié à quoi, quand un jet a été saisi) et appeler ce resolver.
/// Aucun changement de comportement - pur déplacement de membres.</summary>
public partial class EndOfGameDialogViewModel
{
    // Un D6 par Héros survivant sans être hors de combat (jamais les Hommes de main) + 1D6 si la
    // bande a gagné, plafonné à 6 dés (voir Core.Rules.ExplorationChart - règle du livre confirmée
    // par l'utilisateur le 2026-08-17). Le nombre de dés ne peut varier qu'entre Result/HorsDeCombat
    // (déjà résolus quand on atteint cette étape) et le moment où on l'atteint, donc SyncExplorationDice
    // n'a besoin d'être appelée qu'en y entrant (OnStepIndexChanged) plutôt qu'à chaque frappe.
    public int SurvivingHeroCount => WarriorRows.Count(r => r.IsHero && !r.IsOutOfAction);
    public bool WonLastGame => ResultOptions.Count > 0 && SelectedResult == ResultOptions[0];
    public int ExplorationDiceCount => ExplorationChart.ComputeDiceCount(SurvivingHeroCount, WonLastGame);

    public ObservableCollection<ExplorationDieEntry> ExplorationDice { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplorationResult))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationSubRoll))]
    [NotifyPropertyChangedFor(nameof(ExplorationNoteText))]
    [NotifyPropertyChangedFor(nameof(BonusItemOutcome))]
    [NotifyPropertyChangedFor(nameof(HasBonusItem))]
    [NotifyPropertyChangedFor(nameof(BonusItem))]
    [NotifyPropertyChangedFor(nameof(ShowStatTest))]
    [NotifyPropertyChangedFor(nameof(StatTestFieldLabel))]
    // Steps() gagne/perd son étape ExplorationResult selon cette valeur (voir Steps) - le total affiché
    // par StepLabel et la visibilité du bouton "Suivant"/"Enregistrer" (IsLastStep) doivent donc suivre
    // en direct, pas seulement au prochain changement de StepIndex/WarriorRows (même classe de bug que
    // le HasExplorationResult manquant corrigé plus tôt : sans ces deux lignes, l'UI resterait figée sur
    // l'ancien total tant que rien d'autre ne la rafraîchit).
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    private ExplorationResult? triggeredExplorationResult;

    /// <summary>Un seul résultat peut être déclenché par jet (voir ExplorationChart.DetectMultiples) -
    /// null tant que tous les dés ne sont pas renseignés, ou si aucun doublon n'a été trouvé (issue
    /// normale de la table, pas une erreur).</summary>
    public bool HasExplorationResult => TriggeredExplorationResult is not null;

    /// <summary>Un résultat à plusieurs branches mutuellement exclusives (Groupe A, ex. Cadavre : 1-2
    /// po, 3 Dague, 4 Hache...) se départage par un sous-jet D6 - un résultat à une seule branche (ex.
    /// Masures en Ruine) ou dont aucune Outcome n'a de sous-jet n'en a pas besoin. Les résultats à choix
    /// du joueur (Groupe B, RollsIndependently) ne sont pas encore gérés par cette étape (à venir, voir
    /// le plan de séquencement) - ShowExplorationSubRoll reste false pour eux pour l'instant.</summary>
    public bool ShowExplorationSubRoll => TriggeredExplorationResult is { RollsIndependently: false } r
        && r.Outcomes.Count > 1 && r.Outcomes.All(o => o.SubRollMin.HasValue);

    [ObservableProperty]
    private string explorationSubRoll = string.Empty;

    [ObservableProperty]
    private string? explorationSubRollError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorationGold))]
    [NotifyPropertyChangedFor(nameof(IsExplorationItem))]
    [NotifyPropertyChangedFor(nameof(IsExplorationWyrdstone))]
    [NotifyPropertyChangedFor(nameof(IsExplorationNone))]
    [NotifyPropertyChangedFor(nameof(ExplorationNoteText))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationItemQuantityRoll))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationWyrdstoneRoll))]
    [NotifyPropertyChangedFor(nameof(ResolvedExplorationItem))]
    [NotifyPropertyChangedFor(nameof(ResolvedExplorationSecondaryItem))]
    [NotifyPropertyChangedFor(nameof(HasExplorationItemChoice))]
    [NotifyPropertyChangedFor(nameof(ExplorationItemChoicePrimaryItem))]
    [NotifyPropertyChangedFor(nameof(ExplorationItemChoiceAlternativeItem))]
    [NotifyPropertyChangedFor(nameof(ChosenExplorationItemName))]
    [NotifyPropertyChangedFor(nameof(BonusItemOutcome))]
    [NotifyPropertyChangedFor(nameof(HasBonusItem))]
    [NotifyPropertyChangedFor(nameof(BonusItem))]
    [NotifyPropertyChangedFor(nameof(StatTestSickHero))]
    private ExplorationOutcome? resolvedExplorationOutcome;

    /// <summary>ResolvedExplorationOutcome.EquipmentItemName + MaterialRuleName résolus (langue courante),
    /// enveloppés dans un WarbandEquipment jetable (jamais persisté - juste réutilisé pour son
    /// NameDisplay/Name "Épée (O)", exactement le même rendu qu'un objet en Gromril/Ithilmar dans
    /// l'inventaire) plutôt qu'un simple EquipmentItem qui perdrait le matériau à l'affichage. La carte
    /// s'affiche en ChipView tapable (icône de catégorie + popup détail via ShowExplorationItemDetail),
    /// même langage d'interaction que toute autre référence Équipement dans l'app. Passe par
    /// ChosenExplorationItemName plutôt que directement EquipmentItemName pour tenir compte d'un éventuel
    /// choix du joueur (voir HasExplorationItemChoice).</summary>
    public WarbandEquipment? ResolvedExplorationItem =>
        BuildDisplayItem(ChosenExplorationItemName, ResolvedExplorationOutcome?.MaterialRuleName);

    /// <summary>Second objet du même branch (ex. Charrette Renversée : Épée + Dague, voir
    /// ExplorationOutcome.SecondaryEquipmentItemName) - null pour tout le reste de la table. Même
    /// MaterialRuleName que ResolvedExplorationItem (un seul matériau pour les deux, trouvés ensemble).</summary>
    public WarbandEquipment? ResolvedExplorationSecondaryItem =>
        BuildDisplayItem(ResolvedExplorationOutcome?.SecondaryEquipmentItemName, ResolvedExplorationOutcome?.MaterialRuleName);

    /// <summary>Vrai seulement pour une branche à choix (ex. Armurerie 1-2 : "D3 Boucliers ou Rondaches,
    /// au choix") - AlternativeEquipmentItemName est alors non-null, et le XAML remplace le ChipView unique
    /// par 2 RadioButton (plus direct qu'un Picker pour un choix binaire - revenu sur ce point le
    /// 2026-08-18, un Picker impose un tap "pour ouvrir" en plus alors que les deux options tiennent
    /// très bien affichées d'un coup).</summary>
    public bool HasExplorationItemChoice => ResolvedExplorationOutcome?.AlternativeEquipmentItemName is not null;

    /// <summary>Objet "principal" (EquipmentItemName) affiché à côté du premier RadioButton - même
    /// WarbandEquipment jetable que ResolvedExplorationItem/BuildDisplayItem (icône de catégorie + nom),
    /// pour que chaque option de choix se présente comme n'importe quel autre chip équipement de l'app,
    /// pas un simple Label texte. Rendu comme icône+Label FRÈRE du RadioButton dans le XAML plutôt que
    /// dans RadioButton.Content lui-même : un View arbitraire posé en Content ne s'affichait pas côté
    /// Windows/WinUI (repéré le 2026-08-18 - le RadioButton restait vide, le contenu flottait ailleurs
    /// sur l'écran), le ControlTemplate natif de la plateforme n'attend visiblement qu'une chaîne.</summary>
    public WarbandEquipment? ExplorationItemChoicePrimaryItem =>
        BuildDisplayItem(ResolvedExplorationOutcome?.EquipmentItemName, ResolvedExplorationOutcome?.MaterialRuleName);

    /// <summary>Même chose pour AlternativeEquipmentItemName, le second RadioButton.</summary>
    public WarbandEquipment? ExplorationItemChoiceAlternativeItem =>
        BuildDisplayItem(ResolvedExplorationOutcome?.AlternativeEquipmentItemName, ResolvedExplorationOutcome?.MaterialRuleName);

    /// <summary>Nom ANGLAIS de l'objet coché - lié à RadioButtonGroup.SelectedValue (voir le XAML), pas
    /// un index : chaque RadioButton a directement EquipmentItemName/AlternativeEquipmentItemName comme
    /// Value, donc cette propriété EST déjà le nom à utiliser, aucune traduction supplémentaire requise
    /// côté ChosenExplorationItemName. EquipmentItemName (l'objet "principal") par défaut, jamais forcé à
    /// un choix actif (même logique que SelectedResult à l'étape Résultat).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedExplorationItem))]
    private string? selectedExplorationItemName;

    /// <summary>L'EquipmentItemName réellement retenu une fois le choix du joueur pris en compte (voir
    /// HasExplorationItemChoice) - c'est ce nom, pas ExplorationOutcome.EquipmentItemName brut, que
    /// WarbandDetailViewModel.EndOfGame doit ajouter à l'inventaire.</summary>
    public string? ChosenExplorationItemName => HasExplorationItemChoice
        ? SelectedExplorationItemName
        : ResolvedExplorationOutcome?.EquipmentItemName;

    private WarbandEquipment? BuildDisplayItem(string? itemName, string? materialRuleName)
    {
        if (itemName is not { } name) return null;
        var item = _equipmentItemsByEnglishName.GetValueOrDefault(name);
        if (item is null) return null;

        var materialRule = materialRuleName is { } ruleName ? _specialRulesByEnglishName.GetValueOrDefault(ruleName) : null;
        return new WarbandEquipment { Item = item, MaterialRule = materialRule };
    }

    /// <summary>Texte affiché pour une branche Kind.None (voir IsExplorationNone) - le Note de la
    /// branche retenue (ex. "Skavens : vente aux agents du Clan Eshin"), ou à défaut le nom du résultat
    /// déclenché si la branche n'en porte pas.</summary>
    public string ExplorationNoteText => ResolvedExplorationOutcome?.Note ?? TriggeredExplorationResult?.Name ?? string.Empty;

    public bool IsExplorationGold => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold;
    public bool IsExplorationItem => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Item;
    public bool IsExplorationWyrdstone => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Wyrdstone;

    /// <summary>Boutique (2,2) est le seul cas de la table où une branche Auto (ici Or, "D6 po") et une
    /// branche à sous-jet (ici Objet, "sur un 1, en plus, un Porte-bonheur") coexistent sur LE MÊME dé -
    /// "en plus" et non une branche alternative. Dérivée directement d'ExplorationGoldAmount (déjà le
    /// jet du joueur pour l'or) plutôt que de lui redemander un second jet pour la même information.
    /// Aucun autre résultat de la table n'a cette forme (Puits/Bâtiment Éventré n'ont qu'une branche
    /// Wyrdstone seule) - pas généralisé au-delà de ce cas réel.
    ///
    /// Bug corrigé le 2026-08-18 : la condition d'origine ne vérifiait que TriggeredExplorationResult,
    /// donc pour Cadavre (branches Or/Dague/Hache/Épée/Armure toutes à sous-jet exclusif, voir
    /// ShowExplorationSubRoll) un jet d'or de "4" déclenchait à tort le bonus Hache (sous-jet 4 de
    /// Cadavre) alors que ces deux dés n'ont AUCUN rapport pour ce résultat - seul Boutique réutilise
    /// intentionnellement le même dé pour les deux. Logique déplacée vers Core.Rules.
    /// ExplorationOutcomeResolver.ResolveBonusItemOutcome, testée, qui exige explicitement que la
    /// branche Or résolue soit elle-même la branche Auto - impossible à réintroduire par erreur.</summary>
    public ExplorationOutcome? BonusItemOutcome => TriggeredExplorationResult is not null && int.TryParse(ExplorationGoldAmount, out var roll)
        ? ExplorationOutcomeResolver.ResolveBonusItemOutcome(TriggeredExplorationResult, ResolvedExplorationOutcome, roll)
        : null;

    public bool HasBonusItem => BonusItemOutcome is not null;

    /// <summary>BonusItemOutcome.EquipmentItemName + MaterialRuleName résolus - même besoin que
    /// ResolvedExplorationItem.</summary>
    public WarbandEquipment? BonusItem =>
        BuildDisplayItem(BonusItemOutcome?.EquipmentItemName, BonusItemOutcome?.MaterialRuleName);

    [RelayCommand]
    private Task ShowExplorationItemDetail(WarbandEquipment item) => _detailDialogs.ShowEquipmentDetailDialogAsync(item.Item, item.MaterialRule);

    /// <summary>Sauf indication contraire du livre (ex. Forge : "D3 Hallebardes"), on ne trouve qu'un
    /// seul exemplaire d'un objet - ItemQuantityFormula vaut alors "1", une quantité fixe et non un jet
    /// (voir ApplyResolvedOutcome, qui la renseigne directement sans rien demander au joueur). Le dé de
    /// relance (AutoRollExplorationItemQuantityCommand) et le champ ne sont utiles que si la formule est
    /// un vrai jet ("D3", "D6"...).</summary>
    public bool ShowExplorationItemQuantityRoll =>
        ResolvedExplorationOutcome?.ItemQuantityFormula?.Contains('D', StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Même idée que ShowExplorationItemQuantityRoll côté Wyrdstone (ex. Puits : toujours "1"
    /// pierre, pas un jet) - le Bâtiment Éventré/La Fosse ont de vraies formules ("D3"/"D6+1") et
    /// gardent leur champ + dé normalement.</summary>
    public bool ShowExplorationWyrdstoneRoll =>
        ResolvedExplorationOutcome?.GoldFormula?.Contains('D', StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Branche retenue sans effet trésorerie/inventaire (ex. Traînard/"autres bandes",
    /// Charrette Renversée 5-6) - reste purement informatif (Note ou Description du résultat), juste
    /// consigné dans l'Historique à la sauvegarde (voir WarbandDetailViewModel.EndOfGame) plutôt que
    /// silencieusement perdu.</summary>
    public bool IsExplorationNone => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.None;

    // --- Test de caractéristique (Puits/Endurance, Taverne et Bâtiment Éventré/Commandement) --------
    //
    // Choisir un Héros et comparer un D6 à une de ses stats pour départager Réussite/Échec - une autre
    // façon de choisir la branche résolue (ResolvedExplorationOutcome), au même niveau que le sous-jet
    // classique (ShowExplorationSubRoll) ou la branche Auto seule. Comparer un jet déjà saisi à une
    // stat déjà connue est de l'arithmétique, pas une décision aléatoire prise à la place du joueur -
    // contrairement au tirage lui-même (jamais automatique, voir ExplorationGoldAmount et consorts).
    public bool ShowStatTest => TriggeredExplorationResult?.StatTestField is not null;

    public List<WarriorOutcomeRow> StatTestEligibleHeroes => WarriorRows.Where(r => r.IsHero && !r.IsDead).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatTestStatValue))]
    [NotifyPropertyChangedFor(nameof(StatTestSickHero))]
    private WarriorOutcomeRow? statTestHero;

    [ObservableProperty]
    private string statTestRoll = string.Empty;

    [ObservableProperty]
    private string? statTestError;

    /// <summary>Valeur de la stat testée pour le Héros choisi - affichée à côté du jet pour que le
    /// joueur puisse comparer sans avoir à rouvrir la fiche du guerrier.</summary>
    public int? StatTestStatValue => StatTestHero is null || TriggeredExplorationResult?.StatTestField is not { } statField ? null
        // "statField", pas "field" - "field" est un mot-clé contextuel (backing field des propriétés
        // auto-implémentées) depuis C# 13 : l'utiliser comme nom de variable de motif DANS un getter de
        // propriété capture silencieusement le mauvais symbole (CS0266 sur les branches du switch).
        : statField switch
        {
            ExplorationStatField.Toughness => StatTestHero.Warrior.Toughness,
            ExplorationStatField.Leadership => StatTestHero.Warrior.Leadership,
            _ => null
        };

    public string StatTestFieldLabel => TriggeredExplorationResult?.StatTestField switch
    {
        ExplorationStatField.Toughness => Loc["EndOfGameStatFieldToughness"],
        ExplorationStatField.Leadership => Loc["EndOfGameStatFieldLeadership"],
        _ => string.Empty
    };

    partial void OnStatTestHeroChanged(WarriorOutcomeRow? value) => ResolveStatTest();

    partial void OnStatTestRollChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) StatTestError = null;
        ResolveStatTest();
    }

    private void ResolveStatTest()
    {
        ResolvedExplorationOutcome = null;
        ExplorationGoldAmount = string.Empty;
        ExplorationItemQuantity = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;
        ExplorationAmountError = null;

        if (TriggeredExplorationResult?.StatTestField is null || StatTestHero is null
            || StatTestStatValue is not { } statValue || !int.TryParse(StatTestRoll, out var roll))
            return;

        var outcome = ExplorationOutcomeResolver.ResolveStatTestOutcome(TriggeredExplorationResult, roll, statValue);
        if (outcome is not null) ApplyResolvedOutcome(outcome);
    }

    [RelayCommand]
    private void AutoRollStatTest() => StatTestRoll = ExplorationChart.RollDie().ToString();

    /// <summary>Guerrier à marquer WarriorStatus.Sick à la sauvegarde (voir WarbandDetailViewModel.
    /// EndOfGame) - seul le Puits en a besoin pour l'instant (ExplorationOutcome.CausesSickness),
    /// Taverne/Bâtiment Éventré n'ont pas de conséquence de ce genre en cas d'échec.</summary>
    public WarriorOutcomeRow? StatTestSickHero => ResolvedExplorationOutcome?.CausesSickness == true ? StatTestHero : null;

    /// <summary>Montant d'or - jamais rempli automatiquement dès la branche retenue (revenu sur ce point
    /// le 2026-08-17 : même idiome que tous les autres jets de ce wizard, l'appli ne décide jamais à la
    /// place du joueur - vide tant qu'il n'a pas tapé son jet physique ou cliqué le dé, voir
    /// AutoRollExplorationGold). Ce même jet alimente aussi BonusItemOutcome (voir sa doc) - la relire à
    /// chaque frappe est donc nécessaire, pas seulement décorative.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BonusItemOutcome))]
    [NotifyPropertyChangedFor(nameof(HasBonusItem))]
    [NotifyPropertyChangedFor(nameof(BonusItem))]
    private string explorationGoldAmount = string.Empty;

    [ObservableProperty]
    private string explorationItemQuantity = string.Empty;

    /// <summary>Même principe que ExplorationGoldAmount, pour une branche Kind.Wyrdstone (ex. Puits,
    /// Bâtiment Éventré, La Fosse) - GoldFormula est réutilisé tel quel comme formule de pierres
    /// magiques (voir ExplorationOutcome.GoldFormula).</summary>
    [ObservableProperty]
    private string explorationWyrdstoneAmount = string.Empty;

    /// <summary>Même principe que RollError - posé uniquement par ValidateExplorationResultStep si le
    /// montant Or/Objet/pierre(s) magique(s) de la branche résolue est encore vide, effacé dès qu'il est
    /// renseigné. Un seul champ partagé : IsExplorationGold/Item/Wyrdstone sont mutuellement exclusifs,
    /// un seul des trois est jamais visible à la fois.</summary>
    [ObservableProperty]
    private string? explorationAmountError;

    partial void OnExplorationGoldAmountChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) ExplorationAmountError = null; }
    partial void OnExplorationItemQuantityChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) ExplorationAmountError = null; }
    partial void OnExplorationWyrdstoneAmountChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) ExplorationAmountError = null; }

    private void SyncExplorationDice()
    {
        var count = ExplorationDiceCount;
        while (ExplorationDice.Count < count)
        {
            var entry = new ExplorationDieEntry(ExplorationDice.Count + 1);
            entry.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExplorationDieEntry.ManualRoll)) ResolveExplorationResult();
            };
            ExplorationDice.Add(entry);
        }
        while (ExplorationDice.Count > count)
            ExplorationDice.RemoveAt(ExplorationDice.Count - 1);
    }

    /// <summary>Recalcule le résultat déclenché dès que tous les dés d'Exploration sont renseignés -
    /// réinitialise tout l'état en aval (sous-jet, branche résolue, or/objet) à chaque nouveau tirage
    /// plutôt que de laisser une résolution obsolète affichée.</summary>
    private void ResolveExplorationResult()
    {
        TriggeredExplorationResult = null;
        ExplorationSubRoll = string.Empty;
        ExplorationSubRollError = null;
        ResolvedExplorationOutcome = null;
        ExplorationGoldAmount = string.Empty;
        ExplorationItemQuantity = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;
        ExplorationAmountError = null;
        StatTestHero = null;
        StatTestRoll = string.Empty;
        StatTestError = null;

        if (ExplorationDice.Any(d => d.Value is null)) return;

        var multiple = ExplorationChart.DetectMultiples(ExplorationDice.Select(d => d.Value!.Value).ToList());
        if (multiple is null) return;

        TriggeredExplorationResult = _explorationResults
            .FirstOrDefault(r => r.DiceCount == multiple.Value.DiceCount && r.Value == multiple.Value.Value);
        if (TriggeredExplorationResult is null) return;

        // Une branche Auto (sans sous-jet) se résout tout de suite, qu'elle soit seule (ex. Masures en
        // Ruine) ou accompagnée d'une branche à sous-jet optionnelle sur le MÊME dé (ex. Boutique - voir
        // BonusItemOutcome, qui se déduit du jet d'or plutôt que d'en redemander un second). Ce n'est
        // que quand TOUTES les branches ont un sous-jet (ex. Cadavre, mutuellement exclusives) que
        // ShowExplorationSubRoll prend le relais ; un résultat à test de caractéristique (Puits...)
        // attend le choix du Héros + son jet avant de résoudre quoi que ce soit (voir ResolveStatTest) -
        // ResolveAutoOutcome renvoie null pour lui, voir Core.Rules.ExplorationOutcomeResolver.
        var autoOutcome = ExplorationOutcomeResolver.ResolveAutoOutcome(TriggeredExplorationResult);
        if (autoOutcome is not null) ApplyResolvedOutcome(autoOutcome);
    }

    partial void OnExplorationSubRollChanged(string value)
    {
        ExplorationSubRollError = null;
        ResolvedExplorationOutcome = null;
        ExplorationGoldAmount = string.Empty;
        ExplorationItemQuantity = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;
        ExplorationAmountError = null;

        if (TriggeredExplorationResult is null || !int.TryParse(value, out var roll)) return;

        var outcome = ExplorationOutcomeResolver.ResolveSubRollOutcome(TriggeredExplorationResult, roll);
        if (outcome is not null) ApplyResolvedOutcome(outcome);
    }

    /// <summary>Retient la branche - ne tire plus rien automatiquement pour Or/Objet-à-jet/Pierres
    /// magiques (revenu sur ce point le 2026-08-17) : le joueur tape son jet physique ou clique le dé
    /// (AutoRollExplorationGold/ItemQuantity/Wyrdstone), jamais un tirage silencieux à la résolution de
    /// la branche. Seule exception : une quantité d'objet FIXE ("1", pas un jet - voir
    /// ShowExplorationItemQuantityRoll) se renseigne directement, ce n'est pas un hasard à faire trancher
    /// au joueur.</summary>
    private void ApplyResolvedOutcome(ExplorationOutcome outcome)
    {
        ResolvedExplorationOutcome = outcome;
        SelectedExplorationItemName = outcome.EquipmentItemName;
        if (outcome.Kind == ExplorationOutcomeKind.Item && outcome.ItemQuantityFormula is { } itemFormula
            && !itemFormula.Contains('D', StringComparison.OrdinalIgnoreCase))
            ExplorationItemQuantity = itemFormula;
        else if (outcome.Kind == ExplorationOutcomeKind.Wyrdstone && outcome.GoldFormula is { } wyrdstoneFormula
            && !wyrdstoneFormula.Contains('D', StringComparison.OrdinalIgnoreCase))
            ExplorationWyrdstoneAmount = wyrdstoneFormula;
    }

    [RelayCommand]
    private void AutoRollExplorationDie(ExplorationDieEntry entry) => entry.ManualRoll = ExplorationChart.RollDie().ToString();

    [RelayCommand]
    private void AutoRollExplorationSubRoll() => ExplorationSubRoll = ExplorationChart.RollDie().ToString();

    // Le montant Or/pierre(s) magique(s)/quantité d'objet reste vide tant que le joueur n'a pas tapé son
    // jet physique ou cliqué l'un de ces trois dés - même idiome que tous les autres jets du wizard
    // (ManualRoll, ExplorationSubRoll...), aucun tirage automatique à la résolution de la branche.
    [RelayCommand]
    private void AutoRollExplorationGold()
    {
        if (ResolvedExplorationOutcome?.GoldFormula is { } formula) ExplorationGoldAmount = DiceFormula.Roll(formula).ToString();
    }

    [RelayCommand]
    private void AutoRollExplorationItemQuantity()
    {
        if (ResolvedExplorationOutcome?.ItemQuantityFormula is { } formula) ExplorationItemQuantity = DiceFormula.Roll(formula).ToString();
    }

    [RelayCommand]
    private void AutoRollExplorationWyrdstone()
    {
        if (ResolvedExplorationOutcome?.GoldFormula is { } formula) ExplorationWyrdstoneAmount = DiceFormula.Roll(formula).ToString();
    }

    /// <summary>Bloque tant que les dés d'Exploration ne sont pas tous renseignés, et - si le résultat
    /// déclenché a plusieurs branches à choix exclusif (Groupe A, ex. Cadavre) - tant que le sous-jet
    /// qui les départage n'a pas résolu de branche. Un jet qui ne déclenche rien (pas de doublon) n'a
    /// rien de plus à valider (l'étape ExplorationResult n'existe même pas, voir Steps).</summary>
    private bool ValidateExplorationRollStep()
    {
        var valid = true;
        foreach (var die in ExplorationDice)
            valid &= CheckRoll(die.Value is null, () => die.RollError = Loc["EndOfGameRollRequired"]);
        return valid;
    }

    /// <summary>Bloque tant que le sous-jet (s'il y en a un) n'a pas résolu de branche, puis tant que le
    /// montant Or/Objet/pierre(s) magique(s) de la branche résolue est vide - jamais auto-rempli (voir
    /// ApplyResolvedOutcome), donc à valider comme n'importe quel autre jet de ce wizard. Une branche
    /// Kind.None (rien à saisir) ou l'absence de branche (ex. Catacombes, aucune Outcome du tout) n'ont
    /// rien de plus à valider.</summary>
    private bool ValidateExplorationResultStep()
    {
        if (ShowExplorationSubRoll && !CheckRoll(ResolvedExplorationOutcome is null, () => ExplorationSubRollError = Loc["EndOfGameRollRequired"]))
            return false;

        if (ShowStatTest && !CheckRoll(ResolvedExplorationOutcome is null, () => StatTestError = Loc["EndOfGameRollRequired"]))
            return false;

        var amountMissing = ResolvedExplorationOutcome?.Kind switch
        {
            ExplorationOutcomeKind.Gold => string.IsNullOrWhiteSpace(ExplorationGoldAmount),
            ExplorationOutcomeKind.Item => string.IsNullOrWhiteSpace(ExplorationItemQuantity),
            ExplorationOutcomeKind.Wyrdstone => string.IsNullOrWhiteSpace(ExplorationWyrdstoneAmount),
            _ => false
        };
        return CheckRoll(amountMissing == true, () => ExplorationAmountError = Loc["EndOfGameRollRequired"]);
    }
}
