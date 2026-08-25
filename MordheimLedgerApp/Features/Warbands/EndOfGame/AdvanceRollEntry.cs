using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One 2D6 progression roll for one milestone crossed by a WarriorOutcomeRow - see
/// WarriorOutcomeRow.AdvanceRolls/SyncAdvanceRolls (a warrior can cross several milestones in the
/// same End of Game, each needing its own independent roll).
///
/// Mechanized (2026-08-24): ResultText/IsSkillResult stay purely descriptive (still driven by
/// TryGetTextKey, used for the flavor-text subtitle), but Outcome/ResolvedField now resolve the
/// structured shape (see Core.Rules.AdvanceOutcome) and drive an actual +1 to the target
/// characteristic, applied by WarbandDetailViewModel.EndOfGame at Save. A Henchman promotion result
/// (10-12, see HenchmanAdvanceTable.IsPromotion) expands this same card in place with the 2 skill
/// tables + the promoted Hero's immediate Advance roll (NestedHeroRoll) - a NESTED AdvanceRollEntry
/// rather than a new WizardStep, deliberately, per the hazard already documented on
/// EndOfGameDialogViewModel.WizardStep.IsExplorationAdvance (Steps is recomputed on every access; a
/// step whose visibility flips true after the player has scrolled past it corrupts navigation). If the
/// source group still has members left after promoting one, a second nested roll (NestedHenchmanRoll,
/// SuppressPromotion: true - a 10-12 there re-rolls instead of promoting a second model, per the
/// rulebook) covers their own Progression the same pass.</summary>
public partial class AdvanceRollEntry : ObservableObject
{
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly Warrior _warrior;

    /// <summary>True only for the nested "remaining Henchmen" roll built by a Promotion result's
    /// TryBuildPromotionNestedRolls - a 10-12 here is rejected (re-roll, no second promotion this
    /// pass) rather than expanding a third level of nesting.</summary>
    private readonly bool _suppressPromotion;

    /// <summary>Snapshot of the warband's Hero count at wizard-open time (EndOfGameDialogViewModel
    /// construction) - compared against PromotionRules.MaxHeroes for CanPromote. Does NOT account for
    /// another Henchman group promoting earlier in this SAME End of Game pass - a documented,
    /// deliberately accepted simplification (crossing the 6-Hero cap via two separate promotions in one
    /// battle is a rare edge case), see CanPromote.</summary>
    private readonly int _startingHeroCount;

    /// <summary>Every AdvanceRollEntry sharing the same WarriorOutcomeRow.AdvanceRolls/
    /// ExplorationAdvanceRolls collection - a warrior crossing several milestones at once rolls several
    /// entries in the same wizard pass, so eligibility (racial max, Henchman "never twice") for entry N
    /// must fold in whatever entries BEFORE it (by list order) already resolved, without touching the
    /// live Warrior until Save. Entries after this one are deliberately ignored (list order = resolution
    /// order convention) - changing an earlier entry after a later one already resolved doesn't
    /// retroactively re-check it, a documented simplification rather than a full recompute cascade.</summary>
    private readonly ObservableCollection<AdvanceRollEntry> _siblings;

    private static readonly Dictionary<CharacteristicField, string> _fieldAbbrKeys = new()
    {
        [CharacteristicField.Movement] = "StatMovementAbbr",
        [CharacteristicField.WeaponSkill] = "StatWeaponSkillAbbr",
        [CharacteristicField.BallisticSkill] = "StatBallisticSkillAbbr",
        [CharacteristicField.Strength] = "StatStrengthAbbr",
        [CharacteristicField.Toughness] = "StatToughnessAbbr",
        [CharacteristicField.Wounds] = "StatWoundsAbbr",
        [CharacteristicField.Initiative] = "StatInitiativeAbbr",
        [CharacteristicField.Attacks] = "StatAttacksAbbr",
        [CharacteristicField.Leadership] = "StatLeadershipAbbr"
    };

