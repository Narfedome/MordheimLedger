using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Héros promus pendant cette Fin de partie (« The lad's got talent ») : Héros dès l'étape
/// Expérience, ils comptent pour l'Exploration (un dé de plus) et font leur recherche d'objet rare, comme
/// les autres Héros - lecture de la séquence d'après-bataille retenue par l'utilisateur le 2026-11-11.
/// Pas encore désignables pour un effet d'Exploration qui vise « un de vos Héros » (Puits, Fosse, XP
/// répartie) : l'Exploration est enregistrée avant la création du promu en base.</summary>
public partial class EndOfGamePageViewModel
{
    private PromotionContext _promotionContext = new(0, []);

    /// <summary>Une ligne par Héros promu (le Warrior en attente de création, PromotedWarriorPreview), utilisée
    /// pour sa recherche d'objet rare - jamais ajoutée à WarriorRows (elle referait les étapes Expérience,
    /// Blessures… et compterait deux fois dans la Valeur de Bande).</summary>
    private readonly Dictionary<AdvanceRollEntry, WarriorOutcomeRow> _promotedHeroRows = new();

    /// <summary>Promotions dont le Héros est déjà constitué (nom et deux listes choisis).</summary>
    private IEnumerable<(AdvanceRollEntry Entry, Warrior Hero)> PromotedHeroes =>
        _promotionContext.Promotions
            .Where(e => e.PromotedWarriorPreview is not null)
            .Select(e => (e, e.PromotedWarriorPreview!));

    /// <summary>Un dé d'Exploration par Héros promu - le modèle a survécu à la bataille (il a gagné de l'XP).</summary>
    private int PromotedHeroCount => _promotionContext.Promotions.Count;

    /// <summary>Ajoute (ou retire, si la promotion a été annulée en revenant en arrière) la recherche d'objet
    /// rare de chaque Héros promu - appelée à l'entrée de l'étape Objets rares.</summary>
    private void SyncPromotedRareItemSearches()
    {
        var current = PromotedHeroes.ToList();
        foreach (var stale in _promotedHeroRows.Keys.Where(k => current.All(c => c.Entry != k)).ToList())
        {
            var staleRow = _promotedHeroRows[stale];
            foreach (var search in RareItemSearchEntries.Where(s => s.Hero == staleRow).ToList())
            {
                search.PropertyChanged -= OnRareItemSearchEntryChanged;
                RareItemSearchEntries.Remove(search);
            }
            _promotedHeroRows.Remove(stale);
        }

        foreach (var (entry, hero) in current)
        {
            if (_promotedHeroRows.ContainsKey(entry)) continue;
            var groupRow = WarriorRows.First(r => r.AdvanceRolls.Contains(entry) || r.ExplorationAdvanceRolls.Contains(entry));
            var row = new WarriorOutcomeRow(hero, groupRow.ArchetypeName, gainsExperience: true);
            _promotedHeroRows[entry] = row;
            var search = new RareItemSearchEntry(row, Loc, _ownedEquipmentItemIds);
            search.PropertyChanged += OnRareItemSearchEntryChanged;
            RareItemSearchEntries.Add(search);
        }
        OnPropertyChanged(nameof(HasEligibleHeroesForRareItems));
    }
}
