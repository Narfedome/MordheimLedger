using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Héros promus pendant cette Fin de partie (« The lad's got talent ») : Héros dès l'étape
/// Expérience, ils comptent pour l'Exploration (un dé de plus), peuvent être désignés quand un résultat vise
/// « un de vos Héros » (Héros envoyé de la Fosse, test du Puits, XP répartie) et font leur recherche d'objet
/// rare, comme les autres Héros - lecture de la séquence d'après-bataille retenue par l'utilisateur le
/// 2026-11-11. Pas pour l'Arme bénie : elle vise une ligne d'équipement déjà en base, et celui du promu n'est
/// copié qu'à l'enregistrement. À Terminer, l'Exploration est enregistrée avant la promotion : l'XP répartie
/// au promu et ses jets de Progression d'Exploration sont donc appliqués avec la promotion
/// (Apply.WarriorOutcomes), son statut Malade/Mort après (ApplySicknessLifecycleAsync).</summary>
public partial class EndOfGamePageViewModel
{
    private PromotionContext _promotionContext = new(0, []);

    /// <summary>Une ligne par Héros promu (le Warrior en attente de création, PromotedWarriorPreview) - jamais
    /// ajoutée à WarriorRows (elle referait les étapes Expérience, Blessures… et compterait deux fois dans la
    /// Valeur de Bande). Reconstruite si l'aperçu change (nouveau jet ou nouvelles listes = nouvel objet).</summary>
    private readonly Dictionary<AdvanceRollEntry, WarriorOutcomeRow> _promotedHeroRows = new();

    private IEnumerable<WarriorOutcomeRow> PromotedHeroRows => _promotedHeroRows.Values;

    /// <summary>WarriorRows + lignes des Héros promus : tout ce qui peut porter un jet de Progression.</summary>
    private IEnumerable<WarriorOutcomeRow> AllOutcomeRows => WarriorRows.Concat(PromotedHeroRows);

    /// <summary>Lignes du récapitulatif : chaque guerrier, puis chaque Héros promu (ce que l'Exploration lui apporte).</summary>
    public List<WarriorOutcomeRow> RecapWarriorRows => AllOutcomeRows.ToList();

    /// <summary>Héros encore en vie désignables par un résultat d'Exploration, promus compris.</summary>
    private List<WarriorOutcomeRow> ExplorationEligibleHeroes =>
        WarriorRows.Where(r => r.IsHero && !r.IsDeadFromInjuries).Concat(PromotedHeroRows).ToList();

    /// <summary>Promotions dont le Héros est déjà constitué (nom et deux listes choisis).</summary>
    private IEnumerable<(AdvanceRollEntry Entry, Warrior Hero)> PromotedHeroes =>
        _promotionContext.Promotions
            .Where(e => e.PromotedWarriorPreview is not null)
            .Select(e => (e, e.PromotedWarriorPreview!));

    /// <summary>Un dé d'Exploration par Héros promu - le modèle a survécu à la bataille (il a gagné de l'XP).</summary>
    private int PromotedHeroCount => _promotionContext.Promotions.Count;

    /// <summary>Ligne du Héros promu par cette entrée, si elle correspond toujours à son aperçu actuel.</summary>
    private WarriorOutcomeRow? PromotedHeroRowFor(AdvanceRollEntry entry, Warrior promoted) =>
        _promotedHeroRows.TryGetValue(entry, out var row) && row.Warrior == promoted ? row : null;

    /// <summary>Crée la ligne (et la recherche d'objet rare) de chaque Héros promu, retire celles d'une promotion
    /// annulée ou refaite en revenant en arrière - appelée à chaque changement d'étape.</summary>
    private void SyncPromotedHeroRows()
    {
        var current = PromotedHeroes.ToList();
        var changed = false;
        foreach (var stale in _promotedHeroRows.Where(kv => current.All(c => c.Entry != kv.Key || c.Hero != kv.Value.Warrior)).ToList())
        {
            RemovePromotedHeroRow(stale.Key, stale.Value);
            changed = true;
        }

        foreach (var (entry, hero) in current)
        {
            if (_promotedHeroRows.ContainsKey(entry)) continue;
            var groupRow = WarriorRows.First(r => r.AdvanceRolls.Contains(entry) || r.ExplorationAdvanceRolls.Contains(entry));
            // XP de départ = celle du groupe après la bataille : c'est d'elle que partent les paliers de Héros
            // franchis grâce à l'XP d'Exploration (les jets de la partie, eux, sont ceux du groupe).
            var row = new WarriorOutcomeRow(hero, groupRow.ArchetypeName, gainsExperience: true)
            {
                ExperienceGainedText = groupRow.ExperienceGainedText,
                IsPromotedHero = true,
            };
            row.PropertyChanged += OnPromotedHeroRowChanged;
            _promotedHeroRows[entry] = row;
            var search = new RareItemSearchEntry(row, Loc, _ownedEquipmentItemIds);
            search.PropertyChanged += OnRareItemSearchEntryChanged;
            RareItemSearchEntries.Add(search);
            changed = true;
        }

        if (!changed) return;
        OnPropertyChanged(nameof(HasEligibleHeroesForRareItems));
        OnPropertyChanged(nameof(SentHeroOptions));
        OnPropertyChanged(nameof(StatTestHeroOptions));
        OnPropertyChanged(nameof(DistributedExperienceEligibleHeroes));
        OnPropertyChanged(nameof(DistributedExperienceRemaining));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(IsLastStep));
    }

    private void RemovePromotedHeroRow(AdvanceRollEntry entry, WarriorOutcomeRow row)
    {
        foreach (var search in RareItemSearchEntries.Where(s => s.Hero == row).ToList())
        {
            search.PropertyChanged -= OnRareItemSearchEntryChanged;
            RareItemSearchEntries.Remove(search);
        }
        row.PropertyChanged -= OnPromotedHeroRowChanged;
        _promotedHeroRows.Remove(entry);
        // Un choix d'Exploration qui le désignait retombe sur « aucun » plutôt que de viser un Héros disparu.
        if (SentHero == row) SelectedSentHeroOption = SentHeroOptions[0];
        if (StatTestHero == row) SelectedStatTestHeroOption = StatTestHeroOptions[0];
    }

    /// <summary>Même rafraîchissement que l'abonnement aux WarriorRows (InitializeAsync) pour ce qui concerne
    /// un Héros promu : XP répartie et second passage Progression.</summary>
    private void OnPromotedHeroRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WarriorOutcomeRow.DistributedExplorationExperience) or nameof(WarriorOutcomeRow.LeaderExplorationExperience))
        {
            OnPropertyChanged(nameof(StepLabel));
            OnPropertyChanged(nameof(IsLastStep));
        }
        if (e.PropertyName == nameof(WarriorOutcomeRow.DistributedExplorationExperience))
            OnPropertyChanged(nameof(DistributedExperienceRemaining));
    }
}
