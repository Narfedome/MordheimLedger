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

    /// <summary>Dés bonus depuis l'équipement porté par les guerriers encore debout (ex. l'Œil
    /// Omniscient de Numas - voir Core.Rules.ExplorationDiceBonus) - premier vrai usage du paramètre
    /// bonusDice de ComputeDiceCount, jusqu'ici toujours appelé à 0. Ne compte PAS
    /// _pendingExplorationBonusDie (Traînard) : contrairement à l'Œil de Numas ("lancez deux dés au lieu
    /// d'un", un vrai dé de plus gardé), le texte du Traînard est "lancez un dé de PLUS que d'habitude et
    /// écartez-en un au choix" - un dé physique en trop que le joueur écarte lui-même avant de saisir ses
    /// valeurs finales, donc le nombre de dés GARDÉS (ExplorationDiceCount) ne change pas du tout ; seul
    /// un rappel textuel a du sens ici (voir ShowPendingExplorationBonusDieReminder plus bas).</summary>
    public int ExplorationDiceCount => ExplorationChart.ComputeDiceCount(SurvivingHeroCount, WonLastGame,
        ExplorationDiceBonus.EffectiveBonusDice(WarriorRows.Where(r => !r.IsOutOfAction).Select(r => r.Warrior)));

    /// <summary>Rappel purement textuel (voir ExplorationDiceCount pour pourquoi ça ne change PAS le
    /// nombre de dés affichés) - affiché une fois à l'étape du jet d'Exploration.</summary>
    public bool ShowPendingExplorationBonusDieReminder => _pendingExplorationBonusDie;

    /// <summary>Entrée des Catacombes (voir Warband.HasCatacombReroll) : contrairement au rappel
    /// ci-dessus (consommé une fois montré), celui-ci reste affiché à CHAQUE Fin de Partie une fois
    /// acquis, jamais consommé - purement informatif, le joueur relance lui-même le dé physique de son
    /// choix et retape la nouvelle valeur, aucune logique de relance dans l'app (simplification demandée
    /// explicitement par l'utilisateur, 2026-08-21).</summary>
    public bool ShowCatacombRerollReminder => _hasCatacombReroll;

    public ObservableCollection<ExplorationDieEntry> ExplorationDice { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplorationResult))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationSubRoll))]
    [NotifyPropertyChangedFor(nameof(ExplorationNoteText))]
    [NotifyPropertyChangedFor(nameof(IsWarbandConditionedResult))]
    [NotifyPropertyChangedFor(nameof(ExplorationResultDescriptionText))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationNoteInBranchSection))]
    [NotifyPropertyChangedFor(nameof(BonusItemOutcome))]
    [NotifyPropertyChangedFor(nameof(HasBonusItem))]
    [NotifyPropertyChangedFor(nameof(BonusItem))]
    [NotifyPropertyChangedFor(nameof(ShowStatTest))]
    [NotifyPropertyChangedFor(nameof(ShowStatTestHeroPicker))]
    [NotifyPropertyChangedFor(nameof(StatTestLeaderUnavailable))]
    [NotifyPropertyChangedFor(nameof(StatTestAutoPasses))]
    [NotifyPropertyChangedFor(nameof(ShowStatTestRoll))]
    [NotifyPropertyChangedFor(nameof(StatTestHeroDisplayPrefix))]
    [NotifyPropertyChangedFor(nameof(StatTestFieldLabel))]
    [NotifyPropertyChangedFor(nameof(StatTestRollPlaceholder))]
    [NotifyPropertyChangedFor(nameof(ShowDoubleRollCheck))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationGoldRoll))]
    [NotifyPropertyChangedFor(nameof(ShowBonusStatTest))]
    [NotifyPropertyChangedFor(nameof(BonusStatTestFieldLabel))]
    [NotifyPropertyChangedFor(nameof(BonusStatTestRollPlaceholder))]
    [NotifyPropertyChangedFor(nameof(BonusStatTestStatValue))]
    [NotifyPropertyChangedFor(nameof(BonusStatTestOutcome))]
    [NotifyPropertyChangedFor(nameof(HasBonusStatTestOutcome))]
    [NotifyPropertyChangedFor(nameof(BonusStatTestItem))]
    [NotifyPropertyChangedFor(nameof(ShowSentHeroPicker))]
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

    // --- Héros envoyé (La Fosse) ----------------------------------------------------------------
    //
    // Le seul résultat où le joueur choisit d'exposer un Héros à un risque AVANT même de savoir ce que
    // le sous-jet donnera (voir ExplorationResult.RequiresSentHero) - contrairement à StatTestHero
    // (Puits), il n'y a aucune stat comparée, juste "qui part explorer". Envoyer quelqu'un est
    // OPTIONNEL ("si vous le souhaitez") : ne rien choisir laisse simplement le sous-jet caché
    // (ShowExplorationSubRoll), sans forcer de décision.

    public bool ShowSentHeroPicker => TriggeredExplorationResult?.RequiresSentHero == true;

    public List<WarriorOutcomeRow> SentHeroEligibleHeroes => WarriorRows.Where(r => r.IsHero && !r.IsDead).ToList();

    /// <summary>Wraps the Picker's actual items - a leading "Passer son chemin" pseudo-option (Hero
    /// null) followed by every eligible Hero. Retour explicite de l'utilisateur : un Picker vide/placeholder
    /// ne communiquait pas clairement que "ne rien choisir" est une réponse complète et valide ("si vous
    /// le souhaitez") plutôt qu'un champ oublié - la faire apparaître comme une option normale du Picker,
    /// sélectionnée par défaut, lève l'ambiguïté.</summary>
    public List<SentHeroOption> SentHeroOptions =>
        new List<SentHeroOption> { new(Loc["EndOfGamePassByOption"], null) }
            .Concat(SentHeroEligibleHeroes.Select(h => new SentHeroOption(h.Name, h)))
            .ToList();

    public sealed record SentHeroOption(string DisplayName, WarriorOutcomeRow? Hero);

    [ObservableProperty]
    private SentHeroOption? selectedSentHeroOption;

    partial void OnSelectedSentHeroOptionChanged(SentHeroOption? value) => SentHero = value?.Hero;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowExplorationSubRoll))]
    private WarriorOutcomeRow? sentHero;

    /// <summary>Héros à marquer WarriorStatus.Dead à la sauvegarde (voir WarbandDetailViewModel.
    /// EndOfGame) - seule La Fosse en a besoin pour l'instant (ExplorationOutcome.CausesDeath), même
    /// idiome que StatTestSickHero pour le Puits.</summary>
    public WarriorOutcomeRow? PitDevouredHero => ResolvedExplorationOutcome?.CausesDeath == true ? SentHero : null;

    /// <summary>Un résultat à plusieurs branches mutuellement exclusives (Groupe A, ex. Cadavre : 1-2
    /// po, 3 Dague, 4 Hache...) se départage par un sous-jet D6 - un résultat à une seule branche (ex.
    /// Masures en Ruine) ou dont aucune Outcome n'a de sous-jet n'en a pas besoin. Les résultats à choix
    /// du joueur (Groupe B, RollsIndependently) ne sont pas encore gérés par cette étape (à venir, voir
    /// le plan de séquencement) - ShowExplorationSubRoll reste false pour eux pour l'instant. Pour un
    /// résultat RequiresSentHero (ex. La Fosse), reste caché tant qu'aucun Héros n'a été envoyé - "si
    /// vous le souhaitez" : refuser d'envoyer quelqu'un est une issue valide, pas d'erreur bloquante.</summary>
    public bool ShowExplorationSubRoll => TriggeredExplorationResult is { RollsIndependently: false } r
        && r.Outcomes.Count > 1 && r.Outcomes.All(o => o.SubRollMin.HasValue)
        && (!r.RequiresSentHero || SentHero is not null);

    /// <summary>Un résultat gated par un double au 2D6 (voir ExplorationResult.RequiresDoubleRoll, seul
    /// cas actuel : Maison du Marchand) montre 2 champs de dé au lieu du sous-jet classique - la seule
    /// façon dont ce résultat précis départage ses branches, jamais en même temps que
    /// ShowExplorationSubRoll/ShowStatTest.</summary>
    public bool ShowDoubleRollCheck => TriggeredExplorationResult?.RequiresDoubleRoll == true;

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
    [NotifyPropertyChangedFor(nameof(ExplorationResultDescriptionText))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationNoteInBranchSection))]
    [NotifyPropertyChangedFor(nameof(ResolvedFreeHenchman))]
    [NotifyPropertyChangedFor(nameof(HasDistributedHeroExperienceGrant))]
    [NotifyPropertyChangedFor(nameof(DistributedExperienceRemaining))]
    [NotifyPropertyChangedFor(nameof(HasOptionalEquippedHenchmanGrant))]
    [NotifyPropertyChangedFor(nameof(HasWeaponBlessingGrant))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationItemQuantityRoll))]
    [NotifyPropertyChangedFor(nameof(ShowExplorationItemFoundValueRoll))]
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
    [NotifyPropertyChangedFor(nameof(PitDevouredHero))]
    [NotifyPropertyChangedFor(nameof(ShowArtefactRoll))]
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
        BuildDisplayItem(ChosenExplorationItemName, ResolvedExplorationOutcome?.MaterialRuleName, ParsedExplorationItemFoundValue);

    /// <summary>The player's found-value roll, parsed - null while unfilled or for a branch that
    /// doesn't need one (see ShowExplorationItemFoundValueRoll). Threaded into ResolvedExplorationItem
    /// so the ChipView's detail popup shows the actual rolled value (e.g. "45") instead of the catalog's
    /// generic worst-case range once the player has entered it.</summary>
    private int? ParsedExplorationItemFoundValue =>
        ResolvedExplorationOutcome?.FoundValueFormula is not null && int.TryParse(ExplorationItemFoundValue, out var value) ? value : null;

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

    private WarbandEquipment? BuildDisplayItem(string? itemName, string? materialRuleName, int? foundValueOverride = null)
    {
        if (itemName is not { } name) return null;
        var item = _equipmentItemsByEnglishName.GetValueOrDefault(name);
        if (item is null) return null;

        var materialRule = materialRuleName is { } ruleName ? _specialRulesByEnglishName.GetValueOrDefault(ruleName) : null;
        return new WarbandEquipment { Item = item, MaterialRule = materialRule, FoundValueOverride = foundValueOverride };
    }

    /// <summary>Texte affiché pour une branche Kind.None (voir IsExplorationNone) - le Note de la
    /// branche retenue (ex. "Skavens : vente aux agents du Clan Eshin"), ou à défaut le nom du résultat
    /// déclenché si la branche n'en porte pas.</summary>
    public string ExplorationNoteText => ResolvedExplorationOutcome?.Note ?? TriggeredExplorationResult?.Name ?? string.Empty;

    /// <summary>Vrai pour un résultat Groupe B "conditionné par la bande" (Traînard, Prisonniers,
    /// Cimetière, bénédiction du Sanctuaire - voir Core.Rules.ExplorationOutcomeResolver.
    /// ResolveWarbandOutcome) : la description complète du livre énumère TOUTES les branches par bande,
    /// alors qu'une seule s'applique à la bande jouée - retour utilisateur 2026-08-20, ne montrer que
    /// cette branche plutôt que le paragraphe entier.</summary>
    public bool IsWarbandConditionedResult => TriggeredExplorationResult?.Outcomes.Any(o => o.RestrictedToWarbandArchetypeNames.Count > 0) == true;

    /// <summary>Texte affiché en haut de l'étape Résultat - la description complète du livre, sauf pour
    /// un résultat conditionné par la bande où seule la phrase d'intro partagée (ExplorationResult.
    /// ShortDescription) est montrée, suivie de la vraie phrase de la branche résolue
    /// (ExplorationOutcome.BranchText, correctement traduite) plutôt que le paragraphe entier énumérant
    /// les 4 branches - retour utilisateur 2026-08-20 : la première version de ce correctif réutilisait
    /// Note (jamais traduit, un simple tag interne) et perdait la phrase d'intro.</summary>
    public string ExplorationResultDescriptionText => IsWarbandConditionedResult
        ? string.Join(" ", new[] { TriggeredExplorationResult?.ShortDescription, ResolvedExplorationOutcome?.BranchText }.Where(s => !string.IsNullOrEmpty(s)))
        : TriggeredExplorationResult?.Description ?? string.Empty;

    /// <summary>Le bloc Note de la branche résolue (plus bas, sous Or/Objet/Pierre magique) ne doit pas
    /// répéter ce que ExplorationResultDescriptionText affiche déjà en haut pour un résultat conditionné
    /// par la bande.</summary>
    public bool ShowExplorationNoteInBranchSection => IsExplorationNone && !IsWarbandConditionedResult;

    /// <summary>ExplorationOutcome.GrantsFreeHenchmanArchetypeName résolu (ex. "Zombie", Traînard) -
    /// affiché en ChipView tapable (même langage d'interaction que tout objet trouvé) plutôt qu'en
    /// simple texte, pour que le joueur voie ce qui rejoint sa bande avant même de valider le wizard.</summary>
    public WarriorArchetype? ResolvedFreeHenchman => ResolvedExplorationOutcome?.GrantsFreeHenchmanArchetypeName is { } name
        ? _warriorArchetypesByEnglishName.GetValueOrDefault(name) : null;

    [RelayCommand]
    private Task ShowFreeHenchmanDetail(WarriorArchetype archetype) => _detailDialogs.ShowWarriorArchetypeDetailDialogAsync(archetype, Array.Empty<NamedRef>());

    // --- Recrutement conditionné à l'équipement (Prisonniers, "autres bandes") -----------------
    //
    // Contrairement à GrantsFreeHenchmanArchetypeName (archétype FIXE du livre, ex. Zombie) et
    // GrantsDistributedHeroExperienceFormula (répartition libre entre Héros), cette branche laisse le
    // joueur choisir un groupe d'Hommes de main DÉJÀ existant dans la bande à renforcer - "en utilisant
    // les mêmes caractéristiques que le groupe existant" (règle du livre, RAW ambigu pour une bande sans
    // groupe humain - Nains/Elfes/Orcs... - résolu simplement ici en listant les groupes RÉELLEMENT
    // présents dans CETTE bande, peu importe leur race). Le recrutement lui-même est GRATUIT (aucun Cost
    // d'archétype déduit, contrairement à Core.Rules.RecruitmentRules.CanRecruit) - seul le coût de
    // l'équipement à répliquer (le loadout ACTUEL du groupe, partagé pour tout le groupe - voir
    // Warrior.Equipment) doit être couvert par la trésorerie. Confirmé via mockup (2026-08-21) : "Ne pas
    // recruter" est l'option par défaut (première de la liste, aucun tableau de trésorerie affiché tant
    // qu'elle reste choisie, tableau réduit à une seule ligne sinon - le coût lui-même reste visible dans
    // le libellé du Picker) ; pas de bouton dédié Recruter/Refuser, seul le "Suivant" du wizard valide
    // (bloqué si le solde choisi passerait négatif, même idiome que les autres jets de cette étape).

    public bool HasOptionalEquippedHenchmanGrant => ResolvedExplorationOutcome?.GrantsOptionalEquippedHenchman == true;

    public sealed record EquippedHenchmanGroupOption(string DisplayName, WarriorOutcomeRow? Group, int EquipmentCost);

    /// <summary>"Ne pas recruter" toujours en premier/sélectionné par défaut (retour utilisateur
    /// 2026-08-21), suivi d'un groupe d'Hommes de main par groupe RÉEL de la bande (jamais un groupe mort
    /// cette partie - voir IsDead) - le coût affiché est celui de son équipement ACTUEL (Warrior.
    /// Equipment), répliqué tel quel pour le nouveau membre.</summary>
    public List<EquippedHenchmanGroupOption> EquippedHenchmanGroupOptions =>
        new List<EquippedHenchmanGroupOption> { new(Loc["EndOfGameDoNotRecruitOption"], null, 0) }
            .Concat(WarriorRows.Where(r => !r.IsHero && !r.IsDead).Select(r =>
                new EquippedHenchmanGroupOption($"{r.ArchetypeName} (x{r.HeadCount}) — {EquippedHenchmanGroupCost(r)} CO", r, EquippedHenchmanGroupCost(r))))
            .ToList();

    private static int EquippedHenchmanGroupCost(WarriorOutcomeRow group) =>
        group.Warrior.Equipment.Sum(e => EquipmentPricing.CalculateCost(e.Item.Cost, e.MaterialRule?.CostMultiplier, isFree: false));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEquippedHenchmanTreasury))]
    [NotifyPropertyChangedFor(nameof(EquippedHenchmanTreasuryAfter))]
    [NotifyPropertyChangedFor(nameof(CanAffordEquippedHenchman))]
    private EquippedHenchmanGroupOption? selectedEquippedHenchmanGroupOption;

    partial void OnSelectedEquippedHenchmanGroupOptionChanged(EquippedHenchmanGroupOption? value)
    {
        if (value?.Group is not null) EquippedHenchmanError = null;
    }

    [ObservableProperty]
    private string? equippedHenchmanError;

    /// <summary>Masqué tant que "Ne pas recruter" reste choisi (mock confirmé 2026-08-21) - inutile
    /// d'afficher un calcul de trésorerie pour une option qui n'en change rien.</summary>
    public bool ShowEquippedHenchmanTreasury => SelectedEquippedHenchmanGroupOption?.Group is not null;

    /// <summary>Trésorerie actuelle (au moment d'ouvrir ce wizard, voir _currentTreasury) + l'or de CETTE
    /// branche (le même 2D6 que ExplorationGoldAmount, l'escorte hors de la ville) - le coût de
    /// l'équipement du groupe choisi. Peut afficher un total négatif (voir CanAffordEquippedHenchman) :
    /// c'est justement le signal qui bloque la progression.</summary>
    public int EquippedHenchmanTreasuryAfter => _currentTreasury
        + (int.TryParse(ExplorationGoldAmount, out var gold) ? gold : 0)
        - (SelectedEquippedHenchmanGroupOption?.EquipmentCost ?? 0);

    public bool CanAffordEquippedHenchman => SelectedEquippedHenchmanGroupOption?.Group is null
        || RecruitmentRules.CanAffordEquippedHenchman(
            _currentTreasury + (int.TryParse(ExplorationGoldAmount, out var gold) ? gold : 0),
            SelectedEquippedHenchmanGroupOption.EquipmentCost);

    // --- Bénédiction d'arme (Sanctuaire, Sœurs de Sigmar/Chasseurs de Sorcières) ---------------
    //
    // "une arme au choix blesse désormais automatiquement..." - le joueur choisit une arme parmi celles
    // déjà portées par un Héros (jamais un groupe d'Hommes de main, dont l'Equipment est partagé par
    // plusieurs figurines et ne désigne donc pas une arme unique à bénir ; jamais la réserve de la bande
    // non plus - retour utilisateur 2026-08-21). La bénédiction attache la SpecialRule "Blessed Weapon"
    // (voir SpecialRules.json) sur cette WarriorEquipment précise via WarriorEquipment.MaterialRule -
    // même mécanisme qu'un achat en Gromril/Ithilmar, pas un bool dédié : le chip/l'abréviation existants
    // ("B") s'affichent tels quels, aucun nouveau code d'affichage nécessaire.

    public bool HasWeaponBlessingGrant => ResolvedExplorationOutcome?.GrantsWeaponBlessing == true;

    public sealed record WeaponBlessingOption(string DisplayName, WarriorOutcomeRow? Hero, WarriorEquipment? Equipment);

    /// <summary>"Ne pas bénir d'arme" en premier/par défaut (même idiome que EquippedHenchmanGroupOptions/
    /// SentHeroOptions), suivi d'une entrée par arme RÉELLEMENT portée par un Héros vivant - filtré aux
    /// catégories arme (corps-à-corps/tir/poudre noire), une armure ou un objet divers n'a pas de sens à
    /// bénir ici.</summary>
    public List<WeaponBlessingOption> WeaponBlessingOptions =>
        new List<WeaponBlessingOption> { new(Loc["EndOfGameDoNotBlessOption"], null, null) }
            .Concat(WarriorRows.Where(r => r.IsHero && !r.IsDead)
                .SelectMany(hero => hero.Warrior.Equipment
                    .Where(e => e.Item.Category is EquipmentCategory.MeleeWeapon or EquipmentCategory.MissileWeapon or EquipmentCategory.BlackPowderWeapon)
                    .Select(e => new WeaponBlessingOption($"{hero.Name} — {e.NameDisplay}", hero, e))))
            .ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BlessedWeaponPreview))]
    private WeaponBlessingOption? selectedWeaponBlessingOption;

    /// <summary>Aperçu jetable (jamais persisté tel quel - même idiome que BuildDisplayItem/
    /// ResolvedExplorationItem) du chip de l'arme choisie une fois bénie : combine son MaterialRule
    /// existant (Gromril/Ithilmar/Ornée, s'il y en a un) avec la SpecialRule "Blessed Weapon" déjà
    /// résolue dans _specialRulesByEnglishName, pour que le joueur voie tout de suite "Épée (G, B)"
    /// avant même de valider le wizard - retour utilisateur explicite (2026-08-21).</summary>
    public WarriorEquipment? BlessedWeaponPreview => SelectedWeaponBlessingOption?.Equipment is { } equipment
        ? new WarriorEquipment
        {
            Item = equipment.Item,
            MaterialRule = equipment.MaterialRule,
            BlessingRule = _specialRulesByEnglishName.GetValueOrDefault("Blessed Weapon"),
            Quantity = equipment.Quantity
        }
        : null;

    [RelayCommand]
    private Task ShowBlessedWeaponDetail(WarriorEquipment item) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(item.Item, item.MaterialRule, blessingRule: item.BlessingRule);

    // --- Expérience répartie entre les Héros (Prisonniers, Possédés) --------------------------
    //
    // Contrairement à GrantsLeaderExperience (Traînard : +1 fixe au seul chef, aucun choix), cette
    // branche jette un total (D3) que le joueur répartit ENTRE plusieurs Héros comme il le souhaite -
    // confirmé via un mockup (steppeur +/- par Héros, 2026-08-20). Le total lui-même reste le jet
    // physique du joueur (jamais auto-rempli, même idiome que tout le reste du wizard) ; seule la
    // RÉPARTITION est un choix libre, pas un hasard.

    public bool HasDistributedHeroExperienceGrant => ResolvedExplorationOutcome?.GrantsDistributedHeroExperienceFormula is not null;

    public List<WarriorOutcomeRow> DistributedExperienceEligibleHeroes => WarriorRows.Where(r => r.IsHero && !r.IsDead).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistributedExperienceRemaining))]
    private string distributedExperienceTotal = string.Empty;

    [ObservableProperty]
    private string? distributedExperienceError;

    partial void OnDistributedExperienceTotalChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) DistributedExperienceError = null; }

    /// <summary>Null tant que le total n'est pas encore un nombre valide - distinct de 0 (déjà entièrement
    /// réparti), qui doit rester une valeur affichable/validable normalement.</summary>
    public int? DistributedExperienceRemaining => int.TryParse(DistributedExperienceTotal, out var total)
        ? total - DistributedExperienceEligibleHeroes.Sum(h => h.DistributedExplorationExperience)
        : null;

    [RelayCommand]
    private void AutoRollDistributedExperience()
    {
        if (ResolvedExplorationOutcome?.GrantsDistributedHeroExperienceFormula is { } formula)
            DistributedExperienceTotal = DiceFormula.Roll(formula).ToString();
    }

    [RelayCommand]
    private void IncrementDistributedExperience(WarriorOutcomeRow hero)
    {
        if (DistributedExperienceRemaining is > 0) hero.DistributedExplorationExperience++;
    }

    [RelayCommand]
    private void DecrementDistributedExperience(WarriorOutcomeRow hero)
    {
        if (hero.DistributedExplorationExperience > 0) hero.DistributedExplorationExperience--;
    }

    public bool IsExplorationGold => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold;

    /// <summary>False for a branch that TriggersArtefactRoll (see ShowArtefactRoll) even though its
    /// Kind is still Item - that branch's item isn't known yet (a second D6 roll on the Magical
    /// Artefacts table decides it), so the normal quantity/choice/chip UI gated on this flag would be
    /// meaningless here.</summary>
    public bool IsExplorationItem => ResolvedExplorationOutcome is { Kind: ExplorationOutcomeKind.Item, TriggersArtefactRoll: false };

    public bool IsExplorationWyrdstone => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Wyrdstone;

    // --- Table des Artefacts Magiques (Villa d'un Noble) --------------------------------------
    //
    // La seule branche qui, une fois résolue, exige encore un SECOND jet - un D6 sur la table fixe des
    // 6 Artefacts Magiques du livre (voir ExplorationOutcome.TriggersArtefactRoll/Core.Rules.
    // MagicalArtefactTable) - contrairement à toute autre branche Item, l'objet précis n'est connu
    // qu'après ce second jet.

    public bool ShowArtefactRoll => ResolvedExplorationOutcome?.TriggersArtefactRoll == true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedArtefactItemName))]
    [NotifyPropertyChangedFor(nameof(ResolvedArtefactItem))]
    private string artefactRoll = string.Empty;

    [ObservableProperty]
    private string? artefactRollError;

    /// <summary>Nom ANGLAIS de l'objet du catalogue tiré sur la table - null tant que le jet n'est pas
    /// renseigné.</summary>
    public string? ResolvedArtefactItemName => ShowArtefactRoll && int.TryParse(ArtefactRoll, out var roll)
        ? MagicalArtefactTable.RollForItemName(roll) : null;

    /// <summary>Même besoin que ResolvedExplorationItem - un WarbandEquipment jetable pour le ChipView
    /// tapable (icône + popup détail), jamais de matériau pour un artefact unique.</summary>
    public WarbandEquipment? ResolvedArtefactItem => BuildDisplayItem(ResolvedArtefactItemName, null);

    partial void OnArtefactRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) ArtefactRollError = null; }

    [RelayCommand]
    private void AutoRollArtefact() => ArtefactRoll = ExplorationChart.RollDie().ToString();

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
    private Task ShowExplorationItemDetail(WarbandEquipment item) => _detailDialogs.ShowEquipmentDetailDialogAsync(item.Item, item.MaterialRule, item.FoundValueOverride);

    /// <summary>Sauf indication contraire du livre (ex. Forge : "D3 Hallebardes"), on ne trouve qu'un
    /// seul exemplaire d'un objet - ItemQuantityFormula vaut alors "1", une quantité fixe et non un jet
    /// (voir ApplyResolvedOutcome, qui la renseigne directement sans rien demander au joueur). Le dé de
    /// relance (AutoRollExplorationItemQuantityCommand) et le champ ne sont utiles que si la formule est
    /// un vrai jet ("D3", "D6"...).</summary>
    public bool ShowExplorationItemQuantityRoll =>
        ResolvedExplorationOutcome?.ItemQuantityFormula?.Contains('D', StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>True only for a branch whose found item's resale value isn't the catalog's fixed Cost
    /// (e.g. Jewelsmith's Quartz Stones/Ruby - see ExplorationOutcome.FoundValueFormula) - always a real
    /// dice formula when set (unlike ShowExplorationItemQuantityRoll, there's no "fixed value" case that
    /// would skip the roll: a formula-free found value just uses Item.Cost directly and this stays
    /// false).</summary>
    public bool ShowExplorationItemFoundValueRoll => ResolvedExplorationOutcome?.FoundValueFormula is not null;

    /// <summary>Même idée que ShowExplorationItemQuantityRoll côté Wyrdstone (ex. Puits : toujours "1"
    /// pierre, pas un jet) - le Bâtiment Éventré/La Fosse ont de vraies formules ("D3"/"D6+1") et
    /// gardent leur champ + dé normalement.</summary>
    public bool ShowExplorationWyrdstoneRoll =>
        ResolvedExplorationOutcome?.GoldFormula?.Contains('D', StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>False only for a Gold branch resolved via the double-roll check (ShowDoubleRollCheck,
    /// e.g. Merchant's House) - ExplorationGoldAmount is already computed from the SAME 2D6 the player
    /// typed for the double check (see ResolveDoubleRoll/DiceFormula.Apply), so showing an editable/
    /// rerollable field here would invite a second, contradictory roll for the same formula. True
    /// (every other Gold branch) shows the usual editable Entry + dice.</summary>
    public bool ShowExplorationGoldRoll => !ShowDoubleRollCheck;

    /// <summary>Branche retenue sans effet trésorerie/inventaire (ex. Traînard/"autres bandes",
    /// Charrette Renversée 5-6) - reste purement informatif (Note ou Description du résultat), juste
    /// consigné dans l'Historique à la sauvegarde (voir WarbandDetailViewModel.EndOfGame) plutôt que
    /// silencieusement perdu.</summary>
    public bool IsExplorationNone => ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.None;

    // --- Test de caractéristique (Puits/Endurance, Taverne/Commandement) ------------------------
    //
    // Choisir un Héros et comparer son jet à une de ses stats pour départager Réussite/Échec - une
    // autre façon de choisir la branche résolue (ResolvedExplorationOutcome), au même niveau que le
    // sous-jet classique (ShowExplorationSubRoll) ou la branche Auto seule. Comparer un jet déjà saisi
    // à une stat déjà connue est de l'arithmétique, pas une décision aléatoire prise à la place du
    // joueur - contrairement au tirage lui-même (jamais automatique, voir ExplorationGoldAmount et
    // consorts). Le nombre de dés dépend de la stat testée (voir ExplorationChart.RollStatTest et
    // StatTestRollPlaceholder) - 2D6 pour Commandement (RulesReference "Tests de Commandement", une
    // exception explicite), 1D6 pour tout le reste. Taverne réutilise EXACTEMENT ce mécanisme (contraste
    // avec le test additionnel de Bâtiment Éventré, voir ShowBonusStatTest plus bas) avec deux ajouts :
    // toujours le chef (StatTestTargetsLeader, StatTestHero renseigné automatiquement plutôt que par un
    // Picker - voir ShowStatTestHeroPicker) et une réussite automatique pour certaines bandes du livre
    // (AutoPassStatTestWarbandArchetypeNames, voir StatTestAutoPasses).
    public bool ShowStatTest => TriggeredExplorationResult?.StatTestField is not null;

    public List<WarriorOutcomeRow> StatTestEligibleHeroes => WarriorRows.Where(r => r.IsHero && !r.IsDead).ToList();

    /// <summary>Faux pour un test ciblant toujours le chef (ex. Taverne) - StatTestHero est alors
    /// renseigné automatiquement (voir ResolveExplorationResult), aucun choix du joueur nécessaire, donc
    /// aucun Picker à afficher. Vrai pour Puits (le joueur choisit qui envoyer).</summary>
    public bool ShowStatTestHeroPicker => ShowStatTest && TriggeredExplorationResult?.StatTestTargetsLeader != true;

    /// <summary>Vrai pour un test ciblant le chef (voir ShowStatTestHeroPicker) dont le chef n'est pas
    /// disponible cette partie (mort/malade/hors de combat) - même idiome que BonusStatTestLeader :
    /// personne ne peut commander à sa place, rien à valider, ni erreur bloquante ni jet à saisir.</summary>
    public bool StatTestLeaderUnavailable => ShowStatTest && TriggeredExplorationResult?.StatTestTargetsLeader == true && StatTestHero is null;

    /// <summary>Vrai pour un test de Commandement gating (Taverne) auto-réussi pour certaines bandes du
    /// livre (Morts-Vivants/Chasseurs de Sorcières/Sœurs de Sigmar) - aucun jet à saisir, la branche
    /// Réussite se résout directement dès que le résultat se déclenche (voir ResolveExplorationResult).</summary>
    public bool StatTestAutoPasses => TriggeredExplorationResult?.AutoPassStatTestWarbandArchetypeNames.Contains(_warbandArchetypeName) == true;

    /// <summary>Le bloc jet+dé (voir ShowStatTest) ne s'affiche que si un Héros/chef est effectivement
    /// désigné ET que la bande ne réussit pas ce test automatiquement.</summary>
    public bool ShowStatTestRoll => StatTestHero is not null && !StatTestAutoPasses;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatTestStatValue))]
    [NotifyPropertyChangedFor(nameof(StatTestSickHero))]
    [NotifyPropertyChangedFor(nameof(StatTestLeaderUnavailable))]
    [NotifyPropertyChangedFor(nameof(ShowStatTestRoll))]
    [NotifyPropertyChangedFor(nameof(StatTestHeroDisplayPrefix))]
    private WarriorOutcomeRow? statTestHero;

    /// <summary>"{Nom} - " devant le champ de jet pour un test ciblant le chef (aucun Picker qui montre
    /// déjà son nom, voir ShowStatTestHeroPicker) - vide pour Puits, où le Picker suffit.</summary>
    public string StatTestHeroDisplayPrefix => !ShowStatTestHeroPicker && StatTestHero is not null ? $"{StatTestHero.Name} - " : string.Empty;

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

    /// <summary>Dice notation shown as the roll field's placeholder - "2D6" for a Commandement test,
    /// plain "D6" otherwise (see ExplorationChart.RollStatTest). Not localized: dice notation reads the
    /// same in FR/EN, no LocalizationService key needed.</summary>
    public string StatTestRollPlaceholder => TriggeredExplorationResult?.StatTestField == ExplorationStatField.Leadership ? "2D6" : "D6";

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
        ExplorationItemFoundValue = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;
        ExplorationAmountError = null;

        if (TriggeredExplorationResult?.StatTestField is null || StatTestHero is null
            || StatTestStatValue is not { } statValue || !int.TryParse(StatTestRoll, out var roll))
            return;

        var outcome = ExplorationOutcomeResolver.ResolveStatTestOutcome(TriggeredExplorationResult, roll, statValue);
        if (outcome is not null) ApplyResolvedOutcome(outcome);
    }

    [RelayCommand]
    private void AutoRollStatTest()
    {
        if (TriggeredExplorationResult?.StatTestField is { } field) StatTestRoll = ExplorationChart.RollStatTest(field).ToString();
    }

    // --- Test de Commandement additionnel du chef (Bâtiment Éventré) --------------------------
    //
    // Contrairement à ShowStatTest (qui GATE toute la résolution, Puits), ce test s'AJOUTE à une
    // branche Auto déjà résolue (les pierres magiques) - voir ExplorationResult.BonusStatTestField.
    // Toujours le chef de bande (Warrior.IsLeader), jamais un choix du joueur : pas de Picker.

    /// <summary>Vrai seulement pour Bâtiment Éventré (seul résultat à porter BonusStatTestField).</summary>
    public bool ShowBonusStatTest => TriggeredExplorationResult?.BonusStatTestField is not null;

    /// <summary>Null si le chef n'est pas dans WarriorRows (mort/malade cette partie, voir
    /// activeWarriorRows côté WarbandDetailViewModel.EndOfGame) - le test est alors simplement
    /// indisponible plutôt qu'une erreur bloquante, personne ne peut commander à sa place.</summary>
    public WarriorOutcomeRow? BonusStatTestLeader => WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BonusStatTestOutcome))]
    [NotifyPropertyChangedFor(nameof(HasBonusStatTestOutcome))]
    [NotifyPropertyChangedFor(nameof(BonusStatTestItem))]
    private string bonusStatTestRoll = string.Empty;

    [ObservableProperty]
    private string? bonusStatTestError;

    public int? BonusStatTestStatValue => BonusStatTestLeader is null || TriggeredExplorationResult?.BonusStatTestField is not { } statField ? null
        : statField switch
        {
            ExplorationStatField.Toughness => BonusStatTestLeader.Warrior.Toughness,
            ExplorationStatField.Leadership => BonusStatTestLeader.Warrior.Leadership,
            _ => null
        };

    public string BonusStatTestFieldLabel => TriggeredExplorationResult?.BonusStatTestField switch
    {
        ExplorationStatField.Toughness => Loc["EndOfGameStatFieldToughness"],
        ExplorationStatField.Leadership => Loc["EndOfGameStatFieldLeadership"],
        _ => string.Empty
    };

    /// <summary>Même idée que StatTestRollPlaceholder - "2D6" pour Commandement (le seul cas connu pour
    /// ce test additionnel, Bâtiment Éventré), "D6" sinon.</summary>
    public string BonusStatTestRollPlaceholder => TriggeredExplorationResult?.BonusStatTestField == ExplorationStatField.Leadership ? "2D6" : "D6";

    /// <summary>Résolu en direct depuis le jet + la stat du chef, jamais stocké dans
    /// ResolvedExplorationOutcome (qui reste réservé à la branche Auto/pierres magiques) - les deux
    /// coexistent, l'un n'écrase pas l'autre.</summary>
    public ExplorationOutcome? BonusStatTestOutcome => TriggeredExplorationResult is not null
        && BonusStatTestStatValue is { } statValue && int.TryParse(BonusStatTestRoll, out var roll)
        ? ExplorationOutcomeResolver.ResolveBonusStatTestOutcome(TriggeredExplorationResult, roll, statValue)
        : null;

    public bool HasBonusStatTestOutcome => BonusStatTestOutcome is not null;

    /// <summary>BonusStatTestOutcome.EquipmentItemName résolu - même besoin que ResolvedExplorationItem/
    /// BonusItem.</summary>
    public WarbandEquipment? BonusStatTestItem =>
        BuildDisplayItem(BonusStatTestOutcome?.EquipmentItemName, BonusStatTestOutcome?.MaterialRuleName);

    partial void OnBonusStatTestRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) BonusStatTestError = null; }

    [RelayCommand]
    private void AutoRollBonusStatTest()
    {
        if (TriggeredExplorationResult?.BonusStatTestField is { } field) BonusStatTestRoll = ExplorationChart.RollStatTest(field).ToString();
    }

    // --- Double au 2D6 (Maison du Marchand) ----------------------------------------------------
    //
    // Le seul résultat de la table qui départage ses branches par "les 2 dés sont-ils identiques"
    // plutôt qu'un sous-jet ou un test de caractéristique (voir ShowDoubleRollCheck) - 2 champs de dé
    // distincts, indispensables ici puisque le total seul (2D6x5) ne permet pas de reconstituer si
    // c'était un double (ex. un total de 6 peut venir de 3+3 comme de 1+5/2+4/4+2/5+1).

    [ObservableProperty]
    private string explorationDoubleDie1 = string.Empty;

    [ObservableProperty]
    private string explorationDoubleDie2 = string.Empty;

    [ObservableProperty]
    private string? explorationDoubleRollError;

    partial void OnExplorationDoubleDie1Changed(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) ExplorationDoubleRollError = null;
        ResolveDoubleRoll();
    }

    partial void OnExplorationDoubleDie2Changed(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) ExplorationDoubleRollError = null;
        ResolveDoubleRoll();
    }

    private void ResolveDoubleRoll()
    {
        ResolvedExplorationOutcome = null;
        ExplorationGoldAmount = string.Empty;
        ExplorationItemQuantity = string.Empty;
        ExplorationItemFoundValue = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;
        ExplorationAmountError = null;

        if (TriggeredExplorationResult is null || !int.TryParse(ExplorationDoubleDie1, out var die1) || !int.TryParse(ExplorationDoubleDie2, out var die2))
            return;

        var outcome = ExplorationOutcomeResolver.ResolveDoubleRollOutcome(TriggeredExplorationResult, die1, die2);
        if (outcome is null) return;
        ApplyResolvedOutcome(outcome);

        // Le même 2D6 sert à la fois à départager Or/Objet ET, si ce n'est pas un double, à calculer
        // l'or lui-même - un second jet "pour de vrai" redemanderait au joueur de relancer physiquement
        // les mêmes dés, ce que le livre ne prévoit pas ici (un seul 2D6, contrairement à Boutique où
        // le bonus partage le dé de l'Or sans que l'un dérive de l'autre).
        if (outcome.Kind == ExplorationOutcomeKind.Gold && outcome.GoldFormula is { } formula)
            ExplorationGoldAmount = DiceFormula.Apply(formula, [die1, die2]).ToString();
    }

    [RelayCommand]
    private void AutoRollExplorationDoubleDie1() => ExplorationDoubleDie1 = ExplorationChart.RollDie().ToString();

    [RelayCommand]
    private void AutoRollExplorationDoubleDie2() => ExplorationDoubleDie2 = ExplorationChart.RollDie().ToString();

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
    [NotifyPropertyChangedFor(nameof(EquippedHenchmanTreasuryAfter))]
    [NotifyPropertyChangedFor(nameof(CanAffordEquippedHenchman))]
    private string explorationGoldAmount = string.Empty;

    [ObservableProperty]
    private string explorationItemQuantity = string.Empty;

    /// <summary>Found resale value for a branch whose item's value isn't the catalog's fixed Cost (see
    /// ShowExplorationItemFoundValueRoll) - same "never auto-filled, player types the physical roll or
    /// clicks the die" idiom as ExplorationGoldAmount. Stored on the resulting WarbandEquipment row as
    /// FoundValueOverride at save time (see WarbandDetailViewModel.EndOfGame).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedExplorationItem))]
    private string explorationItemFoundValue = string.Empty;

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
    partial void OnExplorationItemFoundValueChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) ExplorationAmountError = null; }
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
        ExplorationItemFoundValue = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;
        ExplorationAmountError = null;
        StatTestHero = null;
        StatTestRoll = string.Empty;
        StatTestError = null;
        ExplorationDoubleDie1 = string.Empty;
        ExplorationDoubleDie2 = string.Empty;
        ExplorationDoubleRollError = null;
        BonusStatTestRoll = string.Empty;
        BonusStatTestError = null;
        // Passe par SelectedSentHeroOption (pas SentHero directement) pour que le Picker affiche "Passer
        // son chemin" comme valeur par défaut plutôt qu'un placeholder vide - voir SentHeroOptions.
        SelectedSentHeroOption = SentHeroOptions[0];
        ArtefactRoll = string.Empty;
        ArtefactRollError = null;
        DistributedExperienceTotal = string.Empty;
        DistributedExperienceError = null;
        foreach (var hero in WarriorRows)
        {
            hero.DistributedExplorationExperience = 0;
            hero.LeaderExplorationExperience = 0;
        }
        SelectedEquippedHenchmanGroupOption = EquippedHenchmanGroupOptions[0];
        EquippedHenchmanError = null;
        SelectedWeaponBlessingOption = WeaponBlessingOptions[0];

        if (ExplorationDice.Any(d => d.Value is null)) return;

        var multiple = ExplorationChart.DetectMultiples(ExplorationDice.Select(d => d.Value!.Value).ToList());
        if (multiple is null) return;

        TriggeredExplorationResult = _explorationResults
            .FirstOrDefault(r => r.DiceCount == multiple.Value.DiceCount && r.Value == multiple.Value.Value);
        if (TriggeredExplorationResult is null) return;

        // Test de caractéristique ciblant toujours le chef (Taverne/Commandement, voir
        // ShowStatTestHeroPicker) : renseigne StatTestHero automatiquement (déclenche ResolveStatTest via
        // OnStatTestHeroChanged, sans effet tant qu'aucun jet n'est saisi) - aucun choix du joueur. Si en
        // plus la bande jouée réussit ce test automatiquement (StatTestAutoPasses), résout directement la
        // branche Réussite sans attendre de jet du tout.
        if (TriggeredExplorationResult.StatTestTargetsLeader)
        {
            StatTestHero = WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);
            if (StatTestAutoPasses && TriggeredExplorationResult.Outcomes.FirstOrDefault(o => o.StatTestPass == true) is { } passOutcome)
                ApplyResolvedOutcome(passOutcome);
        }

        // Une branche Auto (sans sous-jet) se résout tout de suite, qu'elle soit seule (ex. Masures en
        // Ruine) ou accompagnée d'une branche à sous-jet optionnelle sur le MÊME dé (ex. Boutique - voir
        // BonusItemOutcome, qui se déduit du jet d'or plutôt que d'en redemander un second). Ce n'est
        // que quand TOUTES les branches ont un sous-jet (ex. Cadavre, mutuellement exclusives) que
        // ShowExplorationSubRoll prend le relais ; un résultat à test de caractéristique (Puits...)
        // attend le choix du Héros + son jet avant de résoudre quoi que ce soit (voir ResolveStatTest) -
        // ResolveAutoOutcome renvoie null pour lui, voir Core.Rules.ExplorationOutcomeResolver.
        var autoOutcome = ExplorationOutcomeResolver.ResolveAutoOutcome(TriggeredExplorationResult);
        if (autoOutcome is not null) ApplyResolvedOutcome(autoOutcome);
        else if (ExplorationOutcomeResolver.ResolveWarbandOutcome(TriggeredExplorationResult, _warbandArchetypeName) is { } warbandOutcome)
            // Groupe B "conditionné par la bande" (Traînard, Prisonniers, Cimetière, bénédiction du
            // Sanctuaire) : la branche applicable se résout d'elle-même depuis l'archétype de la bande
            // jouée, aucune saisie du joueur ne la départage (voir ResolveWarbandOutcome).
            ApplyResolvedOutcome(warbandOutcome);
    }

    partial void OnExplorationSubRollChanged(string value)
    {
        ExplorationSubRollError = null;
        ResolvedExplorationOutcome = null;
        ExplorationGoldAmount = string.Empty;
        ExplorationItemQuantity = string.Empty;
        ExplorationItemFoundValue = string.Empty;
        ExplorationWyrdstoneAmount = string.Empty;
        ExplorationAmountError = null;
        ArtefactRoll = string.Empty;
        ArtefactRollError = null;

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
        // ItemQuantityFormula réutilisé tel quel pour la quantité d'un Homme de main gratuit (ex.
        // Prisonniers : D3 Zombies) - même champ, même convention ("1" fixe se renseigne directement,
        // "D3" laisse le champ+dé au joueur), pas seulement pour Kind.Item.
        if ((outcome.Kind == ExplorationOutcomeKind.Item || outcome.GrantsFreeHenchmanArchetypeName is not null)
            && outcome.ItemQuantityFormula is { } itemFormula && !itemFormula.Contains('D', StringComparison.OrdinalIgnoreCase))
            ExplorationItemQuantity = itemFormula;
        else if (outcome.Kind == ExplorationOutcomeKind.Wyrdstone && outcome.GoldFormula is { } wyrdstoneFormula
            && !wyrdstoneFormula.Contains('D', StringComparison.OrdinalIgnoreCase))
            ExplorationWyrdstoneAmount = wyrdstoneFormula;

        // Traînard, branche Possédés : XP fixe au chef, sans jet ni choix - pousse la valeur sur sa ligne
        // (WarriorOutcomeRow.LeaderExplorationExperience) pour que la Progression puisse en tenir compte
        // si ça franchit un palier (voir ExplorationMilestoneCount), silencieusement ignoré si le chef
        // n'est pas disponible cette partie (mort/malade/hors de combat), même idiome que
        // BonusStatTestLeader/GrantsLeaderExperience côté sauvegarde (WarbandDetailViewModel.EndOfGame).
        if (outcome.GrantsLeaderExperience is { } leaderXp
            && WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader) is { } leaderRow)
            leaderRow.LeaderExplorationExperience = leaderXp;
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
    private void AutoRollExplorationItemFoundValue()
    {
        if (ResolvedExplorationOutcome?.FoundValueFormula is { } formula) ExplorationItemFoundValue = DiceFormula.Roll(formula).ToString();
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

        // StatTestLeaderUnavailable = test ciblant le chef (Taverne) mais chef indisponible cette partie
        // (mort/malade/hors de combat) - même idiome que BonusStatTestLeader ci-dessous : personne ne
        // peut commander à sa place, rien à valider plutôt qu'une erreur bloquante impossible à résoudre.
        // StatTestAutoPasses : aucun jet à saisir, déjà résolu (voir ResolveExplorationResult). Vérifie
        // StatTestRoll (pas ResolvedExplorationOutcome) - depuis que Taverne n'a plus qu'une seule
        // branche (Réussite, retour utilisateur 2026-08-20 : un Échec ne doit rien produire, comme le
        // test additionnel de Bâtiment Éventré), un Échec résout ResolvedExplorationOutcome à null tout
        // comme "pas encore joué" - StatTestRoll seul distingue les deux, même idiome que BonusStatTestRoll
        // ci-dessous.
        if (ShowStatTest && !StatTestLeaderUnavailable && !StatTestAutoPasses
            && !CheckRoll(string.IsNullOrWhiteSpace(StatTestRoll), () => StatTestError = Loc["EndOfGameRollRequired"]))
            return false;

        if (ShowDoubleRollCheck && !CheckRoll(ResolvedExplorationOutcome is null, () => ExplorationDoubleRollError = Loc["EndOfGameRollRequired"]))
            return false;

        // BonusStatTestLeader null = personne pour commander (mort/malade cette partie) - rien à
        // valider, le bonus est simplement indisponible plutôt qu'une erreur bloquante. Vérifie
        // BonusStatTestRoll (pas ResolvedExplorationOutcome, réservé à la branche Auto) puisqu'un Échec
        // ne produit aucune Outcome (voir ResolveBonusStatTestOutcome) - null y signifierait aussi bien
        // "pas encore joué" que "raté", ambigu pour la validation.
        if (ShowBonusStatTest && BonusStatTestLeader is not null
            && !CheckRoll(string.IsNullOrWhiteSpace(BonusStatTestRoll), () => BonusStatTestError = Loc["EndOfGameRollRequired"]))
            return false;

        // Le jet sur la table des Artefacts Magiques (Villa d'un Noble) se valide à part - la quantité
        // d'objet normale (switch ci-dessous) ne s'applique pas à cette branche, voir IsExplorationItem.
        if (ShowArtefactRoll && !CheckRoll(string.IsNullOrWhiteSpace(ArtefactRoll), () => ArtefactRollError = Loc["EndOfGameRollRequired"]))
            return false;

        // Expérience répartie entre Héros (Prisonniers, Possédés) : bloque tant que le total n'est pas
        // saisi OU qu'il reste des points non distribués (DistributedExperienceRemaining != 0) - le
        // joueur doit avoir explicitement affecté chaque point à un Héros, pas juste renseigné le total.
        if (HasDistributedHeroExperienceGrant
            && !CheckRoll(string.IsNullOrWhiteSpace(DistributedExperienceTotal) || DistributedExperienceRemaining != 0,
                () => DistributedExperienceError = Loc["EndOfGameRollRequired"]))
            return false;

        // Recrutement conditionné à l'équipement (Prisonniers) : "Ne pas recruter" (Group null) n'a rien
        // à valider - seul un groupe choisi dont le coût dépasserait la trésorerie disponible bloque la
        // progression (voir CanAffordEquippedHenchman).
        if (HasOptionalEquippedHenchmanGrant && SelectedEquippedHenchmanGroupOption?.Group is not null
            && !CheckRoll(!CanAffordEquippedHenchman, () => EquippedHenchmanError = Loc["EndOfGameEquippedHenchmanUnaffordable"]))
            return false;

        var amountMissing = ResolvedExplorationOutcome?.Kind switch
        {
            ExplorationOutcomeKind.Gold => string.IsNullOrWhiteSpace(ExplorationGoldAmount),
            ExplorationOutcomeKind.Item => !ShowArtefactRoll && (string.IsNullOrWhiteSpace(ExplorationItemQuantity)
                || (ShowExplorationItemFoundValueRoll && string.IsNullOrWhiteSpace(ExplorationItemFoundValue))),
            ExplorationOutcomeKind.Wyrdstone => string.IsNullOrWhiteSpace(ExplorationWyrdstoneAmount),
            _ => false
        };

        // Un Homme de main gratuit à quantité variable (ex. Prisonniers : D3 Zombies) n'a pas de Kind
        // dédié dans le switch ci-dessus (c'est toujours Kind.None) - se valide à part, même principe.
        var henchmanQuantityMissing = ResolvedFreeHenchman is not null && ShowExplorationItemQuantityRoll
            && string.IsNullOrWhiteSpace(ExplorationItemQuantity);

        return CheckRoll(amountMissing == true || henchmanQuantityMissing, () => ExplorationAmountError = Loc["EndOfGameRollRequired"]);
    }
}
