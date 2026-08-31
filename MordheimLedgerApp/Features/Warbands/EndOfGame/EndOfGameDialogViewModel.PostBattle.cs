using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Post-battle sequence steps 4 (Selling Wyrdstone) and 5 (Check available veterans) - both
/// whole-band, single-card steps (like Captives/Experience), never per-warrior. See
/// EndOfGameDialogViewModel.Steps for their conditional placement.</summary>
public partial class EndOfGameDialogViewModel
{
    // --- Étape : Vente de pierre magique (livre, étape 4) ---------------------------------------

    /// <summary>Combien d'éclats le joueur vend CETTE fois - jamais obligatoire de tout vendre d'un coup
    /// (voir Warband.WyrdstoneShards), borné par les deux commandes ci-dessous plutôt qu'une saisie
    /// libre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WyrdstoneSaleValue))]
    [NotifyPropertyChangedFor(nameof(WyrdstoneSaleValueDisplay))]
    [NotifyPropertyChangedFor(nameof(WyrdstoneSaleTableRows))]
    private int shardsToSell;

    /// <summary>Stock déjà en réserve (_currentWyrdstoneShards, figé à l'ouverture du wizard) PLUS ce que
    /// CETTE Fin de Partie vient de trouver, toutes sources confondues (FoundThisGameWyrdstoneShards) -
    /// le livre ne distingue pas "ancien" et "nouveau" stock, la pierre magique tout juste trouvée doit
    /// être revendable dans la même séance (retour explicite de l'utilisateur 2026-08-28 : sans ça,
    /// aucune progression possible dès la première partie). Steps utilise la même somme pour décider si
    /// cette étape existe du tout.</summary>
    public int MaxShardsToSell => _currentWyrdstoneShards + FoundThisGameWyrdstoneShards;

    /// <summary>Combien d'éclats CETTE Fin de Partie vient de trouver, toutes sources confondues : le
    /// barème universel de chaque jet d'Exploration (BaselineWyrdstoneShardsFound, TOUJOURS déclenché)
    /// PLUS l'éventuel bonus d'un résultat spécifique qui donne aussi de la pierre magique en propre
    /// (Puits/Bâtiment Éventré/La Fosse - IsExplorationWyrdstone/ExplorationWyrdstoneAmount, les deux
    /// s'appliquent l'un ET l'autre, voir ApplyExplorationOutcomeAsync). Sert à la fois au plafond
    /// (MaxShardsToSell) et à l'explication affichée à l'étape Vente (retour utilisateur 2026-08-28 :
    /// "voir d'où sortent nos shards" plutôt qu'un nombre nu).</summary>
    public int FoundThisGameWyrdstoneShards => BaselineWyrdstoneShardsFound +
        (IsExplorationWyrdstone && int.TryParse(ExplorationWyrdstoneAmount, out var located) ? located : 0);

    public bool HasFoundThisGameWyrdstone => FoundThisGameWyrdstoneShards > 0;

    public string FoundThisGameWyrdstoneDisplay => string.Format(Loc["EndOfGameWyrdstoneFoundThisGameFormat"], FoundThisGameWyrdstoneShards);

    public bool HasExistingWyrdstoneStock => _currentWyrdstoneShards > 0;

    public string ExistingWyrdstoneStockDisplay => string.Format(Loc["EndOfGameWyrdstoneExistingStockFormat"], _currentWyrdstoneShards);

    /// <summary>Nombre de guerriers actuellement dans la bande, pour la colonne du barème de vente
    /// (Core.Rules.WyrdstoneSaleTable) - recalculé à chaud (pas mis en cache) pour refléter tout aller-
    /// retour du joueur sur l'étape Blessure qui changerait IsDead entre-temps. Approximation acceptée :
    /// un groupe d'Hommes de main qui perd SEULEMENT une partie de ses figurines cette bataille (IsDead
    /// reste faux tant qu'il en reste au moins une) compte encore son HeadCount D'AVANT ces pertes - la
    /// colonne exacte du barème n'est pas assez sensible à 1-2 guerriers près pour que ça vaille la peine
    /// de recalculer un HeadCount survivant précis ici. Francs-Tireurs exclus (texte du livre, "Recruiting
    /// Hired Swords" : "Hired Swords do not count towards the maximum number of warriors... and don't
    /// affect your income from selling wyrdstone" - repéré 2026-08-28 en lisant le texte de l'étape 7,
    /// corrigé ici avant de l'oublier).</summary>
    public int CurrentWarriorCount => WarriorRows.Where(r => !r.IsDead && !r.Warrior.IsHiredSword).Sum(r => r.HeadCount);

    public int WyrdstoneSaleValue => WyrdstoneSaleTable.GetNetGold(ShardsToSell, CurrentWarriorCount);

