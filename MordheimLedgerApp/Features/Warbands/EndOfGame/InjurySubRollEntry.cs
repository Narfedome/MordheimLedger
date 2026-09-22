using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Shape shared by WarriorOutcomeRow's main roll and InjurySubRollEntry for the "Vendu aux
/// Fosses" (65) gladiator duel state (WonPitFight/SoldToPitsRerollRoll) - lets EndOfGameDialogViewModel
/// bind/validate the PitFight step against whichever occurrence is current (the main roll, or one
/// specific "Blessures multiples" sub-roll) without a big if/else per property. See
/// EndOfGameDialogViewModel.CurrentPitFightOutcome (2026-09-04, retour utilisateur - "à chaque blessure
/// on refait un D66 avec le sous-jet adéquat", extending Vendu aux Fosses to sub-rolls too).</summary>
public interface IPitFightOutcome
{
    bool WonPitFight { get; set; }
    ObservableCollection<InjurySubRollEntry> SoldToPitsRerollRoll { get; }
    bool HasSoldToPitsRerollRoll { get; }
}

/// <summary>One injury roll stacked under a WarriorOutcomeRow, in one of three unrelated situations that
/// happen to share the exact same shape (an indexed D66/D6 roll auto-resolving to text): (1) a Hero's
/// D66 sub-roll from a "Blessures multiples" result (16/21) - see
/// WarriorOutcomeRow.MultipleInjuryRolls/PopulateMultipleInjuryRolls; (2) one D6 roll per Henchman
/// group model marked out of action - see WarriorOutcomeRow.FigureInjuryRolls/SyncFigureInjuryRolls;
/// (3) the single reroll on a lost "Vendu aux Fosses" gladiator duel - see WonPitFight/
/// SoldToPitsRerollRoll below, on either a WarriorOutcomeRow's main roll or (since 2026-09-04) this very
/// type recursively for a "Blessures multiples" sub-roll's own duel.
/// IsHero picks which table resolves ManualRoll (always Hero/D66 for cases 1/3, always Henchman/D6 for
/// case 2 - never mixed within one collection). Same "accept the result as-is" stance as the main
/// injury roll for a sub-roll landing on Dead/Captured/Multiple Injuries again: the rulebook says to
/// re-roll but the app leaves that to the player rather than enforcing it (see SeriousInjuryTable's doc
/// comment).
///
/// **2026-09-04, retour utilisateur ("les rolls des blessures doivent être les mêmes qu'une blessure
/// classique... on peut tirer 2 fois Folie qui nous fait roll la folie une fois chacun") : cette entrée
/// gagne désormais TOUT ce qu'un jet principal (WarriorOutcomeRow) sait faire pour un Héros - Rancune
/// (56), la branche Blessure au bras/Jambe écrasée/Folie (23/25/24, avec sa chip de règle spéciale) et
/// Vendu aux Fosses (65, avec sa propre relance en cas de défaite) - en plus de Blessure profonde (35)
/// et Capturé (61) déjà présents. Seule exception, confirmée explicitement par l'utilisateur : un
/// sous-jet qui tombe lui-même sur Blessures multiples (16/21) NE réexplose PAS en sous-sous-jets, il
/// reste texte de référence pur ("pour une 2ème blessure multiple, on ne peut pas en envoyer une autre
/// si on est déjà en blessure multiple") - cette entrée n'a d'ailleurs jamais porté de mécanisme
/// d'explosion, donc rien à retirer pour respecter cette règle, elle est automatiquement satisfaite.
/// Ces nouveaux champs sont câblés en XAML uniquement pour l'usage "Blessures multiples" (cas 1
/// ci-dessus) et pour Vendu aux Fosses lui-même (cas 3) - le sous-jet de relance d'un combat de
/// gladiateur (SoldToPitsRerollRoll) reste affiché avec seulement Blessure profonde/Capturé comme avant
/// cette passe (pas de section Rancune/Branche/nouveau duel dans son propre DataTemplate) : rejouer un
/// combat de gladiateur qui en amène un autre est un cas si marginal (il faudrait retomber sur 65 une
/// deuxième fois d'affilée) qu'aucune UI dédiée n'a été construite pour lui - la donnée resterait
/// simplement non saisie si ça arrivait, sans planter (ShowSoldToThePits/HasHatredTarget etc. répondent
/// simplement false/vide faute de saisie possible sur cette instance précise). À généraliser si ce cas
/// devient un jour un vrai besoin.</summary>
public partial class InjurySubRollEntry : ObservableObject, IPitFightOutcome
{
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly string _labelKey;
    private readonly IReadOnlyList<Injury> _injuryCatalog;

