using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

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

    /// <summary>Not stored on the Warrior itself - resolved by the ViewModel from the warrior's own
    /// WarriorArchetype.SpecialRules plus the band's WarbandArchetype.SpecialRules (band-wide rules
    /// apply to every warrior regardless of type). Purely a reference/read-only display, same as
    /// RoleName - editing happens on the archetype in the Library, not per-Warrior.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    /// <summary>Mirrors Warrior.Spells - read-only display, managed via WarriorEditDialog (conditional
    /// Sorts tab, only shown when the warrior's archetype is a spellcaster).</summary>
    public ObservableCollection<WarriorSpell> Spells { get; }

    /// <summary>Mirrors Warrior.Mutations - read-only display, managed via WarriorEditDialog (conditional
    /// Mutations tab, only shown when the warrior's archetype has WarriorArchetype.CanBuyMutations).</summary>
    public ObservableCollection<WarriorMutation> Mutations { get; }

    /// <summary>Bande entière (WarbandArchetype.MagicSchools), pas propre à ce guerrier - un lanceur de
    /// sorts pioche dans les écoles de SA bande, il n'a pas sa propre affiliation. Vide (et donc
    /// invisible, voir HasMagicSchools) pour tout guerrier dont l'archétype n'est pas IsSpellcaster -
    /// résolu par WarbandDetailViewModel.ToRow, même idiome que SpecialRules band-wide.</summary>
    public ObservableCollection<MagicSchool> MagicSchools { get; }

    /// <summary>Mirrors Warrior.Animal (an EquipmentItem, Category == Animal) - read-only display,
    /// managed via WarriorEditDialog.</summary>
    public EquipmentItem? Animal => Warrior.Animal;

    public bool HasEquipment => Equipment.Count > 0;
    public bool HasSkills => Skills.Count > 0;
    public bool HasInjuries => Injuries.Count > 0;
    public bool HasSpecialRules => SpecialRules.Count > 0;
    public bool HasSpells => Spells.Count > 0;
    public bool HasMutations => Mutations.Count > 0;
    public bool HasAnimal => Animal is not null;
    public bool HasMagicSchools => MagicSchools.Count > 0;

    /// <summary>Drives the read-only treatment of the card in the "Morts" group (hides Edit/Add/Remove
    /// buttons) - Dead is only ever reached via the End of Game wizard, see WarriorStatus.</summary>
    public bool IsDead => Warrior.Status == WarriorStatus.Dead;

    /// <summary>"× 3" next to the name for a Henchman group with more than one living model - empty for
    /// a Hero (always HeadCount 1) or a lone Henchman, see Warrior.HeadCount.</summary>
    public string HeadCountDisplay => !Warrior.IsHero && Warrior.HeadCount > 1 ? $"× {Warrior.HeadCount}" : string.Empty;

    public WarriorRow(Warrior warrior, string roleName, IEnumerable<SpecialRule>? specialRules = null, IEnumerable<MagicSchool>? magicSchools = null)
    {
        Warrior = warrior;
        RoleName = roleName;
        Equipment = new ObservableCollection<WarriorEquipment>(warrior.Equipment);
        Skills = new ObservableCollection<WarriorSkill>(warrior.Skills);
        Injuries = new ObservableCollection<WarriorInjury>(warrior.Injuries);
        Spells = new ObservableCollection<WarriorSpell>(warrior.Spells);
        Mutations = new ObservableCollection<WarriorMutation>(warrior.Mutations);
        SpecialRules = new ObservableCollection<SpecialRule>(specialRules ?? Enumerable.Empty<SpecialRule>());
        MagicSchools = new ObservableCollection<MagicSchool>(magicSchools ?? Enumerable.Empty<MagicSchool>());
    }
}
