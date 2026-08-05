using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mutations;

/// <summary>
/// Tuile de grille (MutationView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// InjuryRow/SpecialRuleRow.
/// </summary>
public partial class MutationRow : ObservableObject
{
    public Mutation Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public MutationRow(Mutation item) => Item = item;
}
