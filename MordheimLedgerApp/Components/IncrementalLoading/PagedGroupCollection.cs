using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Components;

/// <summary>Ce qu'attend LoadMoreOnScrollBehavior de l'ItemsSource d'une CollectionView pour la
/// charger au fil du défilement - pas de commande à câbler par ViewModel : le behavior détecte seul
/// qu'une source est paginable.</summary>
public interface IIncrementalCollection
{
    bool HasMore { get; }

    /// <summary>Nombre de tuiles déjà affichées (tous groupes confondus) - comparé par le behavior à
    /// l'index de la dernière tuile visible.</summary>
    int LoadedItemCount { get; }

    void LoadMore();
}

/// <summary>Chargement au défilement des grilles du Codex (2026-09-24, même principe que la
/// bibliothèque de pistes de DmTools, LibraryTrackViewModel) - sauf qu'ici tout le catalogue tient
/// déjà en mémoire (filtré/groupé par le ViewModel comme avant) : ce qui coûte, c'est de créer toutes
/// les tuiles d'un coup, pas la requête. Garde donc les groupes complets en source et n'expose que
/// les PageSize premières tuiles, puis page par page via LoadMore (LoadMoreOnScrollBehavior).
/// Un groupe source n'est ajouté à l'affichage qu'avec sa première page de tuiles déjà dedans
/// (copie vide via emptyCopy, puis remplie) : jamais d'en-tête de groupe affiché seul.</summary>
public sealed class PagedGroupCollection<TGroup, TRow> : ObservableCollection<TGroup>, IIncrementalCollection
    where TGroup : ObservableCollection<TRow>
{
    public const int DefaultPageSize = 40;

    private readonly IReadOnlyList<TGroup> _source;
    private readonly Func<TGroup, TGroup> _emptyCopy;
    private readonly int _pageSize;
    private int _groupIndex;
    private int _rowIndex;

    public PagedGroupCollection() : this(Array.Empty<TGroup>(), g => g) { }

    public PagedGroupCollection(IEnumerable<TGroup> source, Func<TGroup, TGroup> emptyCopy, int pageSize = DefaultPageSize)
    {
        _source = source.Where(g => g.Count > 0).ToList();
        _emptyCopy = emptyCopy;
        _pageSize = pageSize;
        LoadMore();
    }

    /// <summary>Toutes les lignes, y compris celles pas encore affichées - pour retrouver une ligne
    /// précise (ex. tuile tout juste créée à sélectionner), voir EnsureLoaded.</summary>
    public IEnumerable<TRow> AllRows => _source.SelectMany(g => g);

    public bool HasMore => _groupIndex < _source.Count;

    public int LoadedItemCount { get; private set; }

    public void LoadMore()
    {
        var remaining = _pageSize;
        while (remaining > 0 && HasMore)
        {
            var sourceGroup = _source[_groupIndex];
            var take = Math.Min(remaining, sourceGroup.Count - _rowIndex);

            if (_rowIndex == 0)
            {
                // Nouveau groupe : rempli AVANT d'être ajouté (une seule notification pour le groupe).
                var displayed = _emptyCopy(sourceGroup);
                for (var i = 0; i < take; i++) displayed.Add(sourceGroup[i]);
                Add(displayed);
            }
            else
            {
                // Suite d'un groupe déjà affiché (coupé par la page précédente).
                var displayed = this[^1];
                for (var i = _rowIndex; i < _rowIndex + take; i++) displayed.Add(sourceGroup[i]);
            }

            _rowIndex += take;
            remaining -= take;
            LoadedItemCount += take;
            if (_rowIndex >= sourceGroup.Count)
            {
                _groupIndex++;
                _rowIndex = 0;
            }
        }
    }

    /// <summary>Charge les pages suivantes jusqu'à ce que row soit affichée (no-op si déjà le cas, ou si
    /// elle n'est pas dans la source).</summary>
    public void EnsureLoaded(TRow row)
    {
        if (!AllRows.Contains(row)) return;
        while (HasMore && !this.Any(g => g.Contains(row)))
            LoadMore();
    }
}