    public int Index { get; }
    public int Total { get; set; }
    public bool IsHero { get; }
    public string Label => string.Format(_loc[_labelKey], Index, Total);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDeepWoundSubRoll))]
    [NotifyPropertyChangedFor(nameof(ShowCapturedChoice))]
    [NotifyPropertyChangedFor(nameof(ShowHatredSection))]
    [NotifyPropertyChangedFor(nameof(ShowInjuryBranchSubRoll))]
    [NotifyPropertyChangedFor(nameof(InjuryBranchSpecialRules))]
    [NotifyPropertyChangedFor(nameof(HasInjuryBranchSpecialRules))]
    [NotifyPropertyChangedFor(nameof(ShowSoldToThePits))]
    private string manualRoll = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.RollError, posé uniquement par
    /// EndOfGameDialogViewModel.Next si ce jet est encore vide/invalide à ce moment-là.</summary>
    [ObservableProperty]
    private string? rollError;

    partial void OnManualRollChanged(string value)
    {
        InjuryResultText = string.Empty;
        if (!int.TryParse(value, out var roll))
        {
            if (DeepWoundSubRoll.Length > 0) DeepWoundSubRoll = string.Empty;
            if (HatredSubRoll.Length > 0) HatredSubRoll = string.Empty;
            if (InjuryBranchSubRoll.Length > 0) InjuryBranchSubRoll = string.Empty;
            if (WonPitFight) WonPitFight = false;
            if (SoldToPitsRerollRoll.Count > 0) { SoldToPitsRerollRoll.Clear(); OnPropertyChanged(nameof(HasSoldToPitsRerollRoll)); }
            return;
        }

        bool found;
        string key;
        found = IsHero ? SeriousInjuryTable.TryGetTextKey(roll, out key) : HenchmanInjuryTable.TryGetTextKey(roll, out key);
        if (found)
        {
            InjuryResultText = _loc[key];
            RollError = null;
        }

        // Même principe que WarriorOutcomeRow.OnManualRollChanged pour chaque sous-jet - un nouveau jet
        // qui ne tombe plus sur le résultat concerné invalide la saisie déjà faite.
        if (!ShowDeepWoundSubRoll && DeepWoundSubRoll.Length > 0)
            DeepWoundSubRoll = string.Empty;

        if (!ShowCapturedChoice && IsRansomed)
            IsRansomed = false;

        if (!ShowHatredSection && HatredSubRoll.Length > 0)
            HatredSubRoll = string.Empty;

        if (!ShowInjuryBranchSubRoll && InjuryBranchSubRoll.Length > 0)
            InjuryBranchSubRoll = string.Empty;

        if (ShowSoldToThePits && !WonPitFight && SoldToPitsRerollRoll.Count == 0)
            PopulateSoldToPitsRerollRoll();
        if (!ShowSoldToThePits && WonPitFight)
            WonPitFight = false;
        if (!ShowSoldToThePits && SoldToPitsRerollRoll.Count > 0)
        {
            SoldToPitsRerollRoll.Clear();
            OnPropertyChanged(nameof(HasSoldToPitsRerollRoll));
        }
    }

    [ObservableProperty]
    private string injuryResultText = string.Empty;

    /// <summary>Même principe que WarriorOutcomeRow.ShowDeepWoundSubRoll, pour un sous-jet "Blessures
    /// multiples" (16/21) qui tombe lui-même sur 35 - toujours faux pour un jet Homme de main (IsHero
    /// false), qui utilise une table D6 totalement différente sans résultat 35.</summary>
    public bool ShowDeepWoundSubRoll => IsHero && int.TryParse(ManualRoll, out var roll) && roll == 35;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeepWoundConfirmationText))]
    private string deepWoundSubRoll = string.Empty;

    partial void OnDeepWoundSubRollChanged(string value)
    {
        if (HasValidDeepWoundSubRoll) DeepWoundRollError = null;
    }

    public bool HasValidDeepWoundSubRoll => int.TryParse(DeepWoundSubRoll, out var subRoll) && subRoll is >= 1 and <= 3;

    public string DeepWoundConfirmationText =>
        HasValidDeepWoundSubRoll && int.TryParse(DeepWoundSubRoll, out var subRoll)
            ? string.Format(_loc["EndOfGameDeepWoundResultFormat"], subRoll)
            : string.Empty;

    [ObservableProperty]
    private string? deepWoundRollError;

    /// <summary>Même principe que WarriorOutcomeRow.ShowCapturedChoice, pour un sous-jet "Blessures
    /// multiples" qui tombe lui-même sur 61.</summary>
    public bool ShowCapturedChoice => IsHero && int.TryParse(ManualRoll, out var roll) && roll == 61;

    [ObservableProperty]
    private bool isRansomed;

    partial void OnIsRansomedChanged(bool value)
    {
        if (!value) RansomAmount = string.Empty;
    }

    [ObservableProperty]
    private string ransomAmount = string.Empty;

    public bool HasValidRansomAmount => int.TryParse(RansomAmount, out var amount) && amount >= 0;

    partial void OnRansomAmountChanged(string value)
    {
        if (HasValidRansomAmount) CapturedChoiceError = null;
    }

    [ObservableProperty]
    private string? capturedChoiceError;

    /// <summary>Même principe que WarriorOutcomeRow.ShowHatredSection, pour un sous-jet "Blessures
    /// multiples" qui tombe lui-même sur "Rancune" (56) - même sous-jet 1D6 de portée, même choix de
    /// cible (texte libre ou WarbandArchetype du catalogue pour la portée 6).</summary>
    public bool ShowHatredSection => IsHero && int.TryParse(ManualRoll, out var roll) && SeriousInjuryTable.IsBitterEnmity(roll);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatredScope))]
    [NotifyPropertyChangedFor(nameof(ShowHatredFreeTextEntry))]
    [NotifyPropertyChangedFor(nameof(ShowHatredArchetypePicker))]
    [NotifyPropertyChangedFor(nameof(HatredFreeTextPlaceholder))]
    private string hatredSubRoll = string.Empty;

    public HatredTargetKind? HatredScope =>
        int.TryParse(HatredSubRoll, out var roll) && HatredTargetTable.TryGetOutcome(roll, out var kind) ? kind : null;

    public bool ShowHatredFreeTextEntry => HatredScope is HatredTargetKind.SpecificWarrior or HatredTargetKind.SpecificWarband;

    public string HatredFreeTextPlaceholder => HatredScope == HatredTargetKind.SpecificWarband
        ? _loc["EndOfGameHatredBandNamePh"]
        : _loc["EndOfGameHatredIndividualNamePh"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHatredTarget))]
    private string hatredTargetFreeTextInput = string.Empty;

    partial void OnHatredTargetFreeTextInputChanged(string value)
    {
        HatredTargetDisplayName = value;
        HatredTargetFreeText = string.IsNullOrWhiteSpace(value) ? null : value;
        if (!string.IsNullOrWhiteSpace(value)) HatredRollError = null;
    }

    public bool ShowHatredArchetypePicker => HatredScope == HatredTargetKind.WarbandArchetype;

    partial void OnHatredSubRollChanged(string value)
    {
        HatredTargetWarbandArchetype = null;
        HatredTargetWarbandArchetypeId = null;
        HatredTargetFreeText = null;
        HatredTargetFreeTextInput = string.Empty;
        HatredTargetDisplayName = string.Empty;
        HatredRollError = null;
    }

    public int? HatredTargetWarbandArchetypeId { get; private set; }
    public string? HatredTargetFreeText { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHatredTarget))]
    private string hatredTargetDisplayName = string.Empty;

    public bool HasHatredTarget => HatredTargetDisplayName.Length > 0;

    [ObservableProperty]
    private string? hatredRollError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHatredTarget))]
    private WarbandArchetype? hatredTargetWarbandArchetype;

    /// <summary>Appelée par EndOfGameDialogViewModel.Injury.PickSubHatredWarbandArchetype une fois le
    /// dialog résolu (portée 6 uniquement).</summary>
    public void SetHatredTarget(WarbandArchetype archetype)
    {
        HatredTargetWarbandArchetype = archetype;
        HatredTargetWarbandArchetypeId = archetype.Id;
        HatredTargetFreeText = null;
        HatredTargetDisplayName = archetype.Name;
        HatredRollError = null;
    }

    public void ClearHatredTargetWarbandArchetype()
    {
        HatredTargetWarbandArchetype = null;
        HatredTargetWarbandArchetypeId = null;
        HatredTargetDisplayName = string.Empty;
    }

    /// <summary>Même principe que WarriorOutcomeRow.ShowInjuryBranchSubRoll, pour un sous-jet
    /// "Blessures multiples" qui tombe lui-même sur "Blessure au bras" (23), "Jambe écrasée" (25) ou
    /// "Folie" (24).</summary>
    public bool ShowInjuryBranchSubRoll => IsHero && int.TryParse(ManualRoll, out var roll) && SeriousInjuryEffectTable.RequiresBranchSubRoll(roll);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InjuryBranchResultText))]
    [NotifyPropertyChangedFor(nameof(ResolvedInjuryText))]
    [NotifyPropertyChangedFor(nameof(InjuryBranchSpecialRules))]
    [NotifyPropertyChangedFor(nameof(HasInjuryBranchSpecialRules))]
    private string injuryBranchSubRoll = string.Empty;

    partial void OnInjuryBranchSubRollChanged(string value)
    {
        if (HasValidInjuryBranchSubRoll) InjuryBranchRollError = null;
    }

    public string InjuryBranchResultText =>
        int.TryParse(ManualRoll, out var roll) && int.TryParse(InjuryBranchSubRoll, out var subRoll) &&
        SeriousInjuryTable.TryGetBranchTextKey(roll, subRoll, out var key) ? _loc[key] : string.Empty;

    /// <summary>Même rôle que WarriorOutcomeRow.ResolvedInjuryText - le texte à utiliser partout où "le
    /// résultat de ce sous-jet" est affiché/enregistré.</summary>
    public string ResolvedInjuryText => InjuryBranchResultText.Length > 0 ? InjuryBranchResultText : InjuryResultText;

    public IReadOnlyList<SpecialRule> InjuryBranchSpecialRules
    {
        get
        {
            if (!int.TryParse(ManualRoll, out var roll) || !int.TryParse(InjuryBranchSubRoll, out var subRoll))
                return Array.Empty<SpecialRule>();

            var category = IsHero ? InjuryCategory.Hero : InjuryCategory.Henchman;
            return (IReadOnlyList<SpecialRule>?)InjuryCatalogLookup.Find(_injuryCatalog, category, roll, subRoll)?.SpecialRules ?? Array.Empty<SpecialRule>();
        }
    }

    public bool HasInjuryBranchSpecialRules => InjuryBranchSpecialRules.Count > 0;

    public SeriousInjuryOutcome? InjuryBranchOutcome =>
        int.TryParse(ManualRoll, out var roll) && int.TryParse(InjuryBranchSubRoll, out var subRoll) &&
        SeriousInjuryEffectTable.TryGetBranchSubRollOutcome(roll, subRoll, out var outcome) ? outcome : null;

    public bool HasValidInjuryBranchSubRoll => int.TryParse(InjuryBranchSubRoll, out var subRoll) && subRoll is >= 1 and <= 6;

    [ObservableProperty]
    private string? injuryBranchRollError;

    /// <summary>Même principe que WarriorOutcomeRow.ShowSoldToThePits, pour un sous-jet "Blessures
    /// multiples" qui tombe lui-même sur "Vendu aux Fosses" (65) - déclenche sa propre étape de combat de
    /// gladiateur (EndOfGameDialogViewModel.Steps, une étape PAR occurrence plutôt qu'une seule pour tout
    /// le guerrier, 2026-09-04 retour utilisateur).</summary>
    public bool ShowSoldToThePits => IsHero && int.TryParse(ManualRoll, out var roll) && roll == 65;

    /// <summary>Coché si le joueur gagne CE combat de gladiateur précis - décoché (par défaut) signifie
    /// défaite (un sous-jet supplémentaire, voir SoldToPitsRerollRoll). Interface IPitFightOutcome.</summary>
    [ObservableProperty]
    private bool wonPitFight;

    partial void OnWonPitFightChanged(bool value)
    {
        if (value && SoldToPitsRerollRoll.Count > 0)
        {
            SoldToPitsRerollRoll.Clear();
            OnPropertyChanged(nameof(HasSoldToPitsRerollRoll));
        }
        else if (!value && ShowSoldToThePits && SoldToPitsRerollRoll.Count == 0)
        {
            PopulateSoldToPitsRerollRoll();
        }
    }

    /// <summary>Défaite de CE combat de gladiateur : un unique sous-jet D66 (relance sur la même table),
    /// même patron que WarriorOutcomeRow.SoldToPitsRerollRoll - y compris sa propre profondeur récursive
    /// (ce sous-jet est lui-même un InjurySubRollEntry), même si aucune UI ne pousse cette récursion plus
    /// loin qu'un niveau dans cette passe (voir la doc de classe ci-dessus).</summary>
    public ObservableCollection<InjurySubRollEntry> SoldToPitsRerollRoll { get; } = new();
    public bool HasSoldToPitsRerollRoll => SoldToPitsRerollRoll.Count > 0;

    private void PopulateSoldToPitsRerollRoll()
    {
        var entry = new InjurySubRollEntry(1, 1, isHero: true, labelKey: "EndOfGameSoldToPitsRerollLabel", injuryCatalog: _injuryCatalog);
        SoldToPitsRerollRoll.Add(entry);
        OnPropertyChanged(nameof(HasSoldToPitsRerollRoll));
    }

    /// <summary>True si le jet actuellement saisi est un résultat de mort (Héros 11-15, Homme de main
    /// 1-2) - utilisé par WarbandDetailViewModel.EndOfGame pour compter les figurines perdues dans un
    /// groupe d'Hommes de main (voir WarriorOutcomeRow.FigureInjuryRolls), et depuis 2026-09-04 aussi
    /// pour un sous-jet "Blessures multiples" tombant lui-même sur la Mort (jusque-là non appliqué du
    /// tout pour ce cas précis - un vrai manque, pas seulement Rancune/Branche/Vendu aux Fosses).</summary>
    public bool IsDeath => int.TryParse(ManualRoll, out var roll) && (IsHero ? SeriousInjuryTable.IsDeath(roll) : HenchmanInjuryTable.IsDeath(roll));

    public InjurySubRollEntry(int index, int total, bool isHero, string labelKey, IReadOnlyList<Injury>? injuryCatalog = null)
    {
        Index = index;
        Total = total;
        IsHero = isHero;
        _labelKey = labelKey;
        _injuryCatalog = injuryCatalog ?? Array.Empty<Injury>();
    }

    /// <summary>Steps.SyncFigureInjuryRolls-style syncs (voir WarriorOutcomeRow.SyncFigureInjuryRolls)
    /// n'ajoutent/ne retirent qu'en bout de liste et préservent les entrées existantes - mais Total (le
    /// nombre total affiché dans Label, ex. "Figurine 2/3") doit rester à jour sur celles-ci quand le
    /// compte global change.</summary>
    public void UpdateTotal(int total)
    {
        if (Total == total) return;
        Total = total;
        OnPropertyChanged(nameof(Label));
    }
}
