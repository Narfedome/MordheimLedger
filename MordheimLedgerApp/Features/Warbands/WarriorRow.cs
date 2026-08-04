using System.Collections.ObjectModel;
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

    /// <summary>The archetype's name (e.g. "Mercenary Captain") shown instead of a plain Hero/Henchman
    /// label — looked up by the ViewModel from the warband's recruitable archetypes, "?" if unknown
    /// (e.g. the archetype was since deleted from the Library).</summary>
    public string RoleName { get; }

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Mirrors Warrior.Equipment as an ObservableCollection so add/remove reflects on the card without a full reload.</summary>
    public ObservableCollection<WarriorEquipment> Equipment { get; }

    /// <summary>Mirrors Warrior.Skills as an ObservableCollection so add/remove reflects on the card without a full reload.</summary>
    public ObservableCollection<WarriorSkill> Skills { get; }

    public WarriorRow(Warrior warrior, string roleName)
    {
        Warrior = warrior;
        RoleName = roleName;
        Equipment = new ObservableCollection<WarriorEquipment>(warrior.Equipment);
        Skills = new ObservableCollection<WarriorSkill>(warrior.Skills);
    }
}