    private readonly Dictionary<string, CharacteristicField> _choiceFieldByLabel = new();

    public int Index { get; }
    public bool IsHero { get; }
    public string Label => string.Format(_loc["EndOfGameMilestoneLabel"], Index);

    /// <summary>True only for a Hero whose archetype is a spellcaster (see WarriorRow.HasMagicSchools) -
    /// gates the "Tirer un sort" alternative on a Skill result (ShowSpellOption). Always false for a
    /// Henchman group.</summary>
    public bool IsSpellcaster { get; }

    /// <summary>Le score 2D6 - saisi à la main (jet physique) ou rempli par AutoRollAdvance. Dès que la
    /// valeur est un jet complet et valide, ResultText/Outcome se résolvent tout seuls
    /// (OnManualRollChanged).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkillResult))]
    [NotifyPropertyChangedFor(nameof(ShowSpellOption))]
    [NotifyPropertyChangedFor(nameof(IsCharacteristicResult))]
    [NotifyPropertyChangedFor(nameof(IsPromotionResult))]
    private string manualRoll = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.RollError, posé uniquement par
    /// EndOfGameDialogViewModel.Next si ce jet (ou le choix de caractéristique qu'il implique) est
    /// encore vide/invalide à ce moment-là.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        ResultText = string.Empty;
        Outcome = null;
        PromotedWarriorPreview = null;
        NestedHeroRoll = null;
        NestedHenchmanRoll = null;
        SelectedPromotionCategoryLabel1 = string.Empty;
        SelectedPromotionCategoryLabel2 = string.Empty;
        PromotedWarriorName = string.Empty;

        if (int.TryParse(value, out var roll))
        {
            // La carte "reste du groupe" (SuppressPromotion) ne peut jamais déclencher une 2e promotion
            // ce même Advance - un 10-12 y est simplement rejeté, le joueur doit relancer (voir la doc
            // de classe). Ni ResultText ni Outcome ne se résolvent dans ce cas : IsFullyResolved reste
            // donc naturellement faux (même chemin de blocage que "rien saisi").
            var rejected = _suppressPromotion && !IsHero && HenchmanAdvanceTable.IsPromotion(roll);
            if (!rejected)
            {
                bool found;
                string key;
                found = IsHero ? HeroAdvanceTable.TryGetTextKey(roll, out key) : HenchmanAdvanceTable.TryGetTextKey(roll, out key);
                if (found)
                {
                    ResultText = _loc[key];
                    RollError = null;
                }

                Outcome = IsHero
                    ? HeroAdvanceTable.TryGetOutcome(roll, out var heroOutcome) ? heroOutcome : null
                    : HenchmanAdvanceTable.TryGetOutcome(roll, out var henchmanOutcome) ? henchmanOutcome : null;
            }
        }

