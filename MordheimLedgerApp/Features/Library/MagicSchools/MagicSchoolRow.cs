using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.MagicSchools;

/// <summary>
/// Tuile de grille (MagicSchoolView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - même mécanisme que
/// SpecialRuleRow.
/// </summary>
public partial class MagicSchoolRow : ObservableObject
{
    public MagicSchool Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public MagicSchoolRow(MagicSchool item) => Item = item;
}
