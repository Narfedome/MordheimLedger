using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>
/// Ligne du roster (WarbandDetailPage) : IsSelected est portée par la ligne elle-même, pas par la
/// sélection native du CollectionView (SelectionMode="None") - même raison que WarbandRow
/// (WarbandListPage) : sur Android, le fond de sélection natif reste teinté par colorAccent quel que
/// soit le style posé dessus, y compris via VisualStateManager sur le Border (constaté sur
/// SelectableGridItemBorderStyle, pourtant déjà utilisé par la Library). Seule une sélection
/// entièrement gérée à la main (jamais confiée à SelectionMode) évite ce souci.
/// </summary>
public partial class WarriorRow : ObservableObject
{
    public Warrior Warrior { get; }

    [ObservableProperty]
    private bool isSelected;

    public WarriorRow(Warrior warrior) => Warrior = warrior;
}
