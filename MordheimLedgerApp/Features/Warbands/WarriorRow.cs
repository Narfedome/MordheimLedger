using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>
/// Ligne du roster (WarbandDetailPage) : pas de sélection - chaque action (éditer, ajouter un objet...)
/// est un bouton directement sur la carte, il n'y a plus de "sélectionner puis agir" comme dans la
/// Bibliothèque.
/// </summary>
public partial class WarriorRow : ObservableObject
{
    public Warrior Warrior { get; }

    /// <summary>The archetype's name (e.g. "Mercenary Captain") shown instead of a plain Hero/Henchman
    /// label — looked up by the ViewModel from the warband's recruitable archetypes, "?" if unknown
    /// (e.g. the archetype was since deleted from the Library).</summary>
    public string RoleName { get; }

    /// <summary>Mirrors Warrior.Equipment as an ObservableCollection so add/remove reflects on the card without a full reload.</summary>
    public ObservableCollection<WarriorEquipment> Equipment { get; }

    /// <summary>Mirrors Warrior.Skills as an ObservableCollection so add/remove reflects on the card without a full reload.</summary>
    public ObservableCollection<WarriorSkill> Skills { get; }

    /// <summary>Mirrors Warrior.Injuries as an ObservableCollection - fed both by the End of Game
    /// Serious Injury roll and by manual additions via WarriorEditDialog (not editable directly from
    /// this card, which stays read-only).</summary>
    public ObservableCollection<WarriorInjury> Injuries { get; }

    public bool HasInjuries => Injuries.Count > 0;

    public WarriorRow(Warrior warrior, string roleName)
    {
        Warrior = warrior;
        RoleName = roleName;
        Equipment = new ObservableCollection<WarriorEquipment>(warrior.Equipment);
        Skills = new ObservableCollection<WarriorSkill>(warrior.Skills);
        Injuries = new ObservableCollection<WarriorInjury>(warrior.Injuries);
    }
}