        ManualSubRoll = string.Empty;
        SelectedSkills.Clear();
        SelectedSpell = null;
        RecomputeCharacteristicResolution();
    }

    /// <summary>Texte descriptif du résultat une fois résolu - purement informatif, voir
    /// HeroAdvanceTable/HenchmanAdvanceTable.</summary>
    [ObservableProperty]
    private string resultText = string.Empty;

    /// <summary>Résultat structuré du jet (voir Core.Rules.AdvanceOutcome) - null tant que ManualRoll
    /// n'est pas un jet complet et valide.</summary>
    public AdvanceOutcome? Outcome { get; private set; }

    /// <summary>Seuls les résultats "Compétence" (voir AdvanceOutcome.Kind) proposent de choisir
    /// directement une compétence ou (Héros sorcier) un sort - les résultats de caractéristique et la
    /// promotion Homme de main (10-12, "Ce gars est doué") passent par ResolvedField/restent
    /// descriptifs.</summary>
    public bool IsSkillResult => Outcome?.Kind == AdvanceKind.Skill;

    /// <summary>Pilote l'affichage du bloc caractéristique dans le XAML (sous-jet/choix/confirmation).</summary>
    public bool IsCharacteristicResult => Outcome?.Kind == AdvanceKind.CharacteristicIncrease;

    /// <summary>Livre : un Héros sorcier peut choisir un nouveau sort permanent à la place d'une nouvelle
    /// compétence sur ce résultat - deux boutons côte à côte (voir XAML), même disposition que
    /// WarriorEditDialog (Compétences/Sorts, deux commandes séparées plutôt qu'un mode partagé).</summary>
    public bool ShowSpellOption => IsSkillResult && IsHero && IsSpellcaster;

    /// <summary>Compétence(s) choisie(s) pour ce jet - rattachée(s) au guerrier par
    /// WarbandDetailViewModel.EndOfGame à l'enregistrement, voir PickAdvanceSkill.</summary>
    public ObservableCollection<Skill> SelectedSkills { get; } = new();
    public string SelectedSkillsText => string.Join(", ", SelectedSkills.Select(s => s.Name));

    /// <summary>Pilote l'affichage exclusif bouton "Choisir une compétence" / nom(s) choisi(s) dans le
    /// XAML - une fois une compétence sélectionnée, son nom remplace le bouton plutôt que de
    /// s'afficher à côté.</summary>
    public bool HasSkillSelected => SelectedSkills.Count > 0;

    /// <summary>Sort permanent choisi à la place d'une compétence (ShowSpellOption) - EXCLUSIF de
    /// SelectedSkills, voir OnManualRollChanged/EndOfGameDialogViewModel.PickAdvanceSkill/
    /// PickAdvanceSpell (choisir l'un efface l'autre).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpellSelected))]
    [NotifyPropertyChangedFor(nameof(ShowSkillOrSpellButtons))]
    private Spell? selectedSpell;

    public bool HasSpellSelected => SelectedSpell is not null;

    /// <summary>Tant que ni compétence ni sort n'est encore choisi : les 2 boutons (+ "OU" entre eux si
    /// ShowSpellOption) restent visibles ; dès qu'un des deux est choisi, ils s'effacent au profit de sa
    /// puce - jamais les deux boutons ET une puce affichés ensemble.</summary>
    public bool ShowSkillOrSpellButtons => !HasSkillSelected && !HasSpellSelected;

    // --- Promotion (Homme de main uniquement, voir Core.Rules.AdvanceOutcome.Kind == Promotion) -------

    public bool IsPromotionResult => Outcome?.Kind == AdvanceKind.Promotion;

    /// <summary>6 Héros maximum par bande (règle fixe du jeu, PAS dérivée du catalogue - voir
    /// Core.Rules.PromotionRules) comparé au compte figé à l'ouverture du wizard (_startingHeroCount).
    /// Faux = bannière + aucun choix proposé, le joueur doit changer ManualRoll (voir XAML).</summary>
    public bool CanPromote => _startingHeroCount < PromotionRules.MaxHeroes;

    /// <summary>Libellés localisés des 6 SkillCategory (mêmes clés resx que le reste de l'app,
    /// SkillCategoryXxx) - deux Pickers indépendants plutôt qu'une liste à cocher plafonnée à 2, même
    /// idiome que les autres Pickers "un seul choix, catalogue éditable" de l'app (Race/EquipmentList).</summary>
    public ObservableCollection<string> SkillCategoryOptions { get; } = new();

    private readonly Dictionary<string, SkillCategory> _skillCategoryByLabel = new();

    [ObservableProperty]
    private string selectedPromotionCategoryLabel1 = string.Empty;

    [ObservableProperty]
    private string selectedPromotionCategoryLabel2 = string.Empty;

    partial void OnSelectedPromotionCategoryLabel1Changed(string value) => TryBuildPromotionNestedRolls();
    partial void OnSelectedPromotionCategoryLabel2Changed(string value) => TryBuildPromotionNestedRolls();

    /// <summary>Nom du guerrier promu - saisie libre requise (voir IsFullyResolved), aucun défaut
    /// sensé à proposer (le nom du groupe, ex. "Bande de Goules", ne convient pas à un individu).</summary>
    [ObservableProperty]
    private string promotedWarriorName = string.Empty;

    partial void OnPromotedWarriorNameChanged(string value)
    {
        if (PromotedWarriorPreview is not null) PromotedWarriorPreview.Name = value;
    }

    /// <summary>Le nouveau Héros en mémoire (voir EntityMapping.CloneAsPromotedHero), construit dès que
    /// les 2 catégories de compétences sont choisies - c'est aussi l'objet que
    /// WarbandDetailViewModel.EndOfGame insère tel quel en base à l'enregistrement (voir
    /// ApplyWarriorOutcomesAsync), pas juste un aperçu jetable : évite de reconstruire le clone une
    /// deuxième fois avec le risque de désynchronisation que ça implique.</summary>
    public Warrior? PromotedWarriorPreview { get; private set; }

    /// <summary>Jet de Progression immédiat du guerrier promu - un AdvanceRollEntry IMBRIQUÉ (pas une
    /// nouvelle WizardStep, voir la doc de classe), construit sur PromotedWarriorPreview.</summary>
    public AdvanceRollEntry? NestedHeroRoll { get; private set; }

    /// <summary>Jet de Progression du reste du groupe si HeadCount &gt; 1 avant décrément - même
    /// mécanique Homme de main que d'habitude, mais SuppressPromotion empêche une 2e promotion ce même
    /// Advance (voir la doc de classe/OnManualRollChanged). Null si le groupe ne comptait qu'un seul
    /// membre (rien à faire pour "le reste").</summary>
    public AdvanceRollEntry? NestedHenchmanRoll { get; private set; }

    /// <summary>Construit PromotedWarriorPreview/NestedHeroRoll/(NestedHenchmanRoll) dès que les 2
    /// catégories choisies sont valides et distinctes - ne reconstruit jamais une fois fait (un
    /// changement de catégorie après coup n'a pas d'équivalent dans le livre, resterait sans effet
    /// plutôt que de perdre un jet imbriqué déjà avancé).</summary>
    private void TryBuildPromotionNestedRolls()
    {
        if (NestedHeroRoll is not null) return;
        if (!CanPromote) return;
        if (!_skillCategoryByLabel.TryGetValue(SelectedPromotionCategoryLabel1, out var category1)) return;
        if (!_skillCategoryByLabel.TryGetValue(SelectedPromotionCategoryLabel2, out var category2)) return;
        if (category1 == category2) return;

        PromotedWarriorPreview = _warrior.CloneAsPromotedHero(PromotedWarriorName);
        PromotedWarriorPreview.AllowedSkillCategories = new List<SkillCategory> { category1, category2 };
        // isSpellcaster: false - le livre n'accorde pas la magie à un Homme de main promu (la Magie
        // reste liée à l'archétype recruté, jamais gagnée par promotion), choix conservateur en
        // l'absence de règle explicite plutôt que de proposer un sort sans école associée.
        NestedHeroRoll = new AdvanceRollEntry(1, isHero: true, PromotedWarriorPreview, new ObservableCollection<AdvanceRollEntry>(), isSpellcaster: false);

        if (_warrior.HeadCount > 1)
            NestedHenchmanRoll = new AdvanceRollEntry(1, isHero: false, _warrior, new ObservableCollection<AdvanceRollEntry>(), isSpellcaster: false, suppressPromotion: true);

        OnPropertyChanged(nameof(PromotedWarriorPreview));
        OnPropertyChanged(nameof(NestedHeroRoll));
        OnPropertyChanged(nameof(NestedHenchmanRoll));
    }

    // --- Caractéristique (voir Core.Rules.AdvanceOutcome.Kind == CharacteristicIncrease) -------------

    /// <summary>Sous-jet 1D6 pour un résultat Héros 6/8/9 (voir CharacteristicChoiceMode.SubRoll1D6) -
    /// saisi à la main ou rempli par AutoRollSubRoll.</summary>
    [ObservableProperty]
    private string manualSubRoll = string.Empty;

    partial void OnManualSubRollChanged(string value) => RecomputeCharacteristicResolution();

    /// <summary>True pour tout résultat sous-jet 1D6 (Héros 6/8/9), résolu ou non - reste affiché même
    /// une fois ResolvedField déterminé (demande explicite : voir le jet ET ce qu'il a donné, pas
    /// juste la confirmation finale), contrairement à ChoiceOptions/NeedsChoice qui eux disparaissent
    /// une fois le choix fait.</summary>
    public bool NeedsSubRoll => Outcome?.ChoiceMode == CharacteristicChoiceMode.SubRoll1D6;

    /// <summary>Libellés localisés (abréviation StatXAbbr) des caractéristiques que le joueur doit
    /// choisir - soit les 2 options de la table (choix libre CC/CT), soit l'ensemble des alternatives
    /// éligibles si le résultat indiqué (fixe, sous-jet, ou les 2 options du choix) est indisponible
    /// (déjà au maximum racial ou, pour un Homme de main, déjà augmentée) - voir
    /// Core.Rules.CharacteristicIncreaseRules. Vide = rien à choisir (soit non applicable, soit déjà
    /// résolu automatiquement).</summary>
    public ObservableCollection<string> ChoiceOptions { get; } = new();

    /// <summary>ChoiceOptions.Count > 0, exposé en bool pour un binding IsVisible direct (pas de
    /// convertisseur "collection non vide" dans l'app - voir CLAUDE.md, les booléens dérivés sont déjà
    /// la convention établie pour ce genre de bascule).</summary>
    public bool NeedsChoice => ChoiceOptions.Count > 0;

    // Nullable (pas string vide) : Picker.SelectedItem est lui-même nullable côté MAUI et repasse à
    // null en pratique (ex. ChoiceOptions vidé/repeuplé par un nouveau jet pendant que ce Picker était
    // sélectionné) - Dictionary.TryGetValue lève ArgumentNullException sur une clé null, d'où le crash
    // en debug avant ce correctif (2026-08-24) si on ne gardait pas value ici.
    [ObservableProperty]
    private string? selectedChoiceLabel = string.Empty;

    partial void OnSelectedChoiceLabelChanged(string? value)
    {
        ResolvedField = value is not null && _choiceFieldByLabel.TryGetValue(value, out var field) ? field : null;
        OnPropertyChanged(nameof(ResolvedField));
        OnPropertyChanged(nameof(ResolvedFieldLabel));
    }

    /// <summary>La caractéristique finalement retenue pour ce résultat +1 - null tant qu'elle n'est pas
    /// déterminée (sous-jet/choix manquant). Appliquée par WarbandDetailViewModel.EndOfGame.
    /// ApplyWarriorOutcomesAsync à l'enregistrement, jamais directement ici (le Warrior vivant n'est
    /// muté qu'au Save, comme le reste de ce wizard).</summary>
    public CharacteristicField? ResolvedField { get; private set; }

    public string ResolvedFieldLabel => ResolvedField is { } f ? _loc[_fieldAbbrKeys[f]] : string.Empty;

    private CharacteristicMaxes Maxes => new(_warrior.MaxMovement, _warrior.MaxWeaponSkill, _warrior.MaxBallisticSkill,
        _warrior.MaxStrength, _warrior.MaxToughness, _warrior.MaxWounds, _warrior.MaxInitiative, _warrior.MaxAttacks, _warrior.MaxLeadership);

    /// <summary>Stats de départ du Warrior + les résolutions déjà faites par les entrées PRÉCÉDENTES de
    /// _siblings (voir sa doc) - jamais Warrior.WeaponSkill etc. directement une fois qu'un palier plus
    /// tôt dans la même Fin de Partie a déjà +1 la même caractéristique.</summary>
    private CharacteristicValues CurrentValues
    {
        get
        {
            var values = new CharacteristicValues(_warrior.Movement, _warrior.WeaponSkill, _warrior.BallisticSkill,
                _warrior.Strength, _warrior.Toughness, _warrior.Wounds, _warrior.Initiative, _warrior.Attacks, _warrior.Leadership);
            foreach (var sibling in _siblings)
            {
                if (ReferenceEquals(sibling, this)) break;
                if (sibling.ResolvedField is { } resolved) values = values.Increment(resolved);
            }
            return values;
        }
    }

    /// <summary>Warrior.IncreasedCharacteristics (Homme de main uniquement) + les résolutions déjà faites
    /// par les entrées précédentes de _siblings cette même session - même principe que CurrentValues.</summary>
    private HashSet<CharacteristicField> AlreadyIncreased
    {
        get
        {
            var set = new HashSet<CharacteristicField>(_warrior.IncreasedCharacteristics);
            foreach (var sibling in _siblings)
            {
                if (ReferenceEquals(sibling, this)) break;
                if (sibling.ResolvedField is { } resolved) set.Add(resolved);
            }
            return set;
        }
    }

    /// <summary>Recalcule ResolvedField/ChoiceOptions à partir d'Outcome/ManualSubRoll - appelée à
    /// chaque changement de jet/sous-jet. Seul BinaryChoice (CC ou CT, Héros 7/Homme de main 6-7) passe
    /// par le mécanisme de repli "toute autre caractéristique" quand les deux options sont indisponibles
    /// - c'est le SEUL cas confirmé par le livre pour ce repli. FixedSingle/SubRoll1D6 se résolvent
    /// TOUJOURS directement sur la caractéristique indiquée par la table/le sous-jet, sans jamais
    /// proposer de picker - un repli y avait été ajouté par généralisation (non confirmée par le livre)
    /// puis retiré sur retour explicite de l'utilisateur (2026-08-24 : "on sait que c'est Endurance +1",
    /// un sous-jet 4-6 sur le 9 physique affichait à tort un choix parmi les 9 caractéristiques).</summary>
    private void RecomputeCharacteristicResolution()
    {
        ChoiceOptions.Clear();
        _choiceFieldByLabel.Clear();
        SelectedChoiceLabel = string.Empty;
        ResolvedField = null;

        if (Outcome?.Kind != AdvanceKind.CharacteristicIncrease)
        {
            NotifyCharacteristicPropertiesChanged();
            return;
        }

        switch (Outcome.ChoiceMode)
        {
            case CharacteristicChoiceMode.FixedSingle:
                ResolvedField = Outcome.FixedField;
                break;
            case CharacteristicChoiceMode.SubRoll1D6:
                if (int.TryParse(ManualSubRoll, out var sub) && sub is >= 1 and <= 6)
                    ResolvedField = sub <= 3 ? Outcome.OptionA : Outcome.OptionB;
                break;
            case CharacteristicChoiceMode.BinaryChoice:
                var resolution = CharacteristicIncreaseRules.ResolveBinaryChoice(Outcome.OptionA!.Value, Outcome.OptionB!.Value,
                    CurrentValues, Maxes, IsHero ? null : AlreadyIncreased);
                if (resolution.ForcedField is { } forced) ResolvedField = forced;
                else if (resolution.FallbackOptions is { } fallback) OfferChoices(fallback);
                else OfferChoices(new[] { Outcome.OptionA!.Value, Outcome.OptionB!.Value });
                break;
        }

        NotifyCharacteristicPropertiesChanged();
    }

    private void OfferChoices(IReadOnlyList<CharacteristicField> fields)
    {
        foreach (var field in fields)
        {
            var label = _loc[_fieldAbbrKeys[field]];
            _choiceFieldByLabel[label] = field;
            ChoiceOptions.Add(label);
        }
    }

    private void NotifyCharacteristicPropertiesChanged()
    {
        OnPropertyChanged(nameof(NeedsSubRoll));
        OnPropertyChanged(nameof(NeedsChoice));
        // ResolvedField (pas seulement son libellé) : la confirmation "<Stat> +1" du XAML est bindée en
        // IsVisible directement sur ResolvedField (IsNotNullConverter) - sans cette notification, le
        // Label ne devenait jamais visible malgré une valeur correctement résolue en interne (bug
        // trouvé le 2026-08-24 : "on a juste le roll, pas le résultat").
        OnPropertyChanged(nameof(ResolvedField));
        OnPropertyChanged(nameof(ResolvedFieldLabel));
    }

    public AdvanceRollEntry(int index, bool isHero, Warrior warrior, ObservableCollection<AdvanceRollEntry> siblings,
        bool isSpellcaster = false, int startingHeroCount = 0, bool suppressPromotion = false)
    {
        Index = index;
        IsHero = isHero;
        _warrior = warrior;
        _siblings = siblings;
        IsSpellcaster = isSpellcaster;
        _startingHeroCount = startingHeroCount;
        _suppressPromotion = suppressPromotion;
        SelectedSkills.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedSkillsText));
            OnPropertyChanged(nameof(HasSkillSelected));
            OnPropertyChanged(nameof(ShowSkillOrSpellButtons));
        };

        if (!IsHero)
        {
            foreach (var category in Enum.GetValues<SkillCategory>())
            {
                var label = _loc[$"SkillCategory{category}"];
                _skillCategoryByLabel[label] = category;
                SkillCategoryOptions.Add(label);
            }
        }
    }

    /// <summary>Message localisé décrivant précisément ce qui manque pour que ce résultat soit complet -
    /// null si rien ne manque (voir IsFullyResolved). Remplace un unique message générique "Un jet est
    /// requis" qui s'appliquait même quand le jet PRINCIPAL était déjà fait mais qu'il restait un choix
    /// à faire (compétence/sort, sous-jet, caractéristique...) - demande explicite du 2026-08-24 : "trop
    /// étrange de dire qu'il faut le jet alors qu'on l'a fait". Seul EndOfGameDialogViewModel.
    /// ValidateAdvanceStep l'utilise (posé dans AdvanceRollEntry.RollError). Recouvre récursivement le
    /// cas Promotion (NestedHeroRoll/NestedHenchmanRoll) - jamais de cycle possible, ceux-ci ne peuvent
    /// eux-mêmes jamais être Kind == Promotion (voir SuppressPromotion et HeroAdvanceTable, qui n'a
    /// aucune entrée Promotion).</summary>
    public string? MissingRequirementMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ResultText)) return _loc["EndOfGameRollRequired"];

            switch (Outcome?.Kind)
            {
                case AdvanceKind.CharacteristicIncrease when ResolvedField is null:
                    return NeedsSubRoll ? _loc["EndOfGameSubRollRequired"] : _loc["EndOfGameAdvanceChoiceRequired"];
                case AdvanceKind.Skill when !HasSkillSelected && !HasSpellSelected:
                    return _loc["EndOfGameAdvanceSkillRequired"];
                case AdvanceKind.Promotion when !CanPromote:
                    return _loc["EndOfGamePromotionBlockedRequired"];
                case AdvanceKind.Promotion when string.IsNullOrWhiteSpace(PromotedWarriorName):
                    return _loc["EndOfGamePromotionNameRequired"];
                case AdvanceKind.Promotion when NestedHeroRoll is null:
                    return _loc["EndOfGamePromotionCategoriesRequired"];
                case AdvanceKind.Promotion when NestedHeroRoll!.MissingRequirementMessage is { } heroMessage:
                    return string.Format(_loc["EndOfGamePromotionHeroRollRequired"], heroMessage);
                case AdvanceKind.Promotion when NestedHenchmanRoll?.MissingRequirementMessage is { } henchmanMessage:
                    return string.Format(_loc["EndOfGamePromotionRemainderRollRequired"], henchmanMessage);
                default:
                    return null;
            }
        }
    }

    public bool IsFullyResolved => MissingRequirementMessage is null;
}
