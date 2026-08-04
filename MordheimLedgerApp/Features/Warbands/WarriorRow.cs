using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>
/// Ligne du roster (WarbandDetailPage) : pas de sélection. Éditer est le seul bouton directement sur
/// la carte - Équipement/Compétences/Blessures sont toutes en lecture seule ici, gérées depuis
/// WarriorEditDialog (onglets dédiés) plutôt qu'à deux endroits différents.
/// </summary>
public partial class WarriorRow : ObservableObject
{
    public Warrior Warrior { get; }

    /// <summary>The archetype's name (e.g. "Mercenary Captain") shown instead of a plain Hero/Henchman
    /// label — looked up by the ViewModel from the warband's recruitable archetypes, "?" if unknown
    /// (e.g. the archetype was since deleted from the Library).</summary>
    public string RoleName { get; }

    /// <summary>Mirrors Warrior.Equipment - read-only display, managed via WarriorEditDialog.</summary>
    public ObservableCollection<WarriorEquipment> Equipment { get; }

    /// <summary>Mirrors Warrior.Skills - read-only display, managed via WarriorEditDialog.</summary>
    public ObservableCollection<WarriorSkill> Skills { get; }

    /// <summary>Mirrors Warrior.Injuries - fed both by the End of Game Serious Injury roll and by
    /// manual additions via WarriorEditDialog - read-only display here.</summary>
    public ObservableCollection<WarriorInjury> Injuries { get; }

    public bool HasEquipment => Equipment.Count > 0;
    public bool HasSkills => Skills.Count > 0;
    public bool HasInjuries => Injuries.Count > 0;

    /// <summary>Drives the read-only treatment of the card in the "Morts" group (hides Edit/Add/Remove
    /// buttons) - Dead is only ever reached via the End of Game wizard, see WarriorStatus.</summary>
    public bool IsDead => Warrior.Status == WarriorStatus.Dead;

    public WarriorRow(Warrior warrior, string roleName)
    {
        Warrior = warrior;
        RoleName = roleName;
        Equipment = new ObservableCollection<WarriorEquipment>(warrior.Equipment);
        Skills = new ObservableCollection<WarriorSkill>(warrior.Skills);
        Injuries = new ObservableCollection<WarriorInjury>(warrior.Injuries);
    }
}