    /// <summary>Formatted "Vendre {N} éclat(s) pour {M} CO" text - computed here (string.Format, same
    /// split as WarriorsHatredChipFormat/SpecialRuleChip: Core/ViewModel stay in charge of formatting,
    /// XAML just binds the result) rather than nesting a {loc:Loc} markup extension inside a
    /// StringFormat attribute, which isn't valid XAML.</summary>
    public string WyrdstoneSaleValueDisplay => string.Format(Loc["EndOfGameWyrdstoneSaleValueFormat"], ShardsToSell, WyrdstoneSaleValue);

    /// <summary>Représentatif d'un guerrier "typique" de chaque tranche du barème (1/4/7/10/13/16) -
    /// n'importe quel nombre DANS la tranche donnerait le même résultat (voir Core.Rules.
    /// WyrdstoneSaleTable.GetNetGold), ces 6 valeurs ne servent qu'à peupler WyrdstoneSaleTableRows sans
    /// dupliquer le barème lui-même en dur ici.</summary>
    private static readonly int[] RepresentativeWarriorCounts = { 1, 4, 7, 10, 13, 16 };

    /// <summary>Barème complet (livre, "Selling Wyrdstone") affiché en repère sous l'aperçu du gain -
    /// retour utilisateur 2026-08-28 ("mettre le barème sous tout ça"). Reconstruit à chaque changement
    /// de ShardsToSell plutôt que mis en cache (8 lignes, coût négligeable) pour recalculer
    /// HighlightedBucketNumber (la SEULE case correspondant au choix courant du joueur - ligne = éclats
    /// vendus, colonne = effectif actuel de la bande, voir CurrentWarriorCount) - lit Core.Rules.
    /// WyrdstoneSaleTable comme seule source de vérité, aucun chiffre ni seuil de colonne dupliqué en dur
    /// ici.</summary>
    public List<WyrdstoneSaleTableRow> WyrdstoneSaleTableRows => Enumerable.Range(1, 8).Select(shards =>
    {
        var values = RepresentativeWarriorCounts.Select(w => WyrdstoneSaleTable.GetNetGold(shards, w)).ToList();
        var isSelectedRow = ShardsToSell > 0 && shards == Math.Min(ShardsToSell, 8);
        return new WyrdstoneSaleTableRow
        {
            ShardsLabel = shards == 8 ? "8+" : shards.ToString(),
            Bucket1 = values[0],
            Bucket2 = values[1],
            Bucket3 = values[2],
            Bucket4 = values[3],
            Bucket5 = values[4],
            Bucket6 = values[5],
            HighlightedBucketNumber = isSelectedRow ? WyrdstoneSaleTable.GetWarriorCountBucketIndex(CurrentWarriorCount) : 0
        };
    }).ToList();

    /// <summary>Choke point partagé pour tout ce qui dérive de FoundThisGameWyrdstoneShards - appelé
    /// depuis ResolveExplorationResult (un dé d'Exploration change) ET OnExplorationWyrdstoneAmountChanged
    /// (le montant d'un résultat Puits/Bâtiment Éventré/La Fosse est saisi/modifié séparément, après la
    /// résolution du dé), les deux dans EndOfGameDialogViewModel.Exploration.cs.</summary>
    private void NotifyWyrdstoneFoundThisGameChanged()
    {
        OnPropertyChanged(nameof(FoundThisGameWyrdstoneShards));
        OnPropertyChanged(nameof(HasFoundThisGameWyrdstone));
        OnPropertyChanged(nameof(FoundThisGameWyrdstoneDisplay));
        OnPropertyChanged(nameof(MaxShardsToSell));
    }

    [RelayCommand]
    private void IncrementShardsToSell() => ShardsToSell = Math.Min(MaxShardsToSell, ShardsToSell + 1);

    [RelayCommand]
    private void DecrementShardsToSell() => ShardsToSell = Math.Max(0, ShardsToSell - 1);

    // --- Étape : Disponibilité des Vétérans (livre, étape 5) -------------------------------------

    /// <summary>Total du jet de 2D6 ("Roll 2D6 to see how much Experience worth of veterans is available
    /// for hire") - saisie libre comme tout autre jet du wizard (le joueur peut taper directement le
    /// total d'un jet physique), pas deux champs séparés par dé : aucune règle du livre ne distingue les
    /// deux dés individuellement ici (contrairement à un test de caractéristique avec exception sur un
    /// 6), seul le total compte.</summary>
    [ObservableProperty]
    private string? veteranExperienceRoll;

    partial void OnVeteranExperienceRollChanged(string? value) => VeteranExperienceRollError = null;

    [ObservableProperty]
    private string? veteranExperienceRollError;

    [RelayCommand]
    private void AutoRollVeteranExperience() => VeteranExperienceRoll = (Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7)).ToString();

    private bool ValidateAvailableVeteransStep() =>
        CheckRoll(!int.TryParse(VeteranExperienceRoll, out _), () => VeteranExperienceRollError = Loc["EndOfGameRollRequired"]);
}
